import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { StartupIntro } from "./StartupIntro";

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(HTMLMediaElement.prototype, "play").mockResolvedValue();
  vi.spyOn(HTMLMediaElement.prototype, "pause").mockImplementation(() => {});
});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.useRealTimers();
});

it("retains the ended video until ready, holds the last frame, then fades", () => {
  const onComplete = vi.fn();
  const view = render(<StartupIntro ready={false} onComplete={onComplete} />);
  const video = view.container.querySelector("video")!;
  fireEvent.ended(video);
  act(() => vi.advanceTimersByTime(2000));
  expect(onComplete).not.toHaveBeenCalled();
  expect(video).toHaveAttribute("src", "/videos/lexverse_intro.mp4");

  view.rerender(<StartupIntro ready onComplete={onComplete} />);
  act(() => vi.advanceTimersByTime(700));
  expect(screen.getByRole("status")).toHaveClass("startup-intro-fading");
  expect(onComplete).not.toHaveBeenCalled();
  act(() => vi.advanceTimersByTime(800));
  expect(onComplete).toHaveBeenCalledOnce();
});

it("continues into the app if the video cannot load", () => {
  const onComplete = vi.fn();
  const view = render(<StartupIntro ready onComplete={onComplete} />);
  fireEvent.error(view.container.querySelector("video")!);
  act(() => vi.advanceTimersByTime(700));
  act(() => vi.advanceTimersByTime(800));
  expect(onComplete).toHaveBeenCalledOnce();
});

it("cancels mouse interactions without ending the intro or bubbling to the app", () => {
  const onComplete = vi.fn();
  const onClick = vi.fn();
  const view = render(<div onClick={onClick}><StartupIntro ready onComplete={onComplete} /></div>);
  const surface = screen.getByRole("status");
  for (const type of ["pointerdown", "pointerup", "mousedown", "mouseup", "click", "dblclick", "auxclick", "contextmenu", "dragstart", "wheel"]) {
    const event = new Event(type, { bubbles: true, cancelable: true });
    fireEvent(surface, event);
    expect(event.defaultPrevented).toBe(true);
  }
  expect(onClick).not.toHaveBeenCalled();
  expect(onComplete).not.toHaveBeenCalled();
  expect(view.container.querySelector("video")).not.toHaveAttribute("controls");
});
