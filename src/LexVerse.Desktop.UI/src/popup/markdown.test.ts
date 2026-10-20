import { describe, it, expect } from "vitest";
import { renderMarkdown } from "./markdown";

describe("popup Markdown", () => {
  it("preserves headings, lists, tables and code", () => {
    const html = renderMarkdown("# Title\n\n- one\n- two\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```js\nx()\n```");
    for (const tag of ["<h1>", "<ul>", "<table>", "<pre>"]) expect(html).toContain(tag);
  });
  it("renders inline, block and bracketed formulas", () => {
    expect(renderMarkdown("Value $x^2$.")).toContain('class="katex"');
    expect(renderMarkdown("$$\n\\frac{a}{b}\n$$")).toContain("katex-display");
    expect(renderMarkdown("\\[\nx^2\n\\]")).toContain("katex-display");
    expect(renderMarkdown("\\(x^2\\)")).toContain('class="katex"');
  });
  it("escapes HTML, disables links/images and rejects trusted KaTeX commands", () => {
    const html = renderMarkdown('<script>alert(1)</script>\n\n[link](https://example.com) ![img](https://example.com/a.png)\n\n$\\href{javascript:alert(1)}{x}$');
    expect(html).not.toMatch(/<script|<img|<a\s|<iframe/);
    expect(html).toContain("&lt;script&gt;");
  });
});
