import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { apps, centerInside } from "./content";
import { findTranslation, SampleRenderer } from "./renderer";

let renderer: SampleRenderer;
function mount(id: string, y: number) {
  const element = document.createElement("div");
  element.className = "source-block"; element.dataset.block = id;
  const text = document.createElement("span"); text.className = "source-text";
  text.textContent = apps.article.pages[0].blocks.find(block => block.id === id)!.source;
  element.append(text); document.body.append(element);
  element.getBoundingClientRect = () => ({ x: 100, y, width: 500, height: 100, top: y, bottom: y + 100, left: 100, right: 600, toJSON() {} });
  return element;
}
beforeEach(() => { vi.useFakeTimers(); document.body.innerHTML = ""; renderer = new SampleRenderer("article"); });
afterEach(() => { renderer.dispose(); vi.useRealTimers(); });

describe("recordable sample content", () => {
  it("uses actual region bounds and reveals only selected source blocks", () => {
    const inside = mount("work-intro", 200), outside = mount("work-focus", 450);
    renderer.command({ request: { operation: "start", kind: "region" }, viewportBounds: { x: 80, y: 180, width: 550, height: 150 } });
    vi.runAllTimers();
    expect(inside.querySelector(".reading-translation")?.textContent).toBe(apps.article.pages[0].blocks[1].translation);
    expect(outside.querySelector(".reading-translation")).toBeNull();
  });
  it("cancels pending results on clear, stop and content changes", () => {
    mount("work-intro", 200); mount("work-focus", 350);
    renderer.command({ request: { operation: "start", kind: "window" } });
    vi.advanceTimersByTime(10);
    expect(document.querySelectorAll(".reading-translation")).toHaveLength(1);
    renderer.command({ request: { operation: "stop" } });
    vi.runAllTimers();
    expect(document.querySelectorAll(".reading-translation")).toHaveLength(1);
    renderer.command({ request: { operation: "clear" } });
    vi.runAllTimers();
    expect(document.querySelectorAll(".reading-translation")).toHaveLength(0);
    renderer.command({ request: { operation: "start", kind: "window" } });
    renderer.command({ request: { operation: "clear" } });
    renderer.pageChanged(); vi.runAllTimers();
    expect(document.querySelectorAll(".reading-translation")).toHaveLength(0);
  });
  it("does not invent a result for arbitrary selected or typed text", () => {
    expect(findTranslation("letters", "천천히 가도 괜찮아요.")?.translation).toBe("Đi chậm cũng không sao.");
    expect(findTranslation("letters", "천천히")).toBeUndefined();
    expect(renderer.command({ request: { operation: "quick", text: "Unprepared input" } }).text).toBeNull();
    expect(renderer.command({ request: { operation: "popup" } }).text).toBeNull();
  });
  it("requires a source block center within the chosen region", () => {
    expect(centerInside({ x: 0, y: 0, width: 100, height: 100 }, { x: 90, y: 90, width: 40, height: 40 })).toBe(false);
    expect(centerInside({ x: 0, y: 0, width: 100, height: 100 }, { x: 40, y: 40, width: 20, height: 20 })).toBe(true);
  });
  it("provides two real content states per independent application", () => {
    for (const app of Object.values(apps)) {
      expect(app.pages.length).toBe(2);
      expect(app.name).not.toMatch(/lexverse|demo/i);
      for (const page of app.pages) for (const block of page.blocks) {
        expect(block.source).not.toBe(block.translation);
        expect(block.source + block.translation).not.toMatch(/lexverse|demo/i);
      }
    }
  });
});
