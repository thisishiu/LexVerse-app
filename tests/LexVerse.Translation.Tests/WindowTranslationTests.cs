using System.Text.Json;
using LexVerse.Application.Desktop;
using LexVerse.Application.Realtime;
using LexVerse.Core.Capture;
using LexVerse.Core.Geometry;
using LexVerse.Core.Pipeline;
using LexVerse.Core.Product;
using LexVerse.Core.Windows;
using LexVerse.Infrastructure.Windows;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class WindowTranslationTests
{
    private static readonly ScreenRect Bounds = new(-1200, 40, 800, 600);
    private static readonly WindowCaptureTarget Target = new(new IntPtr(123), 10, 20, "Document");

    [Fact]
    public void WindowRequestRequiresNativeIdentityAndOwnEntitlement()
    {
        Assert.Throws<ArgumentException>(() => new RealtimeTranslationRequest(
            RealtimeCaptureKind.Window, Bounds, "en-US", RealtimeTranslationOptions.Document));
        Assert.Throws<ArgumentException>(() => new RealtimeTranslationRequest(
            RealtimeCaptureKind.Region, Bounds, "en-US", RealtimeTranslationOptions.Document, Target));
        Assert.Throws<ArgumentException>(() => new RealtimeTranslationRequest(
            RealtimeCaptureKind.Window, Bounds, "en-US", RealtimeTranslationOptions.Document, Target with { Hwnd = IntPtr.Zero }));
        var request = new RealtimeTranslationRequest(RealtimeCaptureKind.Window, Bounds,
            "en-US", RealtimeTranslationOptions.Document, Target);
        Assert.Equal(ProductFeatures.WindowTranslation, request.RequiredFeature);
        Assert.DoesNotContain(ProductFeatures.WindowTranslation, LexVerse.Application.Product.MvpProductPolicy.FreeFeatures);
    }

    [Fact]
    public void PickerUsesOpaqueIdsRefreshInvalidatesAndClosedWindowIsRejected()
    {
        var alive = true;
        var registry = new WindowTargetRegistry(new Enumerator(), _ => Target, _ => alive);
        var entry = Assert.Single(registry.Refresh());
        Assert.Equal("editor", entry.ProcessName);
        Assert.NotEqual(Target.Hwnd.ToString(), entry.Id);
        Assert.Equal(Target, registry.Resolve(entry.Id));
        Assert.Throws<InvalidOperationException>(() => registry.Resolve("123"));
        registry.Refresh();
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(entry.Id));
        entry = Assert.Single(registry.Refresh());
        alive = false;
        Assert.Throws<InvalidOperationException>(() => registry.Resolve(entry.Id));
    }

    [Fact]
    public void BridgeRejectsHandleInjectionAndPickerDoesNotSerializeNativeHandles()
    {
        using var payload = JsonDocument.Parse("{\"windowId\":\"selection\",\"hwnd\":123}");
        Assert.Throws<JsonException>(() => DesktopBridgeProtocol.DeserializePayload<StartPayload>(payload.RootElement));
        var registry = new WindowTargetRegistry(new Enumerator(), _ => Target, _ => true);
        var json = DesktopBridgeProtocol.SerializeResult("1", true, registry.Refresh());
        Assert.DoesNotContain("hwnd", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("processId", json);
    }

    private sealed record StartPayload(string WindowId);
    private sealed class Enumerator : IWindowEnumerator
    {
        public IReadOnlyList<WindowCandidate> EnumerateWindows() =>
            [new(Target.Hwnd, Target.DisplayName, "editor", Bounds, Bounds, true, false)];
    }
}
