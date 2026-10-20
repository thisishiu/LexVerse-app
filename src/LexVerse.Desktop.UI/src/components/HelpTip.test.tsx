import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { HelpTip } from "./HelpTip";
import { HelpMode, helpProps } from "./HelpMode";
import { helpId } from "./helpText";
afterEach(cleanup);
it("has one info button and explains disabled controls without running actions", () => {
 const action = vi.fn();
 const { container } = render(<HelpMode vietnamese><div className="action-with-help"><button disabled onClick={action}>Run</button><HelpTip label="Thông tin">Nội dung trợ giúp</HelpTip></div></HelpMode>);
 expect(screen.queryByRole("button", { name: "Thông tin" })).not.toBeInTheDocument();
 const toggle = screen.getByRole("button", { name: "Chế độ giải thích" });
 fireEvent.click(toggle);
 expect(container.querySelector("[data-help-id]")).toHaveProperty("inert", true);
 const target = screen.getByRole("button", { name: "Thông tin" });
 fireEvent.click(target);
 expect(target).toHaveAttribute("aria-describedby", screen.getByRole("tooltip").id);
 expect(screen.getByRole("tooltip")).toHaveTextContent("Nội dung trợ giúp");
 expect(action).not.toHaveBeenCalled();
 fireEvent.keyDown(target, { key: "Escape" });
 expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
 expect(toggle).toHaveAttribute("aria-pressed", "false");
 expect(container.querySelector("[data-help-id]")).toHaveProperty("inert", false);
});
it("shows one note, blocks shortcuts, closes outside and toggles off", () => {
 render(<HelpMode><div {...helpProps("first", "First", "One")}><input /></div><div {...helpProps("second", "Second", "Two")} /></HelpMode>);
 const toggle = screen.getByRole("button", { name: "Explanation mode" });
 fireEvent.click(toggle);
 fireEvent.click(screen.getByRole("button", { name: "First" }));
 fireEvent.click(screen.getByRole("button", { name: "Second" }));
 expect(screen.getAllByRole("tooltip")).toHaveLength(1);
 expect(screen.getByRole("tooltip")).toHaveTextContent("Two");
 const shortcut = vi.fn(); window.addEventListener("keydown", shortcut);
 fireEvent.keyDown(document.body, { key: "F6" });
 expect(shortcut).not.toHaveBeenCalled();
 window.removeEventListener("keydown", shortcut);
 fireEvent.pointerDown(document.body);
 expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
 fireEvent.click(toggle);
 expect(toggle).toHaveAttribute("aria-pressed", "false");
});

it("uses language independent IDs and clears mode when its page unmounts", () => {
 expect(helpId("External text processing")).toBe(helpId("Xử lý văn bản bên ngoài"));
 const view = render(<HelpMode><div {...helpProps("one", "One", "Details")} /></HelpMode>);
 fireEvent.click(screen.getByRole("button", { name: "Explanation mode" }));
 view.unmount();
 expect(document.documentElement).not.toHaveAttribute("data-help-active");
});

it("suspends the page button while a modal is open", async () => {
 render(<HelpMode><dialog open><HelpMode><p>Modal</p></HelpMode></dialog></HelpMode>);
 await waitFor(() => expect(screen.getAllByRole("button", { name: "Explanation mode" })).toHaveLength(1));
});
