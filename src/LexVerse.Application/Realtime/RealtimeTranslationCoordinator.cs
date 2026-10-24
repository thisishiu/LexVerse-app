using System.Diagnostics;
using LexVerse.Application.Diagnostics;
using LexVerse.Application.Product;

namespace LexVerse.Application.Realtime;

/// <summary>
/// Owns the lifecycle and cadence of one realtime translation session at a time.
/// Starting a new target first cancels and disposes the previous session.
/// </summary>
public sealed class RealtimeTranslationCoordinator : IAsyncDisposable
{
    private readonly FeatureGate _featureGate;
    private readonly IRealtimePipelineSessionFactory _sessionFactory;
    private readonly IRealtimeOutput _output;
    private readonly IOperationalTelemetry _telemetry;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _stateGate = new();
    private ActiveRun? _activeRun;
    private RealtimeCoordinatorState _state = RealtimeCoordinatorState.Stopped;
    private bool _disposed;

    public RealtimeTranslationCoordinator(
        FeatureGate featureGate,
        IRealtimePipelineSessionFactory sessionFactory,
        IRealtimeOutput output,
        IOperationalTelemetry? telemetry = null)
    {
        _featureGate = featureGate ?? throw new ArgumentNullException(nameof(featureGate));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _telemetry = telemetry ?? NullOperationalTelemetry.Instance;
    }

    public event EventHandler<RealtimeCoordinatorState>? StateChanged;

    public event EventHandler<RealtimeFrameUpdate>? FrameProcessed;

    public RealtimeCoordinatorState State
    {
        get
        {
            lock (_stateGate)
            {
                return _state;
            }
        }
    }

