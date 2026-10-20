import { OptionHelp } from "../../components/OptionHelp";
import { useHelpMode } from "../../components/HelpMode";
import { useEffect, useId, useMemo, useState, type KeyboardEvent } from "react";
import {
  CircleHelp,
  Gauge,
  Keyboard,
  Languages,
  MonitorCog,
  RotateCcw,
  Save,
  ScanText,
  ShieldCheck,
  SlidersHorizontal
} from "lucide-react";
import type { RouteProps } from "../../app/route-types";
import type { DesktopSettings, DesktopShortcuts } from "../../types";
import { languages } from "../workspace/WorkspaceScreen";

type SettingsTab = "general" | "translation" | "realtime" | "shortcuts";
type ShortcutName = keyof DesktopShortcuts;

const defaultShortcuts: DesktopShortcuts = {
  popup: "F6",
  region: "Ctrl+Shift+R",
  fullScreen: "Ctrl+Shift+F",
  stop: "Escape",
  reset: "Ctrl+Shift+Backspace"
};

const tabs = [
  ["general", SlidersHorizontal, "General", "Chung"],
  ["translation", Languages, "Translation", "Dịch thuật"],
  ["realtime", MonitorCog, "Realtime", "Thời gian thực"],
  ["shortcuts", Keyboard, "Shortcuts", "Phím tắt"]
] as const;

const defaultSettings: DesktopSettings = {
  appLanguage: "en",
  sourceLanguage: "auto",
  targetLanguage: "vi",
  ocrMode: "document",
  ignoredTerms: "HP, MP, EXP",
  preserveAcronymsAndTechnicalTerms: true,
  closePopupOnOutsideClick: true,
  allowPopupDragging: true,
  showOcrDebugBoxes: false,
  scanIntervalMs: 500,
  overlayOpacityPercent: 85,
  overlayFontScalePercent: 100,
  useDarkOverlayBlocks: true,
  translationInstruction: "Translate naturally and clearly. Keep proper names, technical terms, HP/MP/EXP unchanged. Do not add explanations.",
  shortcuts: defaultShortcuts
};

