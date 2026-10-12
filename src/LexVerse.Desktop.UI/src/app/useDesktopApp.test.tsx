import { StrictMode } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { useDesktopApp } from "./useDesktopApp";
import type { BridgeCommand, DesktopSnapshot } from "../types";

const snapshot: DesktopSnapshot = {
  moduleCount: 2, status: "Ready", hotkey: "F6 ready", quickTranslation: "",
  sourceLanguage: "auto", targetLanguage: "vi",
  privacy: { allowRemoteTextProcessing: false, allowLocalDiagnostics: true },
  realtime: { status: "stopped", canStartRegion: false, canStop: false, performanceBudgetExceeded: false },
  commerce: { configured: false, status: "FREE MODE", offers: [] }
};

afterEach(() => { delete window.chrome; });

function host() {
  const commands: BridgeCommand[] = [];
  const handlers = new Set<(event: { data: unknown }) => void>();
  window.chrome = { webview: {
    postMessage: (message) => commands.push(message as BridgeCommand),
    addEventListener: (_, handler) => { handlers.add(handler); },
    removeEventListener: (_, handler) => { handlers.delete(handler); }
  } };
  return {
    commands,
    reply(command: BridgeCommand, ok: boolean, payload?: unknown) {
      act(() => handlers.forEach(handler => handler({ data: {
        version: 1, id: command.id, ok, payload, errorCode: ok ? undefined : "operation-failed"
      } })));
    }
  };
}

it("signals ready only after the first snapshot is committed, including StrictMode remount", async () => {
  const native = host();
  const { result, unmount } = renderHook(() => useDesktopApp(), {
    wrapper: ({ children }) => <StrictMode>{children}</StrictMode>
  });
  expect(native.commands.every(command => command.type === "app.getSnapshot")).toBe(true);
  native.reply(native.commands.at(-1)!, true, snapshot);
  await waitFor(() => expect(native.commands.at(-1)?.type).toBe("app.uiReady"));
  expect(result.current.snapshot).toEqual(snapshot);
  expect(result.current.sourceLanguage).toBe("auto");
  expect(result.current.targetLanguage).toBe("vi");
  expect(result.current.error).toBeUndefined();
  native.reply(native.commands.at(-1)!, true);
  unmount();
});

it("does not signal ready when the initial snapshot fails", async () => {
  const native = host();
  const { result, unmount } = renderHook(() => useDesktopApp());
  native.reply(native.commands[0], false);
  await waitFor(() => expect(result.current.error).toBe("The native workspace is unavailable."));
  expect(native.commands.map(command => command.type)).toEqual(["app.getSnapshot"]);
  unmount();
});
