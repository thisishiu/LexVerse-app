export type Term = { term: string; translation: string; explanation: string };

const escapePattern = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
const word = /[\p{L}\p{N}\p{M}_]/u;

/** Match rendered prose, including phrases spanning inline Markdown formatting. */
export function annotateTerms(article: HTMLElement, terms: Term[], select: (index: number) => void) {
  const aliases = terms.flatMap((term, index) => term.explanation.trim()
    ? [...new Set([term.term.trim(), term.translation.trim()])].filter(Boolean).map(text => ({ text, index })) : [])
    .sort((a, b) => b.text.length - a.text.length);
  if (!aliases.length) return;
  const pattern = new RegExp(aliases.map(alias => `(${escapePattern(alias.text)})`).join("|"), "giu");
  const walker = document.createTreeWalker(article, NodeFilter.SHOW_TEXT);
  const groups: Text[][] = [];
  let previousBlock: Element | null = null;
  let group: Text[] | undefined;
  while (walker.nextNode()) {
    const node = walker.currentNode as Text;
    if (node.parentElement?.closest("code, pre, .katex, .formula")) { group = undefined; continue; }
    const block = node.parentElement?.closest("p, li, h1, h2, h3, h4, h5, h6, td, th, blockquote, article") ?? null;
    if (!group || block !== previousBlock) { group = []; groups.push(group); }
    group.push(node);
    previousBlock = block;
  }
  for (const nodes of groups) {
    const text = nodes.map(node => node.data).join("");
    const matches: { start: number; end: number; index: number }[] = [];
    pattern.lastIndex = 0;
    for (const match of text.matchAll(pattern)) {
      const start = match.index;
      const end = start + match[0].length;
      const before = Array.from(text.slice(0, start)).at(-1) ?? "";
      const after = Array.from(text.slice(end))[0] ?? "";
      if ((word.test(Array.from(match[0])[0]) && word.test(before)) ||
          (word.test(Array.from(match[0]).at(-1)!) && word.test(after))) continue;
      matches.push({ start, end, index: aliases[match.slice(1).findIndex(value => value !== undefined)].index });
    }
    let offset = 0;
    for (const node of nodes) {
      const fragment = document.createDocumentFragment();
      let cursor = 0;
      for (const match of matches) {
        const start = Math.max(0, match.start - offset);
        const end = Math.min(node.length, match.end - offset);
        if (start >= end) continue;
        fragment.append(node.data.slice(cursor, start));
        const button = document.createElement("button");
        button.type = "button";
        button.className = "term";
        button.dataset.termIndex = String(match.index);
        button.textContent = node.data.slice(start, end);
        button.setAttribute("aria-label", terms[match.index].term);
        button.setAttribute("aria-controls", "term-explanation");
        button.setAttribute("aria-expanded", "false");
        button.addEventListener("click", () => select(match.index));
        fragment.append(button);
        cursor = end;
      }
      offset += node.length;
      if (cursor) { fragment.append(node.data.slice(cursor)); node.replaceWith(fragment); }
    }
  }
}
