import { createContext, useCallback, useContext, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { Info } from "lucide-react";
import "./help-mode.css";

type HelpContext = { active: boolean; exit: () => void; notifyHost?: (active: boolean) => void };
const Context = createContext<HelpContext>({ active: false, exit: () => {} });
export const useHelpMode = () => useContext(Context);

// Stable IDs and optional demo keys let future illustrated guides reuse these targets.
export function helpProps(id: string, title: string, text: string, demoKey?: string) {
  return { "data-help-id": id, "data-help-title": title, "data-help-text": text, "data-help-demo": demoKey };
}

type Target = { element: HTMLElement; id: string; title: string; text: string; left: number; top: number; width: number; height: number };
export function HelpMode({ children, vietnamese = false, onActiveChange }: { children: ReactNode; vietnamese?: boolean; onActiveChange?: (active: boolean) => void }) {
  const parent = useHelpMode();
  const notifyHost = onActiveChange ?? parent.notifyHost;
  const root = useRef<HTMLDivElement>(null);
  const toggle = useRef<HTMLButtonElement>(null);
  const note = useRef<HTMLDivElement>(null);
  const [active, setActive] = useState(false);
  const [targets, setTargets] = useState<Target[]>([]);
  const [selected, setSelected] = useState<string>();
  const [suspended, setSuspended] = useState(false);
  const [toolbarPosition, setToolbarPosition] = useState<{ right: number; bottom: number }>();
  const [position, setPosition] = useState({ left: 8, top: 8 });
  const exit = useCallback(() => { setActive(false); setSelected(undefined); }, []);
  const current = targets.find(target => target.id === selected);
  useEffect(() => {
    notifyHost?.(active);
    return () => { if (active) notifyHost?.(false); };
  }, [active, notifyHost]);

  useEffect(() => {
    const update = () => {
      const nested = Boolean(root.current?.querySelector("dialog[open]"));
      setSuspended(nested);
      if (nested) exit();
      const modal = root.current?.closest("dialog");
      if (modal?.open) {
        const rect = modal.getBoundingClientRect();
        setToolbarPosition({ right: Math.max(8, window.innerWidth - rect.right + 18), bottom: Math.max(8, window.innerHeight - rect.bottom + 14) });
      }
    };
    const observer = new MutationObserver(update);
    if (root.current) observer.observe(root.current, { subtree: true, childList: true, attributes: true, attributeFilter: ["open"] });
    const modal = root.current?.closest("dialog");
    const resize = new ResizeObserver(update);
    if (modal) { observer.observe(modal, { attributes: true, attributeFilter: ["open"] }); resize.observe(modal); }
    window.addEventListener("resize", update);
    update();
    return () => { observer.disconnect(); resize.disconnect(); window.removeEventListener("resize", update); };
  }, [exit]);

  useLayoutEffect(() => {
    if (!active || suspended || !root.current) return;
    const scope = root.current;
    const previousFocus = document.activeElement as HTMLElement | null;
    const original = new Map<HTMLElement, boolean>();
    document.documentElement.dataset.helpActive = "true";
    const measure = () => {
      const next: Target[] = [];
      scope.querySelectorAll<HTMLElement>("[data-help-id]").forEach(element => {
        if (element.closest("[data-help-scope]") !== scope) return;
        if (!original.has(element)) { original.set(element, Boolean(element.inert)); element.inert = true; }
        const rect = element.getBoundingClientRect();
        let left = Math.max(0, rect.left), top = Math.max(0, rect.top);
        let right = Math.min(window.innerWidth, rect.right), bottom = Math.min(window.innerHeight, rect.bottom);
        for (let parent = element.parentElement; parent; parent = parent.parentElement) {
          const style = getComputedStyle(parent);
          const clip = parent.getBoundingClientRect();
          if (/(auto|scroll|hidden|clip)/.test(style.overflowX)) { left = Math.max(left, clip.left); right = Math.min(right, clip.right); }
          if (/(auto|scroll|hidden|clip)/.test(style.overflowY)) { top = Math.max(top, clip.top); bottom = Math.min(bottom, clip.bottom); }
        }
        if (right <= left || bottom <= top) return;
        next.push({ element, id: element.dataset.helpId!, title: element.dataset.helpTitle!, text: element.dataset.helpText!, left, top, width: right - left, height: bottom - top });
      });
      setTargets(next);
    };
    measure();
    const resize = new ResizeObserver(measure);
    resize.observe(scope);
    scope.querySelectorAll<HTMLElement>("[data-help-id]").forEach(element => resize.observe(element));
    const mutations = new MutationObserver(records => {
      if (records.some(record => !(record.target instanceof Element && record.target.closest(".help-target-layer, .help-mode-tools")))) measure();
    });
    mutations.observe(scope, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-help-id", "data-help-title", "data-help-text"] });
    const keydown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault(); event.stopImmediatePropagation(); exit(); toggle.current?.focus();
      } else if (event.key !== "Tab" && !(event.target instanceof Element && event.target.closest("[data-help-control], .settings-tabs, .side-nav, .sidebar-account"))) {
        event.preventDefault(); event.stopImmediatePropagation();
      }
    };
    const outside = (event: PointerEvent) => {
      if (!(event.target instanceof Element && event.target.closest("[data-help-control]"))) setSelected(undefined);
    };
    window.addEventListener("keydown", keydown, true);
    window.addEventListener("resize", measure);
    document.addEventListener("scroll", measure, true);
    document.addEventListener("pointerdown", outside, true);
    toggle.current?.focus();
    return () => {
      original.forEach((inert, element) => { element.inert = inert; });
      resize.disconnect();
      mutations.disconnect();
      delete document.documentElement.dataset.helpActive;
      window.removeEventListener("keydown", keydown, true);
      window.removeEventListener("resize", measure);
      document.removeEventListener("scroll", measure, true);
      document.removeEventListener("pointerdown", outside, true);
      setTargets([]);
      if (previousFocus?.isConnected && !previousFocus.closest("[inert]")) previousFocus.focus();
    };
  }, [active, suspended, exit]);

  useLayoutEffect(() => {
    if (!current || !note.current) return;
    const box = note.current.getBoundingClientRect();
    setPosition({ left: Math.max(8, Math.min(current.left, window.innerWidth - box.width - 8)),
      top: current.top + current.height + box.height + 16 <= window.innerHeight
        ? current.top + current.height + 8 : Math.max(8, current.top - box.height - 8) });
  }, [current]);

  const label = vietnamese ? "Chế độ giải thích" : "Explanation mode";
  return <Context.Provider value={{ active, exit, notifyHost }}><div ref={root} className={`help-scope ${active ? "help-mode-active" : ""}`} data-help-scope>
    {children}
    {!suspended && <div className="help-mode-tools" data-help-control style={toolbarPosition}>
      {active && <span className="help-mode-badge" role="status">{label} <kbd>Esc</kbd></span>}
      <button ref={toggle} type="button" className="help-mode-toggle" data-help-control aria-label={label} aria-pressed={active}
        title={label} onClick={() => { if (active) exit(); else setActive(true); }}><Info size={21} /></button>
    </div>}
    {active && !suspended && <div className="help-target-layer">
      {targets.map(target => <button key={target.id} type="button" data-help-control className="help-target" aria-label={target.title}
        aria-expanded={selected === target.id} aria-describedby={selected === target.id ? "help-mode-note" : undefined}
        style={{ left: target.left, top: target.top, width: target.width, height: target.height }}
        onClick={() => setSelected(target.id)} />)}
      {current && <div ref={note} id="help-mode-note" role="tooltip" className="help-mode-note" data-help-control style={position}>
        <strong>{current.title}</strong><p>{current.text}</p>
      </div>}
    </div>}
  </div></Context.Provider>;
}
