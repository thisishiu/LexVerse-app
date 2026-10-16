import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import { HelpTip } from "./HelpTip";
afterEach(cleanup);
it("opens with keyboard focus, describes its trigger and closes with Escape", () => {
 render(<HelpTip label="Thông tin">Nội dung trợ giúp</HelpTip>);
 const button = screen.getByRole("button", { name: "Thông tin" });
 fireEvent.focus(button);
 expect(button).toHaveAttribute("aria-describedby", screen.getByRole("tooltip").id);
 fireEvent.keyDown(button, { key: "Escape" });
 expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
 expect(button).not.toHaveAttribute("aria-describedby");
});
it("opens on hover and toggles on touch without focus swallowing the first tap", () => {
 render(<HelpTip label="Help">Details</HelpTip>);
 const button = screen.getByRole("button", { name: "Help" });
 fireEvent.pointerEnter(button.parentElement!, { pointerType: "mouse" });
 expect(screen.getByRole("tooltip")).toHaveTextContent("Details");
 fireEvent.pointerLeave(button.parentElement!, { pointerType: "mouse" });
 expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
 fireEvent.pointerDown(button, { pointerType: "touch" });
 fireEvent.focus(button);
 fireEvent.click(button, { detail: 1 });
 expect(screen.getByRole("tooltip")).toBeInTheDocument();
 fireEvent.click(button, { detail: 1 });
 expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
});
