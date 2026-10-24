# Overlay stability and performance

## Changes

- Restored `WDA_EXCLUDEFROMCAPTURE`, which had been disabled for demo recording. Without it, monitor/region OCR can read the translation overlay itself. Failures are exposed through `IsExcludedFromCapture` and a content-free Trace warning.
- Match boxes by source text and nearest physical region, with one-to-one matching for repeated text. Preserve visual instances/order across OCR reordering. Suppress movement of all rectangle edges within 2 physical pixels relative to the last accepted rectangle, without accumulating drift.
- Merge partial results without removing pending boxes; remove absent boxes on the final result. Session IDs, frame sequences, dispatcher generations, and linked session/iteration cancellation prevent stale work from rendering after stop/target changes.
- Skip text creation/measurement for identical display inputs. Cache up to 512 layouts, including text, dimensions, font inputs, DPI, padding, and UI culture. Settings changes invalidate the affected display keys.
- Run the realtime loop outside the caller's UI synchronization context. Limit `FrameProcessed` UI notifications to once per 250 ms, while retaining per-frame counters and existing telemetry sampling.
- Skip unchanged physical bounds and rotation. Cache occlusion/z-order state before allocating GDI regions or calling `SetWindowRgn`/`SetWindowPos`. Keep the existing 30 ms window tracking cadence at Background priority and stop its timer when invalidated/cleared.
- Require matching geometry and OCR region selection before taking the unchanged-frame OCR fast path. Keep exact pixel comparison. Dispose a captured native frame if cancellation wins its completion race.

## Verification (2026-10-24)

- Complete desktop build, including TypeScript, Vite and WPF: succeeded with no warnings/errors.
- Translation/application tests: 243 passed; 2 existing opt-in integration tests skipped (Google Cloud credentials and Postman simulation).
- OCR tests: 49 passed.
- Desktop UI tests: 50 passed with two workers. The initial unrestricted parallel run timed out in the navigation test under concurrent build load; the rerun retained the default test timeout. jsdom reports its existing missing-canvas warnings.
- Regression coverage includes unchanged/reordered/jittering boxes without collection/property mutations, duplicate text, partial completion/removal, geometry/DPI and settings changes, bounded layout cache, unchanged OCR/translation reuse, and cancellation of old-session/queued window-iteration output.

## Reproducible synthetic benchmark

From the repository root:

```powershell
rtk proxy dotnet run --project scripts/OverlayPerformanceBench/OverlayPerformanceBench.csproj --configuration Release --artifacts-path .artifacts/overlay-bench
```

The harness compares the previous measure-every-frame/index-copy path with the production item collection and layout cache. It uses 80 fixed boxes and 120 repeated frames after warming the formatter/JIT. The optimized sample starts after its first render, as required by the static-screen acceptance criterion.

| Metric | Previous path | Optimized path |
| --- | ---: | ---: |
| Elapsed time | 16,810 ms | 15.6 ms |
| Process CPU time | 17,328 ms | 15.6 ms |
| Managed allocations | 895,951,680 bytes | 6,018,800 bytes |
| Layout calculations during repeated updates | 9,600 | 0 |
| Collection/property changes after first render | Not instrumented | 0 / 0 |

These results isolate overlay preparation, not full application CPU. They exclude native capture, OCR, translation/network, the WPF compositor and WebView. They do not establish the requested 30% whole-app CPU reduction or real-screen latency target.

## Live acceptance still required

On a representative interactive Windows session, compare the same screen and settings before/after: 60 seconds static in window/region/full-screen capture; scroll and change text; move/resize/minimize the target; cross monitors at 100/125/150/200% DPI; cover/uncover with another window; repeat start/stop and target switching. Confirm capture exclusion with a real monitor capture. Record process CPU, private bytes, GDI handles, startup/page-transition time and content-to-overlay latency. Box positions must remain fixed when static, changed text must update within the existing capture cadence plus processing time, and resources must not grow across repeated sessions.

This environment verified automated logic, rendering/layout tests, builds and the synthetic benchmark. Interactive multi-monitor capture, real 60-second CPU sampling, startup/page-transition profiling, and native handle soak testing have not been performed.
