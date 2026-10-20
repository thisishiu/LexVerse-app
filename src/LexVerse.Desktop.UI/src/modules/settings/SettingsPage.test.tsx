import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { DesktopBridge } from "../../bridge";
import type { DesktopSnapshot } from "../../types";
import type { DesktopApp } from "../../app/useDesktopApp";
import { SettingsPage } from "./SettingsPage";
import { GuideProvider } from "../../components/GuideProvider";

afterEach(cleanup);
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
