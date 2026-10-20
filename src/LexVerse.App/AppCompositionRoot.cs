using System.IO;
using System.Net.Http;
using LexVerse.Application.Account;
using LexVerse.Application.Commerce;
using LexVerse.Application.Diagnostics;
using LexVerse.Application.Desktop;
using LexVerse.Application.Modules;
using LexVerse.Application.Popup;
using LexVerse.Application.Product;
using LexVerse.Application.Privacy;
using LexVerse.Application.Realtime;
using LexVerse.App.Features.Popup;
using LexVerse.App.Features.Realtime;
using LexVerse.Core.Product;
using LexVerse.Core.Translation;
using LexVerse.Infrastructure.Translation;
using LexVerse.Infrastructure.Commerce;
using LexVerse.Infrastructure.Realtime;
using LexVerse.Infrastructure.Product;
using LexVerse.Infrastructure.Privacy;
using LexVerse.Infrastructure.Windows;
using LexVerse.Infrastructure.Configuration;
using LexVerse.Infrastructure.Desktop;

namespace LexVerse.App;

public sealed class AppCompositionRoot : IDisposable
{
    private readonly FeatureAccessService _featureAccessService;
    private readonly SignedEntitlementFileCache? _entitlementCache;
    private readonly HttpClient _backendHttpClient;
    private readonly HttpClient _popupHttpClient;
    private readonly IAccountSessionProvider _accountProvider;
    private readonly IFeatureAccessService _accessService;
    private readonly IPrivacyPreferencesService _privacyPreferences;
    private readonly IDisposable? _ownedPrivacyPreferences;
    private readonly IDesktopPreferencesService _desktopPreferences;
    private readonly IDisposable? _ownedDesktopPreferences;
    private readonly WpfRealtimeOverlayOutput _realtimeOutput;
    private bool _disposed;

    public AppCompositionRoot(
        IOperationalTelemetry? telemetry = null,
        IPrivacyPreferencesService? privacyPreferences = null,
        IDesktopPreferencesService? desktopPreferences = null)
    {
        BackendEndpointPolicy.LoadEnvironment();
        telemetry ??= NullOperationalTelemetry.Instance;
        if (privacyPreferences is null)
        {
            var persistent = new PersistentPrivacyPreferencesService();
            privacyPreferences = persistent;
            _ownedPrivacyPreferences = persistent;
        }

        _privacyPreferences = privacyPreferences;
        if (desktopPreferences is null)
        {
            var persistent = new PersistentDesktopPreferencesService();
            desktopPreferences = persistent;
            _ownedDesktopPreferences = persistent;
        }

        _desktopPreferences = desktopPreferences;
        _backendHttpClient = new HttpClient();
        _popupHttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(6) };
        var launcher = new WindowsExternalUriLauncher();
        _accountProvider = CreateAccountProvider(_backendHttpClient, launcher);
        var entitlementProvider = CreatePaidEntitlementProvider(
            _backendHttpClient,
            _accountProvider,
            out _entitlementCache);
        _featureAccessService = new FeatureAccessService(entitlementProvider);
        _accessService = _featureAccessService;
        var featureGate = new FeatureGate(_accessService);
        var selectedTextReader = new WindowsSelectedTextReader();
        ITextTranslator translator = CreateRemoteTranslator(["translation.popup"]);
        MonitorService = new WindowsMonitorService();

