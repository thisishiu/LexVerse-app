import "@testing-library/jest-dom/vitest";
import { vi } from "vitest";

// Layout and ResizeObserver are browser facilities absent from jsdom.
globalThis.ResizeObserver = class { observe() {} unobserve() {} disconnect() {} };
vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(() =>
  ({ left: 0, top: 0, right: 120, bottom: 40, width: 120, height: 40, x: 0, y: 0, toJSON() {} }));

// jsdom does not implement the native dialog lifecycle or modal focus handling.
HTMLDialogElement.prototype.showModal = function () { this.setAttribute("open", ""); this.querySelector<HTMLElement>("[autofocus]")?.focus(); };
HTMLDialogElement.prototype.close = function () { this.removeAttribute("open"); };
