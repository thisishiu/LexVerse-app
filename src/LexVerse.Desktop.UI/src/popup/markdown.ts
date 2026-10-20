import MarkdownIt from "markdown-it";
import katex from "katex";

const math = (value: string, displayMode: boolean) => katex.renderToString(value, {
  displayMode, throwOnError: false, trust: false, strict: "ignore", maxExpand: 1000, maxSize: 20,
});

const markdown = new MarkdownIt({ html: false, linkify: false, typographer: false });
// Translated content is read-only: neither links nor remote images are active.
markdown.disable(["image", "link", "autolink"]);
markdown.inline.ruler.before("escape", "math", (state, silent) => {
  const source = state.src.slice(state.pos);
  const opener = source.startsWith("\\(") ? "\\(" : source.startsWith("$$") ? "$$" : source.startsWith("$") ? "$" : null;
  if (!opener) return false;
  const closer = opener === "\\(" ? "\\)" : opener;
  const end = state.src.indexOf(closer, state.pos + opener.length);
  if (end < 0) return false;
  const value = state.src.slice(state.pos + opener.length, end);
  if (!value.trim() || (opener === "$" && (/^\s|\s$/.test(value)))) return false;
  if (!silent) {
    const token = state.push("math_inline", "", 0);
    token.content = value;
    token.meta = { display: opener === "$$" };
  }
  state.pos = end + closer.length;
  return true;
});
markdown.renderer.rules.math_inline = (tokens, index) => math(tokens[index].content, !!tokens[index].meta?.display);
markdown.block.ruler.before("fence", "math_block", (state, start, end, silent) => {
  const first = state.src.slice(state.bMarks[start] + state.tShift[start], state.eMarks[start]).trim();
  const opener = first.startsWith("$$") ? "$$" : first.startsWith("\\[") ? "\\[" : null;
  if (!opener) return false;
  const closer = opener === "$$" ? "$$" : "\\]";
  let value = first.slice(opener.length);
  let next = start + 1;
  if (value.endsWith(closer)) value = value.slice(0, -closer.length);
  else {
    let closed = false;
    for (; next < end; next++) {
      const line = state.src.slice(state.bMarks[next] + state.tShift[next], state.eMarks[next]);
      if (line.trimEnd().endsWith(closer)) {
        value += "\n" + line.trimEnd().slice(0, -closer.length);
        next++; closed = true; break;
      }
      value += "\n" + line;
    }
    if (!closed) return false;
  }
  if (silent) return true;
  const token = state.push("math_block", "", 0);
  token.content = value; token.block = true; token.map = [start, next];
  state.line = next;
  return true;
});
markdown.renderer.rules.math_block = (tokens, index) => `<div class="formula">${math(tokens[index].content, true)}</div>`;

export function renderMarkdown(value: string): string { return markdown.render(value); }