        PopupModule = new PopupFeatureModule(new PopupTranslationUseCase(
            featureGate,
            selectedTextReader,
            translator,
            telemetry,
            new RemotePopupDocumentTranslator(_popupHttpClient,
                () => new Uri((GetAbsoluteUri("LEXVERSE_TRANSLATION_ENDPOINT")
                    ?? throw new InvalidOperationException("Configure LEXVERSE_TRANSLATION_ENDPOINT before translating.")).AbsoluteUri.TrimEnd('/') + "/popup"),
                _accountProvider, _privacyPreferences)));
        _realtimeOutput = new WpfRealtimeOverlayOutput(
            System.Windows.Application.Current.Dispatcher,
            _desktopPreferences);
        var realtimeFactory = new WindowsRealtimePipelineSessionFactory(
            translator,
            monitorService: MonitorService,
            translatorFactory: request => CreateRemoteTranslator(new[] {
                request.RequiredFeature.Value
            }.Concat(request.Options.Mode switch {
                LexVerse.Core.Pipeline.OcrProcessingMode.Comic => new[] { "mode.comic" },
                LexVerse.Core.Pipeline.OcrProcessingMode.Document => new[] { "mode.document" },
                _ => Array.Empty<string>()
            }).ToArray()));
        RealtimeModule = new RealtimeFeatureModule(new RealtimeTranslationCoordinator(
            featureGate,
            realtimeFactory,
            _realtimeOutput,
            telemetry));
        Modules = new FeatureModuleRegistry([PopupModule, RealtimeModule]);
        Commerce = new CommerceCoordinator(
            _accountProvider,
            CreateCatalogProvider(_backendHttpClient),
            CreateCommerceBackend(_backendHttpClient, _accountProvider),
            launcher,
            _accessService);
    }

    public PopupFeatureModule PopupModule { get; }

    public RealtimeFeatureModule RealtimeModule { get; }

    public WindowsMonitorService MonitorService { get; }

    public FeatureModuleRegistry Modules { get; }

    public CommerceCoordinator Commerce { get; }

    public MainWindow CreateMainWindow()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new MainWindow(
            PopupModule,
            RealtimeModule,
            Modules,
            MonitorService,
            _accessService,
            Commerce,
            _privacyPreferences,
            _desktopPreferences,
            _realtimeOutput);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        RealtimeModule.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _realtimeOutput.Dispose();
        Commerce.Dispose();
        _featureAccessService.Dispose();
        _entitlementCache?.Dispose();
        (_accountProvider as IDisposable)?.Dispose();
        _backendHttpClient.Dispose();
        _popupHttpClient.Dispose();
        _ownedPrivacyPreferences?.Dispose();
        _ownedDesktopPreferences?.Dispose();
    }

    private static IEntitlementProvider CreatePaidEntitlementProvider(
        HttpClient httpClient,
        IAccountSessionProvider accounts,
        out SignedEntitlementFileCache? cache)
    {
        cache = null;
        var endpointText = Environment.GetEnvironmentVariable("LEXVERSE_ENTITLEMENT_ENDPOINT");
        var keyId = Environment.GetEnvironmentVariable("LEXVERSE_ENTITLEMENT_KEY_ID");
        var publicKeyPem = Environment.GetEnvironmentVariable("LEXVERSE_ENTITLEMENT_PUBLIC_KEY_PEM");
        if (string.IsNullOrWhiteSpace(endpointText) ||
            string.IsNullOrWhiteSpace(keyId) ||
            string.IsNullOrWhiteSpace(publicKeyPem) ||
            !accounts.IsConfigured ||
            !Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
        {
            return new UnavailableEntitlementProvider();
        }

        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LexVerse",
            "Entitlements",
            "entitlements.json");
        try
        {
            cache = new SignedEntitlementFileCache(cachePath);
            var verifier = new EntitlementEnvelopeVerifier([
                new EntitlementVerificationKey(keyId, publicKeyPem.Replace("\\n", "\n", StringComparison.Ordinal))
            ]);
            return new RemoteSignedEntitlementProvider(
                httpClient,
                endpoint,
                new AccountEntitlementRequestContextProvider(accounts),
                verifier,
                cache,
                allowInsecureLocalhost: BackendEndpointPolicy.AllowInsecureLocalhost);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            cache?.Dispose();
            cache = null;
            return new UnavailableEntitlementProvider();
        }
    }

    private static IAccountSessionProvider CreateAccountProvider(
        HttpClient httpClient,
        IExternalUriLauncher launcher)
    {
        var authorization = GetAbsoluteUri("LEXVERSE_OAUTH_DESKTOP_REQUEST_ENDPOINT");
        var token = GetAbsoluteUri("LEXVERSE_OAUTH_TOKEN_ENDPOINT");
        var account = GetAbsoluteUri("LEXVERSE_ACCOUNT_ENDPOINT");
        var clientId = Environment.GetEnvironmentVariable("LEXVERSE_OAUTH_CLIENT_ID");
        if (authorization is null || token is null || account is null ||
            string.IsNullOrWhiteSpace(clientId))
        {
            return new UnavailableAccountSessionProvider();
        }

        try
        {
            var storePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LexVerse",
                "Account",
                "session.dat");
            return new OAuthPkceAccountSessionProvider(
                httpClient,
                new OAuthNativeAppOptions(authorization, token, account, clientId),
                new WindowsDpapiAccountStore(storePath),
                launcher);
        }
        catch (ArgumentException)
        {
            return new UnavailableAccountSessionProvider();
        }
    }

    private static IProductCatalogProvider CreateCatalogProvider(HttpClient httpClient)
    {
        var endpoint = GetAbsoluteUri("LEXVERSE_CATALOG_ENDPOINT");
        if (endpoint is null)
        {
            return new UnavailableProductCatalogProvider();
        }

        try
        {
            return new RemoteProductCatalogProvider(httpClient, endpoint);
        }
        catch (ArgumentException)
        {
            return new UnavailableProductCatalogProvider();
        }
    }

    private static ICommerceBackend CreateCommerceBackend(
        HttpClient httpClient,
        IAccountSessionProvider accounts)
    {
        var checkout = GetAbsoluteUri("LEXVERSE_CHECKOUT_ENDPOINT");
        var restore = GetAbsoluteUri("LEXVERSE_RESTORE_ENDPOINT");
        if (checkout is null || restore is null || !accounts.IsConfigured)
        {
            return new UnavailableCommerceBackend();
        }

        try
        {
            return new RemoteCommerceBackend(httpClient, checkout, restore, accounts);
        }
        catch (ArgumentException)
        {
            return new UnavailableCommerceBackend();
        }
    }

    private static Uri? GetAbsoluteUri(string variableName) =>
        Uri.TryCreate(
            Environment.GetEnvironmentVariable(variableName),
            UriKind.Absolute,
            out var uri)
            ? uri
            : null;

    private ITextTranslator CreateRemoteTranslator(string[] features) =>
        new ConsentCheckingTextTranslator(new DeferredTextTranslator(() =>
            new RemoteTextTranslator(_backendHttpClient,
                GetAbsoluteUri("LEXVERSE_TRANSLATION_ENDPOINT")
                    ?? throw new InvalidOperationException("Configure LEXVERSE_TRANSLATION_ENDPOINT before translating."),
                _accountProvider, features)), _privacyPreferences);
}