export function SettingsPage({ app }: RouteProps) {
  const { snapshot, bridge, busy, run } = app;
  const [activeTab, setActiveTab] = useState<SettingsTab>("general");
  const { exit: exitHelp } = useHelpMode();
  useEffect(() => { exitHelp(); }, [activeTab, exitHelp]);
  const [baseline, setBaseline] = useState<DesktopSettings>(() => cloneSettings(defaultSettings));
  const [draft, setDraft] = useState<DesktopSettings>(() => cloneSettings(defaultSettings));
  const [capturing, setCapturing] = useState<ShortcutName>();

  const serverSettings = snapshot?.settings;
  const serverFingerprint = useMemo(
    () => serverSettings ? JSON.stringify(serverSettings) : "",
    [serverSettings]
  );
  const baselineFingerprint = useMemo(
    () => baseline ? JSON.stringify(baseline) : "",
    [baseline]
  );

  useEffect(() => {
    if (!serverSettings || serverFingerprint === baselineFingerprint) return;
    setBaseline(cloneSettings(serverSettings));
    setDraft(cloneSettings(serverSettings));
    setCapturing(undefined);
  }, [serverSettings, serverFingerprint, baselineFingerprint]);

  const isVietnamese = draft?.appLanguage === "vi";
  const t = (english: string, vietnamese: string) => isVietnamese ? vietnamese : english;
  const dirty = Boolean(draft && baseline && JSON.stringify(draft) !== JSON.stringify(baseline));
  const shortcutValues = draft ? Object.values(draft.shortcuts).map((value) => value.toLowerCase()) : [];
  const hasDuplicateShortcut = new Set(shortcutValues).size !== shortcutValues.length;
  const disabled = Boolean(busy) || !draft;

  function update<K extends keyof DesktopSettings>(key: K, value: DesktopSettings[K]) {
    setDraft((current) => current ? { ...current, [key]: value } : current);
  }

  function updateShortcut(name: ShortcutName, value: string) {
    setDraft((current) => current ? {
      ...current,
      shortcuts: { ...current.shortcuts, [name]: value }
    } : current);
  }

  function captureShortcut(event: KeyboardEvent<HTMLButtonElement>, name: ShortcutName) {
    if (capturing !== name) return;
    event.preventDefault();
    event.stopPropagation();
    const shortcut = shortcutFromEvent(event);
    if (!shortcut) return;
    updateShortcut(name, shortcut);
    setCapturing(undefined);
  }

  function updatePrivacy(allowRemoteTextProcessing: boolean, allowLocalDiagnostics: boolean) {
    void run(t("Saving privacy", "Đang lưu quyền riêng tư"), () => bridge.command("privacy.updateConsent", {
      allowRemoteTextProcessing,
      allowLocalDiagnostics
    }));
  }

  function saveSettings() {
    if (!draft || hasDuplicateShortcut) return;
    void run(t("Saving settings", "Đang lưu cài đặt"), () => bridge.command("settings.update", draft));
  }

  if (!draft) {
    return <section className="page settings-page settings-loading">{t("Loading settings...", "Đang tải cài đặt...")}</section>;
  }

  return (
    <section className="page settings-page">
      <header className="page-heading settings-heading">
        <div className="settings-save-area">
          {dirty && <span className="unsaved-indicator">{t("Unsaved", "Chưa lưu")}</span>}
          <button
            type="button"
            className="button button-primary"
            disabled={disabled || !dirty || hasDuplicateShortcut}
            onClick={saveSettings}
          >
            <Save size={17} /> {busy === t("Saving settings", "Đang lưu cài đặt") ? t("Saving...", "Đang lưu...") : t("Save changes", "Lưu thay đổi")}
          </button>
          <OptionHelp label={t("Save changes", "Lưu thay đổi")} />
        </div>
      </header>

      <div className="settings-layout">
        <nav className="settings-tabs" aria-label={t("Settings sections", "Các mục cài đặt")}>
          {tabs.map(([id, Icon, english, vietnamese]) => (
            <button
              type="button"
              key={id}
              className={activeTab === id ? "settings-tab-active" : ""}
              aria-current={activeTab === id ? "page" : undefined}
              onClick={() => setActiveTab(id)}
            >
              <Icon size={18} />
              <span>{t(english, vietnamese)}</span>
            </button>
          ))}
        </nav>

        <div className="panel settings-surface">
          {activeTab === "general" && (
            <div className="settings-view">
              <SettingsTitle icon={SlidersHorizontal} title={t("General", "Cài đặt chung")} />

              <div className="settings-form-section">
                <div className="settings-section-label">{t("Display language", "Ngôn ngữ hiển thị")} <OptionHelp label={t("Display language", "Ngôn ngữ hiển thị")} /></div>
                <div className="segmented-control compact-segments" role="group" aria-label={t("Display language", "Ngôn ngữ hiển thị")}>
                  <button type="button" className={draft.appLanguage === "en" ? "segment-active" : ""} onClick={() => update("appLanguage", "en")}>English</button>
                  <button type="button" className={draft.appLanguage === "vi" ? "segment-active" : ""} onClick={() => update("appLanguage", "vi")}>Tiếng Việt</button>
                </div>
              </div>

              <div className="settings-form-section">
                <div className="settings-section-label">{t("Default language pair", "Cặp ngôn ngữ mặc định")}</div>
                <div className="default-language-grid">
                  <div className="language-field">
                    <span><label htmlFor="default-source-language">{t("Source language", "Ngôn ngữ nguồn")}</label> <OptionHelp label={t("Source language", "Ngôn ngữ nguồn")} /></span>
                    <select id="default-source-language" value={draft.sourceLanguage} onChange={(event) => update("sourceLanguage", event.target.value)}>
                      {languages.map(([value]) => <option key={value} value={value}>{languageName(value, isVietnamese)}</option>)}
                    </select>
                  </div>
                  <div className="language-field">
                    <span><label htmlFor="default-target-language">{t("Target language", "Ngôn ngữ đích")}</label> <OptionHelp label={t("Target language", "Ngôn ngữ đích")} /></span>
                    <select id="default-target-language" value={draft.targetLanguage} onChange={(event) => update("targetLanguage", event.target.value)}>
                      {languages.filter(([value]) => value !== "auto").map(([value]) => (
                        <option key={value} value={value}>{languageName(value, isVietnamese)}</option>
                      ))}
                    </select>
                  </div>
                </div>
              </div>

              <div className="settings-form-section">
                <h4 className="settings-section-label section-label-with-icon"><ShieldCheck size={15} /> {t("Privacy", "Quyền riêng tư")}</h4>
                <ToggleRow
                  label={t("External text processing", "Xử lý văn bản bên ngoài")}
                  inlineDetail
                  detail={t("Selected, typed and OCR text", "Văn bản được chọn, nhập và nhận dạng OCR")}
                  checked={snapshot?.privacy.allowRemoteTextProcessing ?? false}
                  disabled={Boolean(busy)}
                  onChange={(checked) => updatePrivacy(checked, snapshot?.privacy.allowLocalDiagnostics ?? true)}
                />
                <ToggleRow
                  label={t("Local performance diagnostics", "Chẩn đoán hiệu năng cục bộ")}
                  inlineDetail
                  detail={t("Content-free timing metrics", "Chỉ số thời gian không chứa nội dung")}
                  checked={snapshot?.privacy.allowLocalDiagnostics ?? true}
                  disabled={Boolean(busy)}
                  onChange={(checked) => updatePrivacy(snapshot?.privacy.allowRemoteTextProcessing ?? false, checked)}
                />
              </div>

              <div className="settings-meta-row">
                <span>{snapshot?.moduleCount ?? 0} {t("native modules", "mô-đun native")}</span>
                <span>{t("OCR on device", "OCR trên thiết bị")}</span>
                <span className="action-with-help"><button type="button" className="button button-quiet" onClick={() => void run(t("Opening help", "Đang mở trợ giúp"), () => bridge.command("external.openHelp"))}>
                  <CircleHelp size={16} /> {t("Help and source", "Trợ giúp và mã nguồn")}
                </button><OptionHelp label={t("Help and source", "Trợ giúp và mã nguồn")} /></span>
              </div>
            </div>
          )}

          {activeTab === "translation" && (
            <div className="settings-view">
              <SettingsTitle icon={Languages} title={t("Language processing", "Xử lý ngôn ngữ")} />

              <div className="settings-form-section form-section-grid">
                <div className="field-stack">
                  <span><label htmlFor="ignored-terms">{t("Ignored terms", "Từ bỏ qua")}</label> <OptionHelp label={t("Ignored terms", "Từ bỏ qua")} /></span>
                  <textarea
                    id="ignored-terms"
                    value={draft.ignoredTerms}
                    maxLength={2_000}
                    rows={4}
                    placeholder="HP, MP, EXP"
                    onChange={(event) => update("ignoredTerms", event.target.value)}
                  />
                </div>
                <div className="field-stack">
                  <span><label htmlFor="translation-instruction">{t("Translation instruction", "Văn phong dịch")}</label> <OptionHelp label={t("Translation instruction", "Văn phong dịch")} /></span>
                  <textarea
                    id="translation-instruction"
                    value={draft.translationInstruction}
                    maxLength={4_000}
                    rows={4}
                    onChange={(event) => update("translationInstruction", event.target.value)}
                  />
                </div>
              </div>

              <div className="settings-form-section">
                <ToggleRow label={t("Preserve acronyms and technical terms", "Giữ acronym và thuật ngữ kỹ thuật")} checked={draft.preserveAcronymsAndTechnicalTerms} onChange={(checked) => update("preserveAcronymsAndTechnicalTerms", checked)} />
                <ToggleRow label={t("Close popup on outside click", "Đóng popup khi bấm bên ngoài")} checked={draft.closePopupOnOutsideClick} onChange={(checked) => update("closePopupOnOutsideClick", checked)} />
                <ToggleRow label={t("Allow popup dragging", "Cho phép kéo popup")} checked={draft.allowPopupDragging} onChange={(checked) => update("allowPopupDragging", checked)} />
              </div>
            </div>
          )}

          {activeTab === "realtime" && (
            <div className="settings-view">
              <SettingsTitle icon={Gauge} title={t("Overlay tuning", "Tinh chỉnh lớp phủ")} />

              <div className="settings-form-section range-settings">
                <RangeSetting label={t("Scan interval", "Chu kỳ quét")} value={draft.scanIntervalMs} min={250} max={2_000} step={50} output={`${draft.scanIntervalMs} ms`} onChange={(value) => update("scanIntervalMs", value)} />
                <RangeSetting label={t("Overlay opacity", "Độ mờ lớp phủ")} value={draft.overlayOpacityPercent} min={40} max={100} step={1} output={`${draft.overlayOpacityPercent}%`} onChange={(value) => update("overlayOpacityPercent", value)} />
                <RangeSetting label={t("Text scale", "Cỡ chữ")} value={draft.overlayFontScalePercent} min={60} max={180} step={5} output={`${draft.overlayFontScalePercent}%`} onChange={(value) => update("overlayFontScalePercent", value)} />
              </div>

              <div className="settings-form-section realtime-options-grid">
                <div className="palette-setting">
                  <span>{t("Overlay palette", "Bảng màu lớp phủ")} <OptionHelp label={t("Overlay palette", "Bảng màu lớp phủ")} /></span>
                  <div className="segmented-control palette-segments" role="group" aria-label={t("Overlay palette", "Bảng màu lớp phủ")}>
                    <button type="button" className={draft.useDarkOverlayBlocks ? "segment-active" : ""} onClick={() => update("useDarkOverlayBlocks", true)}><i className="palette-swatch swatch-dark" />{t("Dark", "Tối")}</button>
                    <button type="button" className={!draft.useDarkOverlayBlocks ? "segment-active" : ""} onClick={() => update("useDarkOverlayBlocks", false)}><i className="palette-swatch swatch-light" />{t("Light", "Sáng")}</button>
                  </div>
                </div>
              </div>

              <div className="settings-form-section">
                <ToggleRow label={t("Show OCR debug boxes", "Hiện khung debug OCR")} detail={t("Source block geometry", "Khung vị trí văn bản nguồn")} checked={draft.showOcrDebugBoxes} onChange={(checked) => update("showOcrDebugBoxes", checked)} />
              </div>
            </div>
          )}

          {activeTab === "shortcuts" && (
            <div className="settings-view">
              <SettingsTitle icon={Keyboard} title={t("Shortcuts", "Phím tắt")} />

              <div className="settings-form-section shortcut-settings">
                <ShortcutRow label={t("Popup translation", "Dịch bằng popup")} name="popup" value={draft.shortcuts.popup} capturing={capturing} setCapturing={setCapturing} onKeyDown={captureShortcut} />
                <ShortcutRow label={t("Region translation", "Dịch theo vùng")} name="region" value={draft.shortcuts.region} capturing={capturing} setCapturing={setCapturing} onKeyDown={captureShortcut} />
                <ShortcutRow label={t("Full-screen realtime", "Dịch toàn màn hình")} name="fullScreen" value={draft.shortcuts.fullScreen} capturing={capturing} setCapturing={setCapturing} onKeyDown={captureShortcut} />
                <ShortcutRow label={t("Stop realtime", "Dừng dịch realtime")} name="stop" value={draft.shortcuts.stop} capturing={capturing} setCapturing={setCapturing} onKeyDown={captureShortcut} />
                <ShortcutRow label={t("Clear overlay", "Xóa lớp phủ")} name="reset" value={draft.shortcuts.reset} capturing={capturing} setCapturing={setCapturing} onKeyDown={captureShortcut} />
              </div>

              {hasDuplicateShortcut && <p className="settings-inline-error" role="alert">{t("Each action needs a unique shortcut.", "Mỗi thao tác cần một phím tắt riêng.")}</p>}

              <div className="settings-footer-actions">
                <span className="action-with-help"><button type="button" className="button button-secondary" onClick={() => setDraft((current) => current ? { ...current, shortcuts: { ...defaultShortcuts } } : current)}>
                  <RotateCcw size={16} /> {t("Restore shortcut defaults", "Khôi phục phím tắt mặc định")}
                </button><OptionHelp label={t("Restore shortcut defaults", "Khôi phục phím tắt mặc định")} /></span>
              </div>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}

function SettingsTitle({ icon: Icon, title }: { icon: typeof ScanText; title: string }) {
  return (
    <header className="settings-view-heading">
      <div className="panel-icon panel-icon-orange"><Icon size={19} /></div>
      <div><h3>{title}</h3></div>
    </header>
  );
}

function ToggleRow({ label, detail, inlineDetail = false, checked, disabled, onChange }: {
  label: string;
  detail?: string;
  inlineDetail?: boolean;
  checked: boolean;
  disabled?: boolean;
  onChange: (checked: boolean) => void;
}) {
  const id = useId();
  return (
    <div className="setting-row">
      <span><label htmlFor={id}><strong>{label}</strong></label> <OptionHelp label={label} />{detail && inlineDetail && <small>{detail}</small>}</span>
      <span className="toggle"><input id={id} aria-label={label} type="checkbox" checked={checked} disabled={disabled} onChange={(event) => onChange(event.target.checked)} /><span aria-hidden="true" /></span>
    </div>
  );
}

function RangeSetting({ label, value, min, max, step, output, onChange }: {
  label: string;
  value: number;
  min: number;
  max: number;
  step: number;
  output: string;
  onChange: (value: number) => void;
}) {
  const id = useId();
  return (
    <div className="range-setting">
      <span><strong><label htmlFor={id}>{label}</label> <OptionHelp label={label} /></strong><output htmlFor={id}>{output}</output></span>
      <input id={id} aria-label={label} type="range" value={value} min={min} max={max} step={step} onChange={(event) => onChange(event.target.valueAsNumber)} />
    </div>
  );
}

function ShortcutRow({ label, name, value, capturing, setCapturing, onKeyDown }: {
  label: string;
  name: ShortcutName;
  value: string;
  capturing?: ShortcutName;
  setCapturing: (name?: ShortcutName) => void;
  onKeyDown: (event: KeyboardEvent<HTMLButtonElement>, name: ShortcutName) => void;
}) {
  const isCapturing = capturing === name;
  return (
    <div className="shortcut-setting-row">
      <span>{label} <OptionHelp label={label} /></span>
      <button
        type="button"
        data-shortcut-capture="true"
        className={isCapturing ? "shortcut-capture-active" : ""}
        onClick={() => setCapturing(isCapturing ? undefined : name)}
        onKeyDown={(event) => onKeyDown(event, name)}
        onBlur={() => isCapturing && setCapturing(undefined)}
      >
        <kbd>{isCapturing ? "..." : displayShortcut(value)}</kbd>
      </button>
    </div>
  );
}

function shortcutFromEvent(event: KeyboardEvent<HTMLButtonElement>) {
  const ignored = ["Control", "Shift", "Alt", "Meta", "AltGraph", "Tab"];
  if (ignored.includes(event.key)) return undefined;
  const parts: string[] = [];
  if (event.ctrlKey) parts.push("Ctrl");
  if (event.altKey) parts.push("Alt");
  if (event.shiftKey) parts.push("Shift");
  if (event.metaKey) parts.push("Win");
  const key = normalizeKey(event.key);
  if (!key) return undefined;
  parts.push(key);
  return parts.join("+");
}

function normalizeKey(key: string) {
  if (key === " ") return "Space";
  if (key === "Esc") return "Escape";
  if (key.length === 1) return key.toUpperCase();
  return key;
}

function displayShortcut(shortcut: string) {
  return shortcut.replaceAll("+", " + ").replace("Escape", "Esc");
}

function cloneSettings(settings: DesktopSettings): DesktopSettings {
  return { ...settings, shortcuts: { ...settings.shortcuts } };
}

function languageName(code: string, vietnamese: boolean) {
  const names: Record<string, [string, string]> = {
    auto: ["Auto detect", "Tự động nhận diện"],
    en: ["English", "Tiếng Anh"],
    ja: ["Japanese", "Tiếng Nhật"],
    ko: ["Korean", "Tiếng Hàn"],
    zh: ["Chinese", "Tiếng Trung"],
    vi: ["Vietnamese", "Tiếng Việt"]
  };
  return names[code]?.[vietnamese ? 1 : 0] ?? code.toUpperCase();
}
