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
});
