import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { App } from "./App";

afterEach(() => {
  cleanup();
  window.history.replaceState(null, "", "/");
});

describe("desktop application shell", () => {
  it("navigates between the operational routes", async () => {
    window.history.replaceState(null, "", "/?preview=ready#/workspace");
    render(<App />);

    expect(screen.getByRole("heading", { name: "Translate", level: 1 })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Account" }));
    expect(screen.getByRole("heading", { name: "Account", level: 1 })).toBeInTheDocument();
    expect(await screen.findByText("LexVerse Pro - monthly")).toBeInTheDocument();

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
