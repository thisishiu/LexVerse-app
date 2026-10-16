import { DesktopBridge } from "./bridge";

describe("DesktopBridge", () => {
  it("acknowledges browser sign-in promptly and uses the normal command deadline", async () => {
    vi.useFakeTimers();
    const previousChrome = window.chrome;
    let receive: ((event: { data: unknown }) => void) | undefined;
    const transport = {
      postMessage: vi.fn(),
      addEventListener: vi.fn((_type, handler) => { receive = handler; }),
      removeEventListener: vi.fn()
    };
    Object.defineProperty(window, "chrome", { configurable: true, value: { webview: transport } });
    const bridge = new DesktopBridge();
    try {
      const signIn = bridge.command("account.signIn");
      await vi.advanceTimersByTimeAsync(100);
      const command = transport.postMessage.mock.calls[0][0];
      receive!({ data: { version: 1, id: command.id, ok: true, payload: null } });
      await expect(signIn).resolves.toBeNull();

      const timedOutSignIn = expect(bridge.command("account.signIn")).rejects.toThrow("bridge-timeout");
      await vi.advanceTimersByTimeAsync(30_000);
      await timedOutSignIn;

      const normalCommand = expect(bridge.command("app.getSnapshot")).rejects.toThrow("bridge-timeout");
      await vi.advanceTimersByTimeAsync(30_000);
      await normalCommand;
    } finally {
      bridge.dispose();
      Object.defineProperty(window, "chrome", { configurable: true, value: previousChrome });
      vi.useRealTimers();
    }
  });

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
