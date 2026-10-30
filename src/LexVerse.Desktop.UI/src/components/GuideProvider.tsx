import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import MarkdownIt from "markdown-it";
import { Info, X, ArrowLeft, ArrowRight } from "lucide-react";
import { GuideAnimation } from "./GuideAnimation";
import { helpId } from "./guideCatalog";
import type { AppRoute } from "../app/route-types";
import "./guide.css";

type Mode = "idle" | "tour" | "explore";
type GuideContext = { active: boolean; exit: () => void; notifyHost?: (active: boolean) => void };
const Context = createContext<GuideContext>({ active: false, exit: () => {} });
export const useGuide = () => useContext(Context);
export function guideProps(id: string, title: string, text: string, demoKey?: string) {
  return { "data-help-id": id, "data-help-title": title, "data-help-text": text, "data-help-demo": demoKey };
}
const markdown = new MarkdownIt({ html: false, linkify: false });
markdown.disable(["image", "link", "autolink"]);
type Target = { id: string; title: string; text: string; element: HTMLElement; left: number; top: number; width: number; height: number };
function visibleRect(element: HTMLElement) {
  const rect = element.getBoundingClientRect();
  let left = Math.max(0, rect.left), top = Math.max(0, rect.top), right = Math.min(innerWidth, rect.right), bottom = Math.min(innerHeight, rect.bottom);
  for (let p = element.parentElement; p; p = p.parentElement) {
    const style = getComputedStyle(p), clip = p.getBoundingClientRect();
    if (/(auto|scroll|hidden|clip)/.test(style.overflowX)) { left = Math.max(left, clip.left); right = Math.min(right, clip.right); }
    if (/(auto|scroll|hidden|clip)/.test(style.overflowY)) { top = Math.max(top, clip.top); bottom = Math.min(bottom, clip.bottom); }
  }
  return right > left && bottom > top ? { left, top, width: right-left, height: bottom-top } : undefined;
}
type Props = { children: ReactNode; vietnamese?: boolean; onActiveChange?: (active: boolean) => void;
  route?: AppRoute; allowed?: boolean; ready?: boolean; error?: string; shortcuts?: Record<string, string> };
