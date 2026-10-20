import { act, cleanup, render } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import lottie from "lottie-web/build/player/lottie_light";
import { GuideAnimation } from "./GuideAnimation";

afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.clearAllMocks(); });
it("uses a static Lottie frame for reduced motion and destroys the renderer on exit", async () => {
  const goToAndStop = vi.fn(), destroy = vi.fn(), play = vi.fn();
  vi.mocked(lottie.loadAnimation).mockReturnValue({ goToAndStop, destroy, play } as unknown as ReturnType<typeof lottie.loadAnimation>);
  const removeEventListener = vi.fn();
  vi.stubGlobal("matchMedia", () => ({ matches: true, addEventListener: vi.fn(), removeEventListener }));
  const view = render(<GuideAnimation demo variant="consent"/>);
  await act(async () => {});
  expect(lottie.loadAnimation).toHaveBeenCalledWith(expect.objectContaining({ autoplay: false, renderer: "svg", animationData: expect.objectContaining({ nm: "consent" }) }));
  expect(goToAndStop).toHaveBeenCalledWith(0, true);
  view.unmount();
  expect(destroy).toHaveBeenCalledOnce();
  expect(removeEventListener).toHaveBeenCalledWith("change", expect.any(Function));
});

it("keeps the information fallback if animation loading fails", async () => {
  vi.mocked(lottie.loadAnimation).mockImplementationOnce(() => { throw new Error("unavailable renderer"); });
  const view = render(<GuideAnimation/>);
  await act(async () => {});
  expect(view.container.querySelector(".guide-animation-fallback")).toHaveTextContent("i");
});
