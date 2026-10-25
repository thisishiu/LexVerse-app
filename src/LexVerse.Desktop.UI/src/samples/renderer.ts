import { apps, centerInside, terms, type AppId, type Bounds, type TextBlock } from "./content";

export interface RenderStyle { opacity?: number; fontScale?: number; dark?: boolean; closeOnOutsideClick?: boolean; allowDragging?: boolean }
export interface SampleRequest { operation: string; kind?: string; bounds?: Bounds; text?: string; style?: RenderStyle }
export interface SampleMessage { request: SampleRequest; viewportBounds?: Bounds | null }
export interface SampleResponse { ok?: boolean; error?: string; text?: string | null; count?: number }

declare global { interface Window { sampleCommand: (message: SampleMessage) => SampleResponse } }

const normalized = (text: string) => text.replace(/\s+/g, " ").trim();
export function findTranslation(appId: AppId, text: string): TextBlock | undefined {
  const blocks = apps[appId].pages.flatMap(page => page.blocks);
  const exact = blocks.find(block => normalized(block.source) === normalized(text));
  if (exact) return exact;
  if (appId === "letters") {
    const parts: Record<string, string> = {
      "천천히 가도 괜찮아요.": "Đi chậm cũng không sao.",
      "중요한 건 멈추지 않는 거예요.": "Điều quan trọng là đừng dừng lại."
    };
    if (parts[normalized(text)]) return { id: "letter-one-slow", source: text, translation: parts[normalized(text)] };
  }
  return undefined;
}

export class SampleRenderer {
  private running = false;
  private kind = "window";
  private selectedIds = new Set<string>();
  private pending = new Map<string, ReturnType<typeof setTimeout>>();
  private style: RenderStyle = {};
  private selection: { text: string; rect: DOMRect } | null = null;
  private generation = 0;

  constructor(private appId: AppId) {
    document.addEventListener("selectionchange", this.rememberSelection);
    document.addEventListener("pointerdown", this.outsideClick);
    window.addEventListener("resize", this.onLayout);
    document.addEventListener("scroll", this.onLayout, true);
  }

  private blocks = () => Array.from(document.querySelectorAll<HTMLElement>(".source-block"));
  private data = (id: string) => apps[this.appId].pages.flatMap(page => page.blocks).find(block => block.id === id);
  private rememberSelection = () => {
    const selection = window.getSelection();
    if (!selection?.rangeCount || !selection.toString().trim()) { this.selection = null; return; }
    const node = selection.anchorNode instanceof Element ? selection.anchorNode : selection.anchorNode?.parentElement;
    if (!node?.closest(".source-text")) { this.selection = null; return; }
    this.selection = { text: normalized(selection.toString()), rect: selection.getRangeAt(0).getBoundingClientRect() };
  };
  private outsideClick = (event: PointerEvent) => {
    if (this.style.closeOnOutsideClick !== false && !(event.target as HTMLElement).closest(".reading-popup")) this.closePopup();
  };
  private onLayout = () => { this.closePopup(); if (this.running) this.refresh(); };

  command = ({ request, viewportBounds }: SampleMessage): SampleResponse => {
    switch (request.operation) {
      case "ping": return { ok: true };
      case "start": {
        this.clear();
        this.style = request.style ?? {};
        this.kind = request.kind ?? "window";
        const visible = { x: 0, y: 0, width: innerWidth, height: innerHeight };
        this.selectedIds = new Set(this.blocks().filter(element => centerInside(element.getBoundingClientRect(), visible)
          && (this.kind !== "region" || (viewportBounds && centerInside(element.getBoundingClientRect(), viewportBounds))))
          .map(element => element.dataset.block!));
        if (!this.selectedIds.size) return { error: "Không có khối nội dung nào trong vùng đã chọn. Hãy chọn vùng chứa đoạn văn." };
        this.running = true;
        this.refresh();
        return { ok: true, count: this.selectedIds.size };
      }
      case "refresh": this.refresh(); return { ok: true };
      case "stop": this.running = false; this.cancelPending(); return { ok: true };
      case "clear": this.clear(); return { ok: true };
      case "quick": return { text: findTranslation(this.appId, request.text ?? "")?.translation ?? null };
      case "popup": {
        this.style = request.style ?? {};
        if (!this.selection) return { text: null };
        const translation = findTranslation(this.appId, this.selection.text);
        if (!translation) return { error: "Đoạn được chọn chưa có bản dịch trong ca quay. Hãy chọn trọn một câu hoặc đoạn có sẵn." };
        this.openPopup(translation, this.selection.rect);
        return { text: translation.translation };
      }
      default: return { error: "Unsupported operation." };
    }
  };