    public async Task StartAsync(
        RealtimeTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync(cancellationToken);
            SetState(new RealtimeCoordinatorState(
                RealtimeCoordinatorStatus.Starting,
                request.CaptureKind));
            var correlationId = Guid.NewGuid();

            try
            {
                var session = await _featureGate.ExecuteAsync(
                    request.RequiredFeature,
                    token => _sessionFactory.CreateAsync(request, token),
                    cancellationToken);
                var run = new ActiveRun(session, new CancellationTokenSource(), correlationId);
                _activeRun = run;
                SetState(new RealtimeCoordinatorState(
                    RealtimeCoordinatorStatus.Running,
                    request.CaptureKind));
                RecordSession(
                    request,
                    correlationId,
                    OperationalOutcome.Started);
                // Capture, hashing, OCR preparation and continuation work must not inherit WPF's context.
                run.Task = Task.Run(() => RunLoopAsync(run, request));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetState(RealtimeCoordinatorState.Stopped);
                RecordSession(
                    request,
                    correlationId,
                    OperationalOutcome.Cancelled);
                throw;
            }
            catch (Exception exception)
            {
                SetState(new RealtimeCoordinatorState(
                    RealtimeCoordinatorStatus.Faulted,
                    request.CaptureKind,
                    TranslationFailure.Code(exception)));
                RecordSession(
                    request,
                    correlationId,
                    exception is FeatureAccessDeniedException
                        ? OperationalOutcome.Denied
                        : OperationalOutcome.Failed,
                    exception is FeatureAccessDeniedException
                        ? OperationalFailureCode.Entitlement
                        : OperationalFailureCode.Capture);
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync(cancellationToken);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        var run = _activeRun;
        _activeRun = null;
        if (run is null)
        {
            await _output.ClearAsync(cancellationToken);
            SetState(RealtimeCoordinatorState.Stopped);
            return;
        }

        SetState(new RealtimeCoordinatorState(
            RealtimeCoordinatorStatus.Stopping,
            State.CaptureKind));
        run.Cancellation.Cancel();

        try
        {
            await run.Task;
        }
        finally
        {
            run.Cancellation.Dispose();
        }

        SetState(run.CleanupFailed
            ? new RealtimeCoordinatorState(
                RealtimeCoordinatorStatus.Faulted,
                State.CaptureKind,
                "cleanup-failed")
            : RealtimeCoordinatorState.Stopped);
    }

    private async Task RunLoopAsync(ActiveRun run, RealtimeTranslationRequest request)
    {
        var cancellationToken = run.Cancellation.Token;
        var outcome = OperationalOutcome.Stopped;
        OperationalFailureCode? failureCode = null;
        var budget = RealtimePerformanceBudget.For(request.Options.Mode);
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var iterationTimer = Stopwatch.StartNew();
                var sequence = ++run.FrameSequence;
                var update = await run.Session.ProcessNextAsync(
                    (partial, token) => RenderCurrentAsync(partial, true, token),
                    cancellationToken);
                update = update with
                {
                    PerformanceBudgetExceeded = budget.IsExceeded(update.Timing)
                };
                update = update with { SessionId = run.CorrelationId, FrameSequence = sequence };
                await RenderCurrentAsync(update, false, cancellationToken);
                if (!cancellationToken.IsCancellationRequested && (run.LastNotificationTimestamp == 0 ||
                    Stopwatch.GetElapsedTime(run.LastNotificationTimestamp) >= TimeSpan.FromMilliseconds(250)))
                {
                    run.LastNotificationTimestamp = Stopwatch.GetTimestamp();
                    FrameProcessed?.Invoke(this, update);
                }
                run.FrameCount++;
                if (update.PerformanceBudgetExceeded)
                {
                    run.PerformanceBudgetExceededCount++;
                }

                if (run.FrameCount == 1
                    || run.FrameCount % 20 == 0
                    || update.PerformanceBudgetExceeded)
                {
                    RecordFrame(run.CorrelationId, request, update, run.FrameCount);
                }
                await DelayUntilNextCaptureAsync(
                    run.Session,
                    request.Options.OcrInterval,
                    iterationTimer.Elapsed,
                    cancellationToken);

                async Task RenderCurrentAsync(RealtimeFrameUpdate frame, bool partial, CancellationToken token)
                {
                    token.ThrowIfCancellationRequested();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(Volatile.Read(ref _activeRun), run)) return;
                    using var renderCancellation = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken);
                    await _output.RenderAsync(frame with
                    {
                        SessionId = run.CorrelationId,
                        FrameSequence = sequence
                    }, partial, renderCancellation.Token);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WindowTargetClosedException)
        {
            SetState(RealtimeCoordinatorState.Stopped);
        }
        catch (Exception exception)
        {
            outcome = OperationalOutcome.Failed;
            failureCode = OperationalFailureCode.Pipeline;
            SetState(new RealtimeCoordinatorState(
                RealtimeCoordinatorStatus.Faulted,
                request.CaptureKind,
                TranslationFailure.Code(exception)));
        }
        finally
        {
            try
            {
                await run.Session.DisposeAsync();
            }
            catch
            {
                run.CleanupFailed = true;
                outcome = OperationalOutcome.Failed;
                failureCode = OperationalFailureCode.Pipeline;
                if (State.Status != RealtimeCoordinatorStatus.Faulted) SetState(new RealtimeCoordinatorState(
                    RealtimeCoordinatorStatus.Faulted,
                    request.CaptureKind,
                    "cleanup-failed"));
            }

            try
            {
                await _output.ClearAsync(CancellationToken.None);
            }
            catch
            {
                run.CleanupFailed = true;
                outcome = OperationalOutcome.Failed;
                failureCode = OperationalFailureCode.Pipeline;
                if (State.Status != RealtimeCoordinatorStatus.Faulted) SetState(new RealtimeCoordinatorState(
                    RealtimeCoordinatorStatus.Faulted,
                    request.CaptureKind,
                    "cleanup-failed"));
            }

            RecordSession(
                request,
                run.CorrelationId,
                outcome,
                failureCode,
                run.Elapsed,
                run.FrameCount,
                run.PerformanceBudgetExceededCount);
        }
    }

    private void RecordFrame(
        Guid correlationId,
        RealtimeTranslationRequest request,
        RealtimeFrameUpdate update,
        int frameCount) =>
        _telemetry.TryRecord(new OperationalEvent
        {
            Kind = OperationalEventKind.RealtimeFrame,
            Outcome = OperationalOutcome.Succeeded,
            CorrelationId = correlationId,
            Duration = update.Timing.Total,
            CaptureDuration = update.Timing.Capture,
            OcrDuration = update.Timing.Ocr,
            TranslationDuration = update.Timing.Translation,
            CaptureKind = request.CaptureKind,
            ProcessingMode = request.Options.Mode,
            OcrBlockCount = update.OcrBlockCount,
            TranslatedBlockCount = update.TranslatedBlockCount,
            CacheHitCount = update.Timing.CacheHits,
            CacheMissCount = update.Timing.CacheMisses,
            FrameCount = frameCount,
            FrameChanged = update.FrameChanged,
            UsedCachedTranslation = update.UsedCachedTranslation,
            PerformanceBudgetExceeded = update.PerformanceBudgetExceeded
        });

    private void RecordSession(
        RealtimeTranslationRequest request,
        Guid correlationId,
        OperationalOutcome outcome,
        OperationalFailureCode? failureCode = null,
        TimeSpan? duration = null,
        int frameCount = 0,
        int performanceBudgetExceededCount = 0) =>
        _telemetry.TryRecord(new OperationalEvent
        {
            Kind = OperationalEventKind.RealtimeSession,
            Outcome = outcome,
            CorrelationId = correlationId,
            CaptureKind = request.CaptureKind,
            ProcessingMode = request.Options.Mode,
            FailureCode = failureCode,
            Duration = duration,
            FrameCount = frameCount,
            PerformanceBudgetExceededCount = performanceBudgetExceededCount
        });

    private static async Task DelayUntilNextCaptureAsync(
        IRealtimePipelineSession session,
        TimeSpan interval,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        var remaining = interval - elapsed;
        if (remaining > TimeSpan.FromMilliseconds(10))
        {
            await session.WaitForNextCaptureAsync(remaining, cancellationToken);
        }
        else
        {
            await Task.Yield();
        }
    }

    private void SetState(RealtimeCoordinatorState state)
    {
        EventHandler<RealtimeCoordinatorState>? handler;
        lock (_stateGate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            handler = StateChanged;
        }

        handler?.Invoke(this, state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _lifecycleGate.WaitAsync();
        try
        {
            await StopCoreAsync(CancellationToken.None);
            _disposed = true;
        }
        finally
        {
            _lifecycleGate.Release();
            _lifecycleGate.Dispose();
        }
    }

    private sealed class ActiveRun(
        IRealtimePipelineSession session,
        CancellationTokenSource cancellation,
        Guid correlationId)
    {
        private readonly long _startedTimestamp = Stopwatch.GetTimestamp();

        public IRealtimePipelineSession Session { get; } = session;

        public CancellationTokenSource Cancellation { get; } = cancellation;

        public Guid CorrelationId { get; } = correlationId;

        public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startedTimestamp);

        public bool CleanupFailed { get; set; }
        public long FrameSequence { get; set; }
        public long LastNotificationTimestamp { get; set; }

        public int FrameCount { get; set; }

        public int PerformanceBudgetExceededCount { get; set; }

        public Task Task { get; set; } = Task.CompletedTask;
    }
}
