import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "./App";
import { DesktopBridge } from "./bridge";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
});

describe("desktop application shell", () => {
  it("keeps the app inert and notifies native only after the intro fade", async () => {
    const preview = new DesktopBridge();
    const snapshot = await preview.command("app.getSnapshot");
    preview.dispose();
    const previousChrome = window.chrome;
    let receive: (event: { data: unknown }) => void = () => {};
    const postMessage = vi.fn((message: { id: string; type: string }) => {
      queueMicrotask(() => receive({ data: {
        version: 1, id: message.id, ok: true,
        payload: message.type === "app.getSnapshot" ? snapshot : null
      } }));
    });
    Object.defineProperty(window, "chrome", { configurable: true, value: { webview: {
      postMessage,
      addEventListener: (_type: string, handler: typeof receive) => { receive = handler; },
      removeEventListener: () => {}
    } } });
    vi.useFakeTimers();
    vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
    vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
    try {
      const view = render(<App />);
      await act(async () => {});
      const shell = view.container.querySelector(".app-shell")!;
      expect(shell).toHaveAttribute("inert");
      expect(postMessage.mock.calls.map(([message]) => message.type)).toContain("app.uiReady");
      expect(postMessage.mock.calls.map(([message]) => message.type)).not.toContain("app.introCompleted");
      fireEvent.ended(view.container.querySelector("video")!);
      await act(async () => { vi.advanceTimersByTime(700); });
      await act(async () => { vi.advanceTimersByTime(799); });
      expect(shell).toHaveAttribute("inert");
      expect(postMessage.mock.calls.map(([message]) => message.type)).not.toContain("app.introCompleted");
      await act(async () => { vi.advanceTimersByTime(1); });
      expect(postMessage.mock.calls.filter(([message]) => message.type === "app.introCompleted")).toHaveLength(1);
      expect(shell).not.toHaveAttribute("inert");
      expect(view.container.querySelector("video")).toBeNull();
    } finally {
      cleanup();
      vi.restoreAllMocks();
      vi.useRealTimers();
      Object.defineProperty(window, "chrome", { configurable: true, value: previousChrome });
    }
  });

  it("navigates between the operational routes", async () => {
    window.history.replaceState(null, "", "/?preview=ready#/workspace");
    render(<App />);

    expect(screen.getByRole("heading", { name: "Translate", level: 1 })).toBeInTheDocument();

    expect(screen.getAllByRole("button", { name: "User guide" })).toHaveLength(1);
    await waitFor(() => expect(screen.getByRole("button", { name: "User guide" })).toBeEnabled());
    fireEvent.click(screen.getByRole("button", { name: "User guide" }));
    expect(screen.getByRole("button", { name: "User guide" })).toHaveAttribute("aria-pressed", "true");

    fireEvent.click(screen.getByRole("button", { name: "Close guide" }));
    fireEvent.click(screen.getByRole("button", { name: "Account" }));
    expect(screen.getByRole("heading", { name: "Account", level: 1 })).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "User guide" })).toHaveLength(1);
    expect(screen.getByRole("button", { name: "User guide" })).toHaveAttribute("aria-pressed", "false");
    expect(screen.queryByText("LexVerse Pro - monthly")).not.toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "View more plan" })).toBeDisabled();
    expect(screen.queryByText("The plans website has not been configured.")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Back to account" })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Settings" }));
    expect(screen.getByRole("heading", { name: "Application settings", level: 1 })).toBeInTheDocument();
  });

  it("opens a settings deep link without visiting the workspace first", () => {
    window.history.replaceState(null, "", "/#/settings");
    render(<App />);

    expect(screen.getByRole("heading", { name: "Application settings" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Privacy" })).toBeInTheDocument();
  });
});
