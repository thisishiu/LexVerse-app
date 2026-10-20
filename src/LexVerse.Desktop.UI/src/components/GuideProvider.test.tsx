import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useState } from "react";
import { GuideProvider, guideProps } from "./GuideProvider";
import { GuideTarget } from "./GuideTarget";
import type { AppRoute } from "../app/route-types";

afterEach(cleanup);
it("waits for confirmed consent across routes, then demonstrates without performing actions", async () => {
  let confirmConsent: () => void = () => {};
  const translate = vi.fn(), consent = vi.fn(() => new Promise<void>(resolve => { confirmConsent = resolve; })), notify = vi.fn();
  function App() {
    const [route, setRoute] = useState<AppRoute>("workspace");
    const [allowed, setAllowed] = useState(false);
    return <GuideProvider route={route} allowed={allowed} onActiveChange={notify} error="consent-declined" shortcuts={{ popup: "Alt+P" }}>
      <button data-help-id="nav-settings" onClick={() => setRoute("settings")}>Settings</button>
      <button data-help-id="nav-workspace" onClick={() => setRoute("workspace")}>Translate page</button>
      {route === "settings" ? <div className="setting-row"><input type="checkbox" aria-label="Consent" checked={allowed} onChange={() => { void consent().then(() => setAllowed(true)); }}/><GuideTarget label="External text processing"/></div> : <>
        <div className="language-field"><GuideTarget label="From"/><select aria-label="From"><option>English</option></select></div>
        <div className="heading-with-help"><GuideTarget label="Text translator"/><button onClick={translate}>Translate now</button></div>
        <div className="heading-with-help"><GuideTarget label="Selected text"/></div>
      </>}
    </GuideProvider>;
  }
  render(<App/>);
  expect(screen.queryByRole("dialog")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "User guide" }));
  expect(screen.getByRole("dialog")).toHaveAccessibleName("Open Settings");
  fireEvent.click(screen.getByRole("button", { name: "Settings" }));
  expect(screen.getByRole("dialog")).toHaveAccessibleName("Allow translation");
  fireEvent.click(screen.getByRole("checkbox", { name: "Consent" }));
  expect(consent).toHaveBeenCalledOnce();
  expect(screen.getByRole("dialog")).toHaveAccessibleName("Allow translation");
  await act(async () => { confirmConsent(); });
  expect(screen.getByRole("dialog")).toHaveAccessibleName("Back to Translate");
  fireEvent.click(screen.getByRole("button", { name: "Translate page" }));
  expect(screen.getByRole("dialog")).toHaveAccessibleName("Choose languages");
  fireEvent.click(screen.getByRole("button", { name: "Next" }));
  fireEvent.click(screen.getByRole("button", { name: "Translate now" }));
  expect(translate).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Next" }));
  expect(screen.getByRole("dialog")).toHaveTextContent("Alt+P");
  fireEvent.keyDown(document.body, { key: "Escape" });
  expect(screen.queryByRole("dialog")).toBeNull();
  expect(document.documentElement).not.toHaveAttribute("data-help-active");
  await waitFor(() => expect(notify).toHaveBeenLastCalledWith(false));
});

it("renders safe structured Markdown and blocks both click and keyboard actions", () => {
  const action = vi.fn();
  render(<GuideProvider><div {...guideProps("one", "One", "### Purpose\n**Read this**\n<script>alert(1)</script>")}><button onClick={action}>Do work</button></div></GuideProvider>);
  const toggle = screen.getByRole("button", { name: "User guide" });
  fireEvent.click(toggle);
  fireEvent.click(screen.getByRole("button", { name: "One" }));
  expect(screen.getByRole("heading", { name: "Purpose" })).toBeInTheDocument();
  expect(screen.getByRole("dialog").querySelector("script")).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Do work" }));
  expect(action).not.toHaveBeenCalled();
  const shortcut = vi.fn(); window.addEventListener("keydown", shortcut);
  fireEvent.keyDown(document.body, { key: "F6" });
  expect(shortcut).not.toHaveBeenCalled();
  window.removeEventListener("keydown", shortcut);
  fireEvent.keyDown(document.body, { key: "Escape" });
  expect(toggle).toHaveFocus();
  expect(toggle).toHaveAttribute("aria-pressed", "false");
});
