import { expect, it, vi } from "vitest";
import { annotateTerms } from "./terms";
import { renderMarkdown } from "./markdown";

it("matches Unicode words, translated aliases and repeated phrases; prefers longer phrases", () => {
  const article = document.createElement("article");
  article.innerHTML = renderMarkdown("HỌC MÁY, học máy; máy. Supermachine machine. học **máy**!");
  const select = vi.fn();
  annotateTerms(article, [
    { term: "máy", translation: "machine", explanation: "Short" },
    { term: "machine learning", translation: "học máy", explanation: "Long" },
    { term: "Supermachine", translation: "", explanation: " " },
  ], select);
  const buttons = [...article.querySelectorAll<HTMLButtonElement>(".term")];
  expect(buttons.map(button => button.textContent)).toEqual(["HỌC MÁY", "học máy", "máy", "machine", "học ", "máy"]);
  expect(buttons.map(button => button.dataset.termIndex)).toEqual(["1", "1", "0", "0", "1", "1"]);
  expect(article.querySelector("strong .term")).not.toBeNull();
  buttons[0].click();
  expect(select).toHaveBeenCalledWith(1);
});

it("keeps code and KaTeX untouched and treats special characters literally", () => {
  const article = document.createElement("article");
  article.innerHTML = renderMarkdown("C++ and `C++`. $x$ and x.\n\n```\nx\n```\n\n$$x$$");
  annotateTerms(article, [
    { term: "C++", translation: "", explanation: "Language" },
    { term: "x", translation: "", explanation: "Variable" },
  ], vi.fn());
  expect([...article.querySelectorAll(".term")].map(button => button.textContent)).toEqual(["C++", "x"]);
  expect(article.querySelector("code .term, pre .term, .katex .term, .formula .term")).toBeNull();
});
