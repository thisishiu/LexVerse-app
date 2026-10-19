import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { DesktopBridge } from "../../bridge";
import type { DesktopApp } from "../../app/useDesktopApp";
import type { DesktopSnapshot } from "../../types";
import { WorkspacePage } from "./WorkspaceScreen";
import { translationErrorMessage } from "../../translationErrors";

afterEach(cleanup);

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
