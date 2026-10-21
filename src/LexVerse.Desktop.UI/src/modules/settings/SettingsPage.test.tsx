import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { DesktopBridge } from "../../bridge";
import type { DesktopSnapshot } from "../../types";
import type { DesktopApp } from "../../app/useDesktopApp";
import { SettingsPage } from "./SettingsPage";
import { GuideProvider } from "../../components/GuideProvider";

afterEach(cleanup);
it.each([
  ["External text processing", true, true],
  ["Local performance diagnostics", false, false]
] as const)("activates %s through its visible switch", async (name, remote, diagnostics) => {
  const bridge = new DesktopBridge();
  const snapshot = structuredClone(await bridge.command<DesktopSnapshot>("app.getSnapshot"));
  bridge.dispose();
  snapshot.settings.appLanguage = "en";
  snapshot.privacy.allowRemoteTextProcessing = false;
  snapshot.privacy.allowLocalDiagnostics = true;
  const updatePrivacy = vi.fn();
  const app = { snapshot, updatePrivacy, privacySaving: false } as unknown as DesktopApp;
  const view = render(<SettingsPage app={app} navigate={vi.fn()} />);
  const checkbox = screen.getByRole("checkbox", { name });
  const switchLabel = checkbox.closest("label")!;
  expect(switchLabel).toHaveAttribute("for", checkbox.id);
  fireEvent.click(switchLabel.querySelector("span")!);
  expect(updatePrivacy).toHaveBeenCalledExactlyOnceWith(remote, diagnostics);
  updatePrivacy.mockClear();
  fireEvent.click(switchLabel);
  expect(updatePrivacy).toHaveBeenCalledExactlyOnceWith(remote, diagnostics);
  updatePrivacy.mockClear();
  fireEvent.click(screen.getByText(name, { selector: "strong" }));
  expect(updatePrivacy).toHaveBeenCalledExactlyOnceWith(remote, diagnostics);
  view.rerender(<SettingsPage app={{ ...app, privacySaving: true }} navigate={vi.fn()} />);
  updatePrivacy.mockClear();
  fireEvent.click(switchLabel.querySelector("span")!);
  expect(updatePrivacy).not.toHaveBeenCalled();
});

it("locks only privacy while saving and shows a retryable inline error", async () => {
  const bridge = new DesktopBridge();
  const snapshot = structuredClone(await bridge.command<DesktopSnapshot>("app.getSnapshot"));
  bridge.dispose();
  snapshot.settings.appLanguage = "en";
  const updatePrivacy = vi.fn();
  const app = { snapshot, updatePrivacy, privacySaving: false, busy: "Translating" } as unknown as DesktopApp;
  const view = render(<GuideProvider vietnamese={false}><SettingsPage app={app} navigate={vi.fn()} /></GuideProvider>);
  const diagnostics = screen.getByRole("checkbox", { name: "Local performance diagnostics" });
  expect(diagnostics).toBeEnabled();
  fireEvent.click(diagnostics);
  expect(updatePrivacy).toHaveBeenCalledWith(false, false);
  view.rerender(<GuideProvider vietnamese={false}><SettingsPage app={{ ...app, busy: undefined, privacySaving: true }} navigate={vi.fn()} /></GuideProvider>);
  expect(diagnostics).toBeDisabled();
  expect(screen.getByRole("checkbox", { name: "External text processing" })).toBeDisabled();
  expect(screen.getByRole("status")).toHaveTextContent("Saving privacy");
  expect(screen.getByRole("button", { name: "Translation" })).toBeEnabled();
  view.rerender(<GuideProvider vietnamese={false}><SettingsPage app={{ ...app, busy: undefined, privacyError: "operation-failed" }} navigate={vi.fn()} /></GuideProvider>);
  expect(diagnostics).toBeEnabled();
  expect(screen.getByRole("alert")).toHaveTextContent("Please try again");
});

it.each(["en", "vi"] as const)("explains settings without changing them (%s)", async language => {
  const bridge = new DesktopBridge();
  const snapshot = await bridge.command<DesktopSnapshot>("app.getSnapshot"); bridge.dispose();
  snapshot.settings.appLanguage = language;
  const run = vi.fn();
  render(<GuideProvider vietnamese={language === "vi"}><SettingsPage app={{ snapshot, run } as unknown as DesktopApp} navigate={vi.fn()} /></GuideProvider>);
  const english = language === "en";
  const toggle = screen.getByRole("checkbox", { name: english ? "External text processing" : "Xử lý văn bản bên ngoài" });
  const checked = (toggle as HTMLInputElement).checked;
  fireEvent.click(screen.getByRole("button", { name: english ? "User guide" : "Hướng dẫn sử dụng" }));
  fireEvent.click(screen.getByRole("button", { name: english ? "External text processing" : "Xử lý văn bản bên ngoài" }));
  expect(screen.getByRole("dialog")).toHaveTextContent(english ? "translation service" : "dịch vụ dịch");
  expect((toggle as HTMLInputElement).checked).toBe(checked); expect(run).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: english ? "Translation" : "Dịch thuật" }));
  expect(screen.getByRole("button", { name: english ? "User guide" : "Hướng dẫn sử dụng" })).toHaveAttribute("aria-pressed", "true");
  expect(screen.queryByRole("group", { name: /OCR/ })).not.toBeInTheDocument();
  expect(screen.getAllByRole("checkbox")).toHaveLength(3);
  expect(screen.queryByRole("checkbox", { name: /Show popup while translating|Hiện popup trong khi đang dịch/ })).not.toBeInTheDocument();
});
