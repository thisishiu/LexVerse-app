import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { WindowPicker } from "./WindowPicker";

afterEach(() => { cleanup(); vi.useRealTimers(); });
const entries = [{ id: "opaque", processName: "editor", title: "Document" }, { id: "other", processName: "browser", title: "Article" }];

it("shows loading, filters by app and title, and starts with the opaque ID", async () => {
  let finish!: (value: typeof entries) => void;
  const start = vi.fn().mockResolvedValue(null), close = vi.fn();
  render(<WindowPicker vietnamese={false} list={() => new Promise(resolve => { finish = resolve; })} start={start} onClose={close} />);
  expect(screen.getByRole("status")).toHaveTextContent("Loading");
  finish(entries);
  await screen.findByRole("radio", { name: "editor Document" });
  fireEvent.change(screen.getByRole("textbox"), { target: { value: "DOCUMENT" } });
  expect(screen.getAllByRole("radio")).toHaveLength(1);
  fireEvent.click(screen.getByRole("radio"));
  fireEvent.click(screen.getByRole("button", { name: "Start" }));
  await waitFor(() => expect(close).toHaveBeenCalledOnce());
  expect(start).toHaveBeenCalledWith("opaque");
});

it("recovers from list errors, shows empty searches, and keeps selection on start errors", async () => {
  const list = vi.fn().mockRejectedValueOnce(new Error("private")).mockResolvedValue(entries);
  const close = vi.fn();
  render(<WindowPicker vietnamese list={list} start={vi.fn().mockRejectedValue(new Error("gone"))} onClose={close} />);
  expect(await screen.findByRole("alert")).toHaveTextContent("Không tải được");
  fireEvent.click(screen.getByRole("button", { name: "Làm mới" }));
  await screen.findByRole("radio", { name: "editor Document" });
  fireEvent.change(screen.getByRole("textbox"), { target: { value: "absent" } });
  expect(screen.queryByRole("radio")).not.toBeInTheDocument();
  expect(screen.getByText(/Không có cửa sổ phù hợp/)).toBeInTheDocument();
  fireEvent.change(screen.getByRole("textbox"), { target: { value: "editor" } });
  fireEvent.click(screen.getByRole("radio"));
  fireEvent.click(screen.getByRole("button", { name: "Bắt đầu" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("Không bắt đầu dịch được");
  expect(close).not.toHaveBeenCalled();
  expect(screen.getByRole("radio")).toBeChecked();
});

it("closes on Escape without bubbling and restores focus", async () => {
  const trigger = document.createElement("button"); document.body.append(trigger); trigger.focus();
  const close = vi.fn(), behind = vi.fn(); window.addEventListener("keydown", behind);
  const view = render(<WindowPicker vietnamese={false} list={async () => []} start={vi.fn()} onClose={close} />);
  await waitFor(() => expect(screen.queryByRole("status")).not.toBeInTheDocument());
  fireEvent.keyDown(screen.getByRole("textbox"), { key: "Escape" });
  expect(close).toHaveBeenCalledOnce(); expect(behind).not.toHaveBeenCalled();
  view.unmount(); expect(trigger).toHaveFocus();
  window.removeEventListener("keydown", behind); trigger.remove();
});

it("updates real preview data without losing selection and cancels on close", async () => {
  const image = "data:image/jpeg;base64,first";
  const previews = vi.fn().mockResolvedValue([{ id: "opaque", status: "available", imageDataUrl: image }, { id: "other", status: "unavailable" }]);
  const cancel = vi.fn().mockResolvedValue(null);
  const view = render(<WindowPicker vietnamese={false} list={async () => entries} start={vi.fn()} onClose={vi.fn()} previews={previews} cancelPreviews={cancel} />);
  expect(await screen.findByRole("img", { name: "Preview: Document" })).toHaveAttribute("src", image);
  expect(screen.getByText("No preview available")).toBeInTheDocument();
  fireEvent.click(screen.getByRole("radio", { name: "editor Document" }));
  vi.useFakeTimers();
  // The existing first timer was scheduled with real timers; a search restarts polling.
  fireEvent.change(screen.getByRole("textbox"), { target: { value: "editor" } });
  await act(async () => {});
  fireEvent.click(screen.getByRole("radio", { name: "editor Document" }));
  const calls = previews.mock.calls.length;
  previews.mockResolvedValue([{ id: "opaque", status: "available", imageDataUrl: "data:image/jpeg;base64,next" }]);
  await act(async () => { await vi.advanceTimersByTimeAsync(2000); });
  expect(previews).toHaveBeenCalledTimes(calls + 1);
  expect(screen.getByRole("radio")).toBeChecked();
  expect(screen.getByRole("img")).toHaveAttribute("src", "data:image/jpeg;base64,next");
  expect(previews.mock.calls.at(-1)?.[1]).toEqual(["opaque"]);
  view.unmount();
  expect(cancel).toHaveBeenCalledWith(previews.mock.calls.at(-1)?.[0]);
  await act(async () => { await vi.advanceTimersByTimeAsync(4000); });
  expect(previews).toHaveBeenCalledTimes(calls + 1);
});

it("does not overlap preview batches and ignores responses after refresh", async () => {
  let finish!: (value: unknown[]) => void;
  const previews = vi.fn().mockImplementation(() => new Promise(resolve => { finish = resolve; }));
  const cancel = vi.fn().mockResolvedValue(null);
  render(<WindowPicker vietnamese={false} list={async () => entries} start={vi.fn()} onClose={vi.fn()} previews={previews} cancelPreviews={cancel} />);
  await screen.findByRole("radio", { name: "editor Document" });
  vi.useFakeTimers();
  await act(async () => { await vi.advanceTimersByTimeAsync(6000); });
  expect(previews).toHaveBeenCalledTimes(1);
  const oldFinish = finish;
  fireEvent.click(screen.getByRole("button", { name: "Refresh" }));
  await act(async () => {});
  expect(cancel).toHaveBeenCalled();
  await act(async () => { oldFinish([{ id: "opaque", status: "available", imageDataUrl: "data:image/jpeg;base64,stale" }]); });
  expect(screen.queryByRole("img")).not.toBeInTheDocument();
  expect(screen.getAllByRole("radio").every(radio => !(radio as HTMLInputElement).checked)).toBe(true);
});

it("prevents starting a closed window and lets unavailable previews remain selectable", async () => {
  render(<WindowPicker vietnamese={false} list={async () => entries} start={vi.fn()} onClose={vi.fn()}
    previews={async () => [{ id: "opaque", status: "closed" }, { id: "other", status: "unavailable" }]} />);
  await screen.findByText("Window closed");
  expect(screen.getByRole("radio", { name: "editor Document" })).toBeDisabled();
  fireEvent.click(screen.getByRole("radio", { name: "browser Article" }));
  expect(screen.getByRole("button", { name: "Start" })).toBeEnabled();
});

it("Esc leaves explanation mode before closing the modal", async () => {
  const close = vi.fn();
  render(<WindowPicker vietnamese={false} list={async () => entries} start={vi.fn()} onClose={close} />);
  await screen.findByRole("radio", { name: "editor Document" });
  const toggle = screen.getByRole("button", { name: "User guide" });
  fireEvent.click(toggle);
  fireEvent.keyDown(toggle, { key: "Escape" });
  expect(close).not.toHaveBeenCalled();
  expect(toggle).toHaveAttribute("aria-pressed", "false");
  fireEvent.keyDown(toggle, { key: "Escape" });
  expect(close).toHaveBeenCalledOnce();
});