const demos = [
  ["From", "Chọn ngôn ngữ", "Choose languages", "Chọn ngôn ngữ **Từ** và **Sang**. Dùng Tự động nhận diện nếu chưa biết ngôn ngữ gốc.", "Choose **From** and **To**. Use Auto detect when you do not know the source language."],
  ["Text translator", "Dịch văn bản", "Text translation", "Nhập hoặc dán văn bản, bấm **Dịch**, rồi **Sao chép bản dịch**. Hướng dẫn chỉ minh họa, không gửi nội dung đi dịch.", "Enter or paste text, press **Translate**, then **Copy translation**. This demonstration does not send text for translation."],
  ["Selected text", "Dịch bằng popup", "Popup translation", "Bôi đen chữ trong ứng dụng khác rồi nhấn **{popup}**. Popup hiển thị bản dịch cạnh con trỏ.", "Select text in another application and press **{popup}**. The translation popup appears near your cursor."],
  ["Translate app", "Dịch theo app", "Translate an app", "Bấm **Dịch theo app**, tìm và chọn cửa sổ rồi bấm **Bắt đầu**. Bản dịch xuất hiện trên cửa sổ được chọn.", "Press **Translate app**, find and select a window, then press **Start**. Translations appear over that window."],
  ["Region", "Dịch theo vùng", "Translate a region", "Nhấn **{region}** rồi kéo khung bao quanh vùng chữ cần dịch. Chỉ vùng đã chọn được quét.", "Press **{region}** and drag a rectangle around the text. Only the selected region is scanned."],
  ["Full screen", "Toàn màn hình và OCR", "Full screen and OCR", "Nhấn **{fullScreen}** để dịch màn hình. Chọn chế độ **OCR** phù hợp: phụ đề, hội thoại game, tài liệu hoặc truyện tranh.", "Press **{fullScreen}** to translate the screen. Choose the **OCR** mode that matches subtitles, game dialogue, documents or comics."],
  ["Stop live translation", "Dừng và xóa lớp phủ", "Stop and clear", "**{stop}** dừng phiên dịch. **{reset}** xóa các khối bản dịch đang hiện; xóa lớp phủ không đồng nghĩa dừng phiên dịch.", "**{stop}** stops the session. **{reset}** clears displayed translation blocks; clearing the overlay does not stop the session."]
];
export function GuideProvider({ children, vietnamese = false, onActiveChange, route, allowed = false, ready = true, error, shortcuts = {} }: Props) {
  const parent = useGuide();
  const notifyHost = onActiveChange ?? parent.notifyHost;
  const root = useRef<HTMLDivElement>(null), toggle = useRef<HTMLButtonElement>(null), panel = useRef<HTMLElement>(null);
  const [mode, setMode] = useState<Mode>("idle"), [step, setStep] = useState(0);
  const [targets, setTargets] = useState<Target[]>([]), [selected, setSelected] = useState<string>();
  const [tabsRect, setTabsRect] = useState<ReturnType<typeof visibleRect>>();
  const [suspended, setSuspended] = useState(false), [position, setPosition] = useState({ left: 16, top: 16 });
  const active = mode !== "idle";
  const t = (en: string, vi: string) => vietnamese ? vi : en;
  const exit = useCallback(() => { setMode("idle"); setSelected(undefined); }, []);
  const lastRoute = useRef(route);
  useEffect(() => {
    if (lastRoute.current !== route && mode === "explore") exit();
    lastRoute.current = route;
    if (mode === "tour") {
      if (step === 0 && route === "settings") setStep(allowed ? 2 : 1);
      else if (step === 1 && allowed) setStep(2);
      else if (step === 2 && route === "workspace") setStep(3);
      else if ((step === 1 && route !== "settings") || (step >= 3 && route !== "workspace")) exit();
    }
  }, [route, mode, step, allowed, exit]);
  useEffect(() => { notifyHost?.(active); return () => { if (active) notifyHost?.(false); }; }, [active, notifyHost]);
  useEffect(() => {
    const element = root.current; if (!element) return;
    const update = () => setSuspended(Boolean(element.querySelector("dialog[open]")));
    const observer = new MutationObserver(update);
    observer.observe(element, { subtree: true, childList: true, attributes: true, attributeFilter: ["open"] });
    update(); return () => observer.disconnect();
  }, []);
  useEffect(() => { if (suspended) exit(); }, [suspended, exit]);
  const wanted = step === 0 ? "nav-settings" : step === 1 ? helpId("External text processing") : step === 2 ? "nav-workspace" : helpId(demos[step - 3]?.[0] ?? "");
  const current = targets.find(item => item.id === (mode === "tour" ? wanted : selected));
  const interactive = mode === "tour" && step < 3;
  useLayoutEffect(() => {
    if (!active || suspended || !root.current) return;
    const scope = root.current;
    const measure = () => {
      const next: Target[] = [];
      scope.querySelectorAll<HTMLElement>("[data-help-id]").forEach(declaration => {
        const element = mode === "tour" && declaration.dataset.helpId === helpId("From")
          ? declaration.closest<HTMLElement>(".language-toolbar") ?? declaration
          : mode === "tour" && declaration.classList.contains("heading-with-help") ? declaration.closest<HTMLElement>(".panel") ?? declaration : declaration;
        if (element.closest("[data-help-scope]") !== scope || element.hidden || getComputedStyle(element).visibility === "hidden") return;
        const rect = visibleRect(element);
        if (!rect) return;
        next.push({ element, id: declaration.dataset.helpId!, title: declaration.dataset.helpTitle ?? "", text: declaration.dataset.helpText ?? "", ...rect });
      });
      setTargets(next);
      const tabs = scope.querySelector<HTMLElement>(".settings-tabs");
      setTabsRect(mode === "explore" && tabs ? visibleRect(tabs) : undefined);
    };
    measure();
    const resize = new ResizeObserver(measure); resize.observe(scope);
    scope.querySelectorAll<HTMLElement>("[data-help-id]").forEach(el => resize.observe(el));
    const observer = new MutationObserver(records => { if (records.some(r => !(r.target instanceof Element && r.target.closest("[data-guide-ui]")))) measure(); });
    observer.observe(scope, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-help-id", "data-help-title", "data-help-text", "hidden"] });
    window.addEventListener("resize", measure); document.addEventListener("scroll", measure, true);
    return () => { resize.disconnect(); observer.disconnect(); window.removeEventListener("resize", measure); document.removeEventListener("scroll", measure, true); };
  }, [active, suspended, route, mode]);
  useLayoutEffect(() => {
    if (mode !== "tour") return;
    const target = Array.from(root.current?.querySelectorAll<HTMLElement>("[data-help-id]") ?? []).find(el => el.dataset.helpId === wanted);
    target?.scrollIntoView?.({ block: "center", behavior: "instant" });
    document.dispatchEvent(new Event("scroll"));
  }, [mode, step, route, wanted]);
  useEffect(() => {
    if (!active || suspended) return;
    const previous = document.activeElement as HTMLElement;
    document.documentElement.dataset.helpActive = "true"; toggle.current?.focus();
    return () => { delete document.documentElement.dataset.helpActive; if (previous?.isConnected) previous.focus(); };
  }, [active, suspended]);
  useEffect(() => {
    if (!active || suspended) return;
    const permitted = (target: EventTarget | null) => target instanceof Element && (target.closest("[data-guide-ui]") ||
      (mode === "explore" && target.closest(".settings-tabs, .side-nav, .sidebar-account")) || (interactive && current?.element.contains(target)));
    const block = (event: Event) => { if (!permitted(event.target)) { event.preventDefault(); event.stopImmediatePropagation(); } };
    const key = (event: KeyboardEvent) => {
      if (event.key === "Escape") { event.preventDefault(); event.stopImmediatePropagation(); exit(); toggle.current?.focus(); return; }
      if (event.key === "Tab") {
        const candidates = Array.from(root.current?.querySelectorAll<HTMLElement>("button, input, select, textarea, [tabindex='0']") ?? []).filter(el => !el.hasAttribute("disabled") && permitted(el) && el.getBoundingClientRect().width > 0);
        const index = candidates.indexOf(document.activeElement as HTMLElement);
        event.preventDefault(); event.stopImmediatePropagation();
        candidates[(index + (event.shiftKey ? -1 : 1) + candidates.length) % candidates.length]?.focus(); return;
      }
      if (!permitted(event.target)) block(event);
    };
    const blockedEvents = ["click", "pointerdown", "contextmenu", "beforeinput", "paste", "cut", "drop", "submit"];
    blockedEvents.forEach(name => document.addEventListener(name, block, true)); window.addEventListener("keydown", key, true);
    return () => { blockedEvents.forEach(name => document.removeEventListener(name, block, true)); window.removeEventListener("keydown", key, true); };
  }, [active, suspended, mode, interactive, current, exit]);
  useLayoutEffect(() => {
    if (!panel.current) return;
    const update = () => {
      const rect = panel.current!.getBoundingClientRect();
      const margin = 16;
      let left = innerWidth - rect.width - 24, top = 70;
      if (current) {
        const right = current.left + current.width + margin;
        if (right + rect.width <= innerWidth - 12) { left = right; top = current.top; }
        else if (current.left - rect.width - margin >= 12) { left = current.left - rect.width - margin; top = current.top; }
        else { left = current.left; top = current.top + current.height + margin;
          if (top + rect.height > innerHeight - 12) top = current.top - rect.height - margin;
        }
      }
      setPosition({ left: Math.max(12, Math.min(left, innerWidth - rect.width - 12)), top: Math.max(12, Math.min(top, innerHeight - rect.height - 12)) });
    };
    update(); const observer = new ResizeObserver(update); observer.observe(panel.current); return () => observer.disconnect();
  }, [current, active, step, selected]);
  const demo = demos[step - 3];
  const title = mode === "explore" ? current?.title : step === 0 ? t("Open Settings", "Mở Cài đặt") : step === 1 ? t("Allow translation", "Cho phép dịch") : step === 2 ? t("Back to Translate", "Quay lại trang Dịch") : demo?.[vietnamese ? 1 : 2];
  const text = mode === "explore" ? current?.text : step === 0 ? t("Press **Settings** in the sidebar to enable translation.", "Bấm **Cài đặt** trên thanh bên để bật quyền dịch.") : step === 1 ? t("Enable **External text processing**. Selected, typed and OCR text can be sent to the translation service. This saves immediately. Continue only after confirmation.", "Bật **Xử lý văn bản bên ngoài**. Văn bản được chọn, nhập và nhận dạng OCR có thể được gửi tới dịch vụ dịch. Tùy chọn lưu ngay; hướng dẫn chờ xác nhận thành công.") : step === 2 ? t("Permission is enabled. Press **Translate** in the sidebar.", "Đã bật quyền. Bấm **Dịch** trên thanh bên để quay về.") : demo?.[vietnamese ? 3 : 4]?.replace(/\{(\w+)\}/g, (_, name: string) => shortcuts[name] ?? name);
  const related = step === 8 ? ["Full screen", "About OCR modes"] : step === 9 ? ["Stop live translation", "Clear overlay"] : [];
  const visibleTargets = mode === "tour" ? targets.filter(item => item.id === wanted || related.some(label => item.id === helpId(label))) : targets.filter(item => !item.id.startsWith("nav-"));
  const maskId = route ? "app-guide-mask" : "dialog-guide-mask";
  return <Context.Provider value={{ active, exit, notifyHost }}><div ref={root} className="guide-scope" data-help-scope>
    {children}
    {!suspended && <div className="guide-tools" data-guide-ui>
      {active && <span role="status">{t("Guide mode", "Chế độ hướng dẫn")} · Esc</span>}
      <button ref={toggle} className="guide-toggle" disabled={!ready} aria-label={t("User guide", "Hướng dẫn sử dụng")} aria-pressed={active} onClick={() => {
        if (active) exit(); else { setSelected(undefined); setStep(allowed ? 3 : 0); setMode(route === "workspace" ? "tour" : "explore"); }
      }}><Info size={22} /></button>
    </div>}
    {active && !suspended && <div className="guide-overlay" data-guide-ui>
      <svg className="guide-shade" width="100%" height="100%"><defs><mask id={maskId}><rect width="100%" height="100%" fill="white" />{visibleTargets.map(item => <rect key={item.id} x={item.left-4} y={item.top-4} width={item.width+8} height={item.height+8} rx="10" fill="black" />)}{tabsRect && <rect x={tabsRect.left} y={tabsRect.top} width={tabsRect.width} height={tabsRect.height} fill="black" />}</mask></defs><rect width="100%" height="100%" fill="#102a20" fillOpacity=".66" mask={`url(#${maskId})`} /></svg>
      {visibleTargets.map(item => <div key={item.id} className="guide-highlight" style={{ left: item.left-4, top: item.top-4, width: item.width+8, height: item.height+8 }}>
        {!interactive && <button className="guide-hit" aria-label={item.title} aria-expanded={selected === item.id} onClick={() => setSelected(item.id)}><span className="guide-pin"><GuideAnimation /></span></button>}
      </div>)}
      {(mode === "tour" || current) && <section ref={panel} className="guide-card" role="dialog" aria-label={title} style={position}>
        <button className="guide-close" aria-label={t("Close guide", "Đóng hướng dẫn")} onClick={mode === "tour" ? exit : () => setSelected(undefined)}><X size={18} /></button>
        <div className="guide-card-heading"><GuideAnimation demo variant={mode === "explore" ? "text" : ["settings", "consent", "settings", "languages", "text", "popup", "app", "region", "screen", "stop"][step]} /><span>{mode === "tour" ? `${step < 3 ? step+1 : step-2} / ${step < 3 ? 3 : demos.length}` : t("Discover LexVerse", "Khám phá LexVerse")}</span></div>
        <h2>{title}</h2><div className="guide-prose" dangerouslySetInnerHTML={{ __html: markdown.render(text ?? "") }} />
        {mode === "tour" && !current && <p role="status">{t("Scroll to the highlighted control, or close the guide.", "Hãy cuộn đến vùng hướng dẫn hoặc đóng hướng dẫn.")}</p>}
        {mode === "tour" && step === 1 && error && <p className="guide-error" role="alert">{t("Permission was not confirmed. Try the switch again or close the guide.", "Chưa xác nhận được quyền. Hãy thử bật lại công tắc hoặc đóng hướng dẫn.")}</p>}
        {mode === "tour" && <footer><button className="button button-quiet" onClick={exit}>{t("End", "Kết thúc")}</button>{step >= 3 && <><button className="button button-quiet" disabled={step === 3} onClick={() => setStep(s => s-1)}><ArrowLeft size={15}/>{t("Back", "Trước")}</button><button className="button button-primary" onClick={() => step === demos.length+2 ? exit() : setStep(s => s+1)}>{step === demos.length+2 ? t("Done", "Hoàn tất") : t("Next", "Tiếp")}<ArrowRight size={15}/></button></>}</footer>}
      </section>}
    </div>}
  </div></Context.Provider>;
}