  private refresh() {
    if (!this.running) return;
    let index = 0;
    for (const element of this.blocks()) {
      const id = element.dataset.block!;
      if (this.kind === "region" && !this.selectedIds.has(id)) continue;
      const rect = element.getBoundingClientRect();
      if (rect.bottom <= 55 || rect.top >= innerHeight || element.querySelector(".reading-translation") || this.pending.has(id)) continue;
      const block = this.data(id);
      if (!block) continue;
      const generation = this.generation;
      const timer = setTimeout(() => {
        this.pending.delete(id);
        if (!this.running || generation !== this.generation || !element.isConnected) return;
        const box = document.createElement("div");
        box.className = "reading-translation";
        box.textContent = block.translation;
        box.style.setProperty("--box-opacity", String(this.style.opacity ?? .96));
        box.style.setProperty("--font-scale", String(this.style.fontScale ?? 1));
        if (this.style.dark === false) box.classList.add("light");
        element.append(box);
        element.classList.add("has-translation");
      }, index++ * 260);
      this.pending.set(id, timer);
    }
  }

  pageChanged() {
    this.cancelPending(); this.closePopup(); this.selection = null;
    if (this.running) this.refresh();
  }

  private cancelPending() { this.generation++; for (const timer of this.pending.values()) clearTimeout(timer); this.pending.clear(); }
  private clear() {
    this.running = false; this.cancelPending(); this.closePopup();
    document.querySelectorAll(".reading-translation").forEach(element => element.remove());
    this.blocks().forEach(element => element.classList.remove("has-translation"));
  }
  private closePopup() { document.querySelector(".reading-popup")?.remove(); }

  private openPopup(block: TextBlock, rect: DOMRect) {
    this.closePopup();
    const popup = document.createElement("section");
    popup.className = "reading-popup";
    popup.setAttribute("role", "dialog"); popup.setAttribute("aria-label", "Bản dịch");
    const header = document.createElement("header");
    const label = document.createElement("span");
    label.textContent = `${apps[this.appId].sourceLanguage.toUpperCase()} → ${apps[this.appId].targetLanguage.toUpperCase()}`;
    const close = document.createElement("button"); close.textContent = "×"; close.setAttribute("aria-label", "Đóng bản dịch");
    close.onclick = () => this.closePopup();
    header.append(label, close);
    const body = document.createElement("p"); body.textContent = block.translation;
    popup.append(header, body);
    if (terms[block.id]) {
      const vocabulary = document.createElement("div"); vocabulary.className = "reading-vocabulary";
      const caption = document.createElement("small"); caption.textContent = "TỪ VỰNG"; vocabulary.append(caption);
      for (const [word, meaning] of terms[block.id]) {
        const line = document.createElement("p"); const strong = document.createElement("b"); strong.textContent = word;
        line.append(strong, ` ${meaning}`); vocabulary.append(line);
      }
      popup.append(vocabulary);
    }
    document.body.append(popup);
    const width = popup.offsetWidth, height = popup.offsetHeight;
    popup.style.left = `${Math.max(15, Math.min(innerWidth - width - 15, rect.left))}px`;
    popup.style.top = `${Math.max(65, Math.min(innerHeight - height - 15, rect.bottom + 14))}px`;
    let dragging: { x: number; y: number } | null = null;
    header.onpointerdown = event => {
      if (this.style.allowDragging === false || (event.target as HTMLElement).closest("button")) return;
      dragging = { x: event.clientX - popup.offsetLeft, y: event.clientY - popup.offsetTop };
      header.setPointerCapture(event.pointerId);
    };
    header.onpointermove = event => {
      if (!dragging) return;
      popup.style.left = `${Math.max(0, Math.min(innerWidth - popup.offsetWidth, event.clientX - dragging.x))}px`;
      popup.style.top = `${Math.max(0, Math.min(innerHeight - popup.offsetHeight, event.clientY - dragging.y))}px`;
    };
    header.onpointerup = header.onlostpointercapture = () => { dragging = null; };
  }

  dispose() {
    this.clear();
    document.removeEventListener("selectionchange", this.rememberSelection);
    document.removeEventListener("pointerdown", this.outsideClick);
    window.removeEventListener("resize", this.onLayout);
    document.removeEventListener("scroll", this.onLayout, true);
  }
}
