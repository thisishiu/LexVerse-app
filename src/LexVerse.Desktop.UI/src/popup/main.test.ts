import { afterEach, expect, it, vi } from "vitest";

afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); delete window.chrome; });

it("renders host results, keeps terms as text, reports height and replaces the previous translation", async () => {
  document.body.innerHTML = '<main id="content"></main>';
  vi.spyOn(window, "scrollTo").mockImplementation(() => {});
  Object.defineProperty(document, "fonts", { configurable: true, value: { ready: Promise.resolve() } });
  const frames: FrameRequestCallback[] = [];
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => frames.push(callback));
  vi.stubGlobal("ResizeObserver", class { observe() {} });
  let receive: ((event: { data: unknown }) => void) | undefined;
  const postMessage = vi.fn();
  window.chrome = { webview: {
    postMessage, addEventListener: (_, handler) => { receive = handler; }, removeEventListener: vi.fn(),
  } };
  await import("./main");
  expect(postMessage).toHaveBeenCalledWith({ type: "ready" });
  receive!({ data: {
    markdown: "# Bản dịch\n\nx và Biến số.\n\n$x^2$\n\n" + "Nội dung dài.\n\n".repeat(50),
    termsLabel: "Thuật ngữ", terms: [{ term: "x", translation: "Biến số", explanation: '<img src="https://example.test" onerror="alert(1)">' }],
  } });
  expect(postMessage).not.toHaveBeenCalledWith({ type: "rendered" });
  await Promise.resolve();
  frames.shift()!(0);
  expect(postMessage).not.toHaveBeenCalledWith({ type: "rendered" });
  frames.shift()!(0);
  expect(postMessage).toHaveBeenCalledWith({ type: "rendered" });
  expect(document.querySelector("h1")).toHaveTextContent("Bản dịch");
  expect(document.querySelector(".katex")).not.toBeNull();
  expect(document.querySelector(".terms")).toBeNull();
  const term = document.querySelector<HTMLButtonElement>(".term")!;
  term.focus(); term.click();
  expect(term).toHaveAttribute("aria-expanded", "true");
  expect(document.querySelector(".terms h3")).toHaveTextContent("x · Biến số");
  expect(document.querySelector(".term-details p")).toHaveTextContent("<img");
  expect(document.querySelector("img")).toBeNull();
  document.querySelector<HTMLButtonElement>(".term-close")!.click();
  expect(document.querySelector(".terms")).toBeNull();
  expect(term).toHaveFocus();
  expect(term).toHaveAttribute("aria-expanded", "false");
  term.click();
  expect(postMessage).toHaveBeenCalledWith(expect.objectContaining({ type: "height" }));
  receive!({ data: { markdown: "Replacement", terms: [], termsLabel: "Terminology" } });
  expect(document.querySelector("article")).toHaveTextContent("Replacement");
  expect(document.querySelector(".terms")).toBeNull();
  expect(document.body).not.toHaveTextContent("Nội dung dài.");
});
