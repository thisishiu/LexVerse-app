import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { DesktopBridge } from "../../bridge";
import type { DesktopApp } from "../../app/useDesktopApp";
import type { DesktopSnapshot } from "../../types";
import { WorkspacePage } from "./WorkspaceScreen";
import { translationErrorMessage } from "../../translationErrors";

afterEach(cleanup);

it("selects an app through opaque IDs and refresh invalidates the selection", async () => {
  const preview = new DesktopBridge();
  const snapshot = await preview.command<DesktopSnapshot>("app.getSnapshot");
  preview.dispose();
  snapshot.settings.appLanguage = "vi";
  snapshot.privacy.allowRemoteTextProcessing = true;
  snapshot.realtime.canStartWindow = true;
  const command = vi.fn().mockResolvedValue([{ id: "opaque-id", title: "Document", processName: "editor" }]);
  const run = vi.fn(async (_label, task) => task());
  const app = { snapshot, bridge: { command }, run, quickText: "", sourceLanguage: "auto", targetLanguage: "vi" } as unknown as DesktopApp;
  render(<WorkspacePage app={app} navigate={vi.fn()} />);
  const appButton = screen.getByRole("button", { name: "Dịch theo app" });
  expect(appButton.closest(".live-actions")).not.toBeNull();
  fireEvent.click(appButton);
  const picker = await screen.findByRole("radio", { name: "editor Document" });
  expect(screen.getByRole("button", { name: "Bắt đầu" })).toBeDisabled();
  fireEvent.click(picker);
  fireEvent.click(screen.getByRole("button", { name: "Làm mới" }));
  await waitFor(() => expect(screen.getByRole("radio")).not.toBeChecked());
  fireEvent.click(screen.getByRole("radio"));
  fireEvent.click(screen.getByRole("button", { name: "Bắt đầu" }));
  await waitFor(() => expect(command).toHaveBeenCalledWith("realtime.startWindow", { windowId: "opaque-id" }));
});

it("shows actionable realtime and text errors, and clears them on a new attempt", async () => {
  const bridge = new DesktopBridge();
  const snapshot = await bridge.command<DesktopSnapshot>("app.getSnapshot");
  bridge.dispose();
  snapshot.settings.appLanguage = "vi";
  snapshot.realtime.status = "faulted";
  snapshot.realtime.errorCode = "translation-quota-exceeded";
  snapshot.quickTranslationErrorCode = "translation-network-unavailable";
  const app = { snapshot, quickText: "hello", sourceLanguage: "auto", targetLanguage: "vi", run: vi.fn() } as unknown as DesktopApp;
  const view = render(<WorkspacePage app={app} navigate={vi.fn()} />);
  expect(screen.getByText(translationErrorMessage("translation-quota-exceeded", true))).toBeInTheDocument();
  expect(screen.getByText(translationErrorMessage("translation-network-unavailable", true))).toBeInTheDocument();
  snapshot.realtime.status = "starting";
  snapshot.realtime.errorCode = undefined;
  snapshot.quickTranslationErrorCode = undefined;
  view.rerender(<WorkspacePage app={app} navigate={vi.fn()} />);
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
});

it("localizes known errors and safely explains unknown errors", () => {
  for (const code of ["translation-network-unavailable", "translation-timeout", "translation-quota-exceeded",
    "translation-access-denied", "translation-consent-required", "translation-service-unavailable", "capture-failed", "ocr-failed"]) {
    expect(translationErrorMessage(code, true)).not.toBe(translationErrorMessage(code, false));
    expect(translationErrorMessage(code, false)).not.toContain("unknown");
  }
  expect(translationErrorMessage("private exception", false)).toContain("unknown");
  expect(translationErrorMessage("private exception", true)).not.toContain("private exception");
});

it("prioritizes screen features and saves only the changed OCR setting", async () => {
  const bridge = new DesktopBridge();
  const snapshot = await bridge.command<DesktopSnapshot>("app.getSnapshot"); bridge.dispose();
  snapshot.settings.appLanguage = "en";
  const before = structuredClone(snapshot.settings);
  const command = vi.fn().mockResolvedValue(null);
  const run = vi.fn(async (_label, action) => action());
  const app = { snapshot, bridge: { command }, run, quickText: "", sourceLanguage: "auto", targetLanguage: "vi" } as unknown as DesktopApp;
  const { container } = render(<HelpMode><WorkspacePage app={app} navigate={vi.fn()} /></HelpMode>);
  expect([...container.querySelectorAll("h3")].map(node => node.firstChild?.textContent)).toEqual(["Live translation", "Selected text", "Text translator"]);
  const mode = screen.getByRole("combobox", { name: "OCR mode" });
  expect(mode.querySelectorAll("option")).toHaveLength(5);
  fireEvent.change(mode, { target: { value: "comic" } });
  expect(command).toHaveBeenCalledWith("settings.update", { ...before, ocrMode: "comic" });
  expect(snapshot.settings).toEqual(before);
  fireEvent.click(screen.getByRole("button", { name: "Explanation mode" }));
  fireEvent.click(screen.getByRole("button", { name: "About OCR modes" }));
  expect(screen.getByRole("tooltip")).toHaveTextContent("Comic: speech bubbles");
});
import { HelpMode } from "../../components/HelpMode";
