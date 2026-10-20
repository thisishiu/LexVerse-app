import { useEffect, useRef, useState } from "react";
import { GuideTarget } from "./GuideTarget";
import { GuideProvider } from "./GuideProvider";
import type { WindowPreview } from "../types";

type Entry = { id: string; title: string; processName?: string };
export function WindowPicker({ vietnamese, list, start, onClose, canStart = true, previews, cancelPreviews }: {
  vietnamese: boolean; list: () => Promise<Entry[]>; start: (id: string) => Promise<unknown>; onClose: () => void; canStart?: boolean;
  previews?: (sessionId: string, ids: string[]) => Promise<WindowPreview[]>;
  cancelPreviews?: (sessionId: string) => Promise<unknown>;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const previousFocus = useRef(document.activeElement as HTMLElement | null);
  const [entries, setEntries] = useState<Entry[]>([]);
  const [selected, setSelected] = useState("");
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [images, setImages] = useState<Record<string, WindowPreview>>({});
  const grid = useRef<HTMLDivElement>(null);
  const previewApi = useRef({ previews, cancelPreviews });
  previewApi.current = { previews, cancelPreviews };
  const previewSession = useRef<string | undefined>(undefined);
  const previewGeneration = useRef(0);
  function stopPreviews() {
    previewGeneration.current++;
    if (previewSession.current) void previewApi.current.cancelPreviews?.(previewSession.current).catch(() => {});
    previewSession.current = undefined;
  }
  const t = (en: string, vi: string) => vietnamese ? vi : en;
  const request = useRef(0);
  async function refresh() {
    const version = ++request.current;
    stopPreviews(); setImages({}); setEntries([]);
    setBusy(true); setSelected(""); setError("");
    try { const next = await list(); if (version === request.current) setEntries(next); }
    catch { if (version === request.current) setError(t("Could not load windows. Refresh to retry.", "Không tải được cửa sổ. Hãy làm mới để thử lại.")); }
    finally { if (version === request.current) setBusy(false); }
  }
  useEffect(() => {
    dialog.current?.showModal();
    void refresh();
    const element = dialog.current;
    return () => { request.current++; stopPreviews(); element?.close(); previousFocus.current?.focus(); };
  }, []);
  async function begin() {
    stopPreviews();
    const version = request.current;
    setBusy(true); setError("");
    try { await start(selected); if (version === request.current) onClose(); }
    catch { if (version === request.current) setError(t("Could not start translation. Refresh the list or try again.", "Không bắt đầu dịch được. Hãy làm mới danh sách hoặc thử lại.")); }
    finally { if (version === request.current) setBusy(false); }
  }
  const filtered = entries.filter(entry => `${entry.processName ?? ""} ${entry.title}`.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  const filteredKey = filtered.map(entry => entry.id).join(",");
  useEffect(() => {
    if (!entries.length || !previewApi.current.previews) return;
    const sessionId = crypto.randomUUID();
    previewSession.current = sessionId;
    const generation = ++previewGeneration.current;
    let timer: ReturnType<typeof setTimeout>;
    let disposed = false;
    async function update() {
      if (disposed || generation !== previewGeneration.current) return;
      const bounds = grid.current?.getBoundingClientRect();
      const visible = Array.from(grid.current?.querySelectorAll<HTMLElement>("[data-window-id]") ?? []).filter(element => {
        const rect = element.getBoundingClientRect();
        return !bounds || (rect.bottom >= bounds.top && rect.top <= bounds.bottom);
      }).map(element => element.dataset.windowId!).slice(0, 12);
      try {
        const next = visible.length ? await previewApi.current.previews!(sessionId, visible) : [];
        if (!disposed && generation === previewGeneration.current) setImages(previous => ({ ...previous, ...Object.fromEntries(next.map(item => [item.id, item])) }));
      } catch {
        if (!disposed && generation === previewGeneration.current) setImages(previous => ({ ...previous, ...Object.fromEntries(visible.map(id => [id, { id, status: "unavailable" } satisfies WindowPreview])) }));
      } finally { if (!disposed && generation === previewGeneration.current) timer = setTimeout(() => void update(), 2000); }
    }
    void update();
    return () => { disposed = true; clearTimeout(timer); stopPreviews(); };
  }, [entries, filteredKey]);
  return <dialog ref={dialog} className="window-picker-dialog" aria-labelledby="window-picker-title"
    onCancel={event => { event.preventDefault(); onClose(); }}
    onKeyDown={event => { event.stopPropagation(); if (event.key === "Escape") { event.preventDefault(); onClose(); } }}>
    <GuideProvider vietnamese={vietnamese}>
    <header><h2 id="window-picker-title">{t("Translate app", "Dịch theo app")} <GuideTarget label={t("Window selection help", "Hướng dẫn chọn cửa sổ")}>{t("Search by app or window title, select one result, then press Start. Refresh if a window has opened or closed.", "Tìm theo tên app hoặc tiêu đề cửa sổ, chọn một kết quả rồi bấm Bắt đầu. Làm mới nếu có cửa sổ vừa mở hoặc đóng.")}</GuideTarget></h2><p>{t("Choose an open window to translate.", "Chọn cửa sổ đang mở để dịch.")}</p></header>
    <div className="field-stack"><span><label htmlFor="window-search">{t("Search apps or windows", "Tìm app hoặc cửa sổ")}</label> <GuideTarget label={t("Search help", "Hướng dẫn tìm kiếm")}>{t("Type part of an app name or window title to filter the list.", "Nhập một phần tên app hoặc tiêu đề cửa sổ để lọc danh sách.")}</GuideTarget></span><input id="window-search" autoFocus value={query} onChange={event => { setQuery(event.target.value); setSelected(""); }} /></div>
    {error && <p className="inline-notice notice-warning" role="alert">{error}</p>}
    <div ref={grid} className="window-picker-list window-preview-grid" role="radiogroup" aria-label={t("Open window", "Cửa sổ đang mở")} aria-busy={busy}>
      {busy ? <p role="status">{t("Loading…", "Đang xử lý…")}</p> : filtered.map(entry => <div className="window-choice-row" data-window-id={entry.id} key={entry.id}><label className={`window-choice ${selected === entry.id ? "window-choice-selected" : ""}`}>
        <div className="window-preview">{images[entry.id]?.imageDataUrl
          ? <img src={images[entry.id].imageDataUrl} alt={t(`Preview: ${entry.title}`, `Xem trước: ${entry.title}`)} />
          : <span>{images[entry.id]?.status === "closed" ? t("Window closed", "Cửa sổ đã đóng") : !images[entry.id] && previews ? t("Loading preview…", "Đang tải ảnh xem trước…") : t("No preview available", "Không có ảnh xem trước")}</span>}</div>
        <input type="radio" name="translation-window" disabled={images[entry.id]?.status === "closed"} aria-label={`${entry.processName || t("Application", "Ứng dụng")} ${entry.title}`} value={entry.id} checked={selected === entry.id} onChange={() => setSelected(entry.id)} />
        <span><strong>{entry.processName || t("Application", "Ứng dụng")}</strong><small>{entry.title}</small></span>
      </label><GuideTarget id={`window-${entry.id}`} label={`${t("Window help", "Hướng dẫn cửa sổ")}: ${entry.title}`}>{t(`Select this window to translate ${entry.processName || entry.title}. The title helps distinguish multiple windows of the same app.`, `Chọn cửa sổ này để dịch ${entry.processName || entry.title}. Tiêu đề giúp phân biệt các cửa sổ của cùng một app.`)}</GuideTarget></div>)}
      {!busy && !filtered.length && <p>{t("No matching windows. Refresh or change your search.", "Không có cửa sổ phù hợp. Hãy làm mới hoặc đổi từ khóa.")}</p>}
    </div>
    <footer className="button-row"><span className="action-with-help"><button type="button" className="button button-secondary" disabled={busy} onClick={() => void refresh()}>{t("Refresh", "Làm mới")}</button><GuideTarget label={t("Refresh help", "Hướng dẫn làm mới")}>{t("Reload open windows. Choose a window again after refreshing.", "Tải lại các cửa sổ đang mở. Chọn lại cửa sổ sau khi làm mới.")}</GuideTarget></span>
      <span className="action-with-help"><button type="button" className="button button-quiet" onClick={onClose}>{t("Cancel", "Hủy")}</button><GuideTarget label={t("Cancel help", "Hướng dẫn hủy")}>{t("Close this picker without starting a new translation.", "Đóng hộp chọn mà không bắt đầu phiên dịch mới.")}</GuideTarget></span>
      <span className="action-with-help"><button type="button" className="button button-primary" disabled={busy || !canStart || images[selected]?.status === "closed" || !entries.some(entry => entry.id === selected)} onClick={() => void begin()}>{t("Start", "Bắt đầu")}</button><GuideTarget label={t("Start help", "Hướng dẫn bắt đầu")}>{t("Start translating the selected window. Enable external text processing in Settings first.", "Bắt đầu dịch cửa sổ đã chọn. Cần bật xử lý văn bản bên ngoài trong Cài đặt trước.")}</GuideTarget></span></footer>
    </GuideProvider>
  </dialog>;
}
