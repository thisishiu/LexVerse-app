import { DesktopBridge } from "./bridge";

describe("DesktopBridge", () => {
  it("returns a privacy-safe preview snapshot outside WebView2", async () => {
    const bridge = new DesktopBridge();
    const snapshot = await bridge.command<{ status: string; privacy: { allowRemoteTextProcessing: boolean } }>("app.getSnapshot");

    expect(snapshot.status).toContain("Preview mode");
    expect(snapshot.privacy.allowRemoteTextProcessing).toBe(false);
    bridge.dispose();
  });

  it("rejects side effects when the native host is unavailable", async () => {
    const bridge = new DesktopBridge();
    await expect(bridge.command("realtime.stop")).rejects.toThrow("native-host-unavailable");
    bridge.dispose();
  });

  it("reconnects its native listener after a StrictMode cleanup", () => {
    const previousChrome = window.chrome;
    const transport = {
      postMessage: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn()
    };
    Object.defineProperty(window, "chrome", {
      configurable: true,
      value: { webview: transport }
    });

    try {
      const bridge = new DesktopBridge();
      expect(transport.addEventListener).toHaveBeenCalledTimes(1);

      bridge.dispose();
      expect(transport.removeEventListener).toHaveBeenCalledTimes(1);

      bridge.connect();
      expect(transport.addEventListener).toHaveBeenCalledTimes(2);
      bridge.dispose();
    } finally {
      Object.defineProperty(window, "chrome", {
        configurable: true,
        value: previousChrome
      });
    }
  });
});
