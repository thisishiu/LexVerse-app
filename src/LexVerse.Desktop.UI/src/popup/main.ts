import { renderMarkdown } from "./markdown";
import { annotateTerms, type Term } from "./terms";
import "katex/dist/katex.min.css";
import "../assets/fonts.css";
import "../tokens.css";
import "./popup.css";

type Result = { markdown: string; terms: Term[]; termsLabel: string };
const content = document.getElementById("content")!;
const transport = window.chrome?.webview;
function render(result: Result) {
  content.replaceChildren();
  const translation = document.createElement("article");
  const prose = document.createElement("div"); prose.className = "translation-body";
  prose.innerHTML = renderMarkdown(result.markdown);
  translation.append(prose);
  content.append(translation);
  let section: HTMLElement | undefined;
  let trigger: HTMLButtonElement | undefined;
  const clearSelection = () => translation.querySelectorAll<HTMLButtonElement>(".term").forEach(button => {
    button.setAttribute("aria-expanded", "false");
  });
  annotateTerms(translation, result.terms, index => {
    const term = result.terms[index];
    trigger = document.activeElement instanceof HTMLButtonElement ? document.activeElement : undefined;
    clearSelection();
    translation.querySelectorAll<HTMLButtonElement>(`.term[data-term-index="${index}"]`).forEach(button => button.setAttribute("aria-expanded", "true"));
    section?.remove();
    section = document.createElement("section");
    section.className = "terms";
    section.id = "term-explanation";
    section.setAttribute("aria-labelledby", "term-title");
    const heading = document.createElement("header");
    const title = document.createElement("h2"); title.textContent = result.termsLabel;
    const close = document.createElement("button"); close.type = "button"; close.className = "term-close";
    close.textContent = "×";
    close.setAttribute("aria-label", result.termsLabel === "Thuật ngữ" ? "Đóng giải thích" : "Close explanation");
    close.addEventListener("click", () => {
      section?.remove(); section = undefined; clearSelection(); trigger?.focus(); reportHeight();
    });
    heading.append(title, close);
    const details = document.createElement("div"); details.className = "term-details";
    details.setAttribute("aria-live", "polite");
    const name = document.createElement("h3"); name.id = "term-title"; name.textContent = term.term;
    if (term.translation && term.translation !== term.term) name.append(` · ${term.translation}`);
    const explanation = document.createElement("p"); explanation.textContent = term.explanation;
    details.append(name, explanation);
    section.append(heading, details); content.append(section);
    reportHeight();
  });
  window.scrollTo(0, 0);
  reportHeight();
  void document.fonts.ready.then(() => {
    reportHeight();
    requestAnimationFrame(() => requestAnimationFrame(() => transport?.postMessage({ type: "rendered" })));
  });
}
function reportHeight() {
  const article = content.querySelector("article");
  if (!article) return;
  const section = content.querySelector<HTMLElement>(".terms");
  // Measure natural prose height, not the viewport-constrained container, to avoid resize feedback.
  const details = section?.querySelector<HTMLElement>(".term-details");
  const panelHeight = section ? (section.querySelector("header")?.getBoundingClientRect().height ?? 0) + (details?.scrollHeight ?? 0) + 24 : 0;
  const height = (article.querySelector(".translation-body")?.getBoundingClientRect().height ?? 0) + panelHeight + 40;
  transport?.postMessage({ type: "height", value: Math.ceil(height) });
}
transport?.addEventListener("message", event => {
  const result = event.data as Result;
  if (result && typeof result.markdown === "string" && Array.isArray(result.terms)) render(result);
});
new ResizeObserver(reportHeight).observe(content);
transport?.postMessage({ type: "ready" });
