import type { FormEvent } from "react";
import {
  ArrowLeftRight,
  Clipboard,
  Eraser,
  Keyboard,
  Languages,
  Maximize2,
  MonitorUp,
  ScanLine,
  ShieldAlert,
  Square
} from "lucide-react";
import type { RouteProps } from "../../app/route-types";

export const languages = [
  ["auto", "Auto detect"],
  ["en", "English"],
  ["ja", "Japanese"],
  ["ko", "Korean"],
  ["zh", "Chinese"],
  ["vi", "Vietnamese"]
] as const;

export function WorkspacePage({ app, navigate }: RouteProps) {
  const {
    bridge,
    snapshot,
    quickText,
    setQuickText,
    sourceLanguage,
    setSourceLanguage,
    targetLanguage,
    setTargetLanguage,
    busy,
    setError,
    run
  } = app;

  const remoteProcessingAllowed = snapshot?.privacy.allowRemoteTextProcessing ?? false;
  const isVietnamese = snapshot?.settings.appLanguage === "vi";
  const t = (english: string, vietnamese: string) => isVietnamese ? vietnamese : english;
  const translatingLabel = t("Translating", "Đang dịch");
  const realtimeStatus = snapshot?.realtime.status ?? "loading";
  const realtimeRunning = realtimeStatus === "running" || realtimeStatus === "starting";
  const disabled = Boolean(busy);

  function translate(event: FormEvent) {
    event.preventDefault();
    if (!quickText.trim()) {
      setError("invalid-text");
      return;
    }
    void run(translatingLabel, () => bridge.command("translation.translateQuickText", {
      text: quickText,
      sourceLanguage,
      targetLanguage
    }));
  }

  function swapLanguages() {
    if (sourceLanguage === "auto") return;
    setSourceLanguage(targetLanguage);
    setTargetLanguage(sourceLanguage);
  }

  function copyTranslation() {
    if (!snapshot?.quickTranslation) return;
    void run(t("Copying translation", "Đang sao chép"), () => navigator.clipboard.writeText(snapshot.quickTranslation));
  }

  return (
    <section className="page workspace-page">

      <div className="workspace-grid">
        <form className="panel translator-panel" onSubmit={translate}>
          <header className="panel-heading panel-heading-with-action">
            <div className="panel-heading-copy">
              <div className="panel-icon panel-icon-orange"><Languages size={19} /></div>
              <div>
                <h3>{t("Text translator", "Dịch văn bản")}</h3>
              </div>
            </div>
            <span className="character-count">{quickText.length.toLocaleString()} / 32,000</span>
          </header>

          <div className="language-toolbar">
            <label>
              <span>{t("From", "Từ")}</span>
              <select value={sourceLanguage} onChange={(event) => setSourceLanguage(event.target.value)}>
                {languages.map(([value]) => <option key={value} value={value}>{languageLabel(value, isVietnamese)}</option>)}
              </select>
            </label>
            <button
              type="button"
              className="icon-button"
              onClick={swapLanguages}
              disabled={sourceLanguage === "auto"}
              aria-label={t("Swap languages", "Đổi chiều ngôn ngữ")}
              title={sourceLanguage === "auto" ? t("Choose a source language to swap", "Chọn ngôn ngữ nguồn để đổi chiều") : t("Swap languages", "Đổi chiều ngôn ngữ")}
            >
              <ArrowLeftRight size={18} />
            </button>
            <label>
              <span>{t("To", "Sang")}</span>
              <select value={targetLanguage} onChange={(event) => setTargetLanguage(event.target.value)}>
                {languages.filter(([value]) => value !== "auto").map(([value]) => (
                  <option key={value} value={value}>{languageLabel(value, isVietnamese)}</option>
                ))}
              </select>
            </label>
          </div>

          <div className="translation-editors">
            <label className="translation-pane source-pane" htmlFor="quick-text">
              <span className="pane-label">{t("Source text", "Văn bản nguồn")}</span>
              <textarea
                id="quick-text"
                value={quickText}
                onChange={(event) => setQuickText(event.target.value)}
                placeholder={t("Enter or paste text", "Nhập hoặc dán văn bản")}
                maxLength={32_000}
              />
            </label>
            <section className="translation-pane result-pane" aria-labelledby="translation-result-label">
              <div className="pane-header">
                <span className="pane-label" id="translation-result-label">{t("Translation", "Bản dịch")}</span>
                <button
                  type="button"
                  className="icon-button icon-button-small"
                  disabled={!snapshot?.quickTranslation}
                  onClick={copyTranslation}
                  aria-label={t("Copy translation", "Sao chép bản dịch")}
                  title={t("Copy translation", "Sao chép bản dịch")}
                >
                  <Clipboard size={16} />
                </button>
              </div>
              <output aria-live="polite">
                {snapshot?.quickTranslation || <span className="result-placeholder">{t("Translation will appear here.", "Bản dịch sẽ xuất hiện tại đây.")}</span>}
              </output>
            </section>
          </div>

          <footer className="translator-footer">
            {!remoteProcessingAllowed ? (
              <button type="button" className="privacy-link" onClick={() => navigate("settings")}>
                <ShieldAlert size={16} /> {t("External processing is off", "Xử lý bên ngoài đang tắt")}
              </button>
            ) : <span />}
            <button
              className="button button-primary translate-action"
              disabled={disabled || !remoteProcessingAllowed || !quickText.trim()}
            >
              <Languages size={17} />
              {busy === translatingLabel ? t("Translating...", "Đang dịch...") : t("Translate", "Dịch")}
            </button>
          </footer>
        </form>

        <aside className="workspace-side">
          <section className="panel live-panel" aria-labelledby="live-title">
            <header className="panel-heading">
              <div className="panel-icon panel-icon-green"><MonitorUp size={19} /></div>
              <div>
                <h3 id="live-title">{t("Live translation", "Dịch thời gian thực")}</h3>
              </div>
              <span className={`status-badge ${realtimeRunning ? "status-success" : "status-neutral"}`}>
                <span /> {formatRealtimeStatus(realtimeStatus, isVietnamese)}
              </span>
            </header>

            <div className="live-actions">
              <button
                type="button"
                className="button button-primary"
                disabled={disabled || !remoteProcessingAllowed || !snapshot?.realtime.canStartRegion}
                onClick={() => void run(t("Selecting screen region", "Đang chọn vùng màn hình"), () => bridge.command("realtime.selectRegion"))}
              >
                <ScanLine size={18} /> {t("Region", "Vùng")}
              </button>
              <button
                type="button"
                className="button button-secondary"
                disabled={disabled || !remoteProcessingAllowed || !snapshot?.realtime.canStartFullScreen}
                onClick={() => void run(t("Starting full-screen translation", "Đang dịch toàn màn hình"), () => bridge.command("realtime.startFullScreen"))}
              >
                <Maximize2 size={17} /> {t("Full screen", "Toàn màn hình")}
              </button>
              <button
                type="button"
                className="icon-button live-stop-button"
                disabled={disabled || !snapshot?.realtime.canStop}
                onClick={() => void run(t("Stopping live translation", "Đang dừng dịch realtime"), () => bridge.command("realtime.stop"))}
                aria-label={t("Stop live translation", "Dừng dịch realtime")}
                title={t("Stop live translation", "Dừng dịch realtime")}
              >
                <Square size={14} fill="currentColor" />
              </button>
            </div>

            <div className="overlay-action">
              <button
                    type="button"
                    className="icon-button icon-button-small"
                    disabled={disabled}
                    onClick={() => void run(t("Clearing overlay", "Đang xóa lớp phủ"), () => bridge.command("realtime.resetOverlay"))}
                    aria-label={t("Clear overlay", "Xóa lớp phủ")}
                    title={t("Clear overlay", "Xóa lớp phủ")}
                  >
                    <Eraser size={15} />
              </button>
              <span>{t("Clear overlay", "Xóa lớp phủ")}</span>
            </div>
            <details className="realtime-more">
              <summary>{t("Screen details", "Chi tiết màn hình")}</summary>
              <dl className="detail-list realtime-details">
              <div><dt>OCR</dt><dd>{t("Local", "Cục bộ")}</dd></div>
              <div><dt>{t("Mode", "Chế độ")}</dt><dd>{formatMode(snapshot?.settings.ocrMode, isVietnamese)}</dd></div>
              <div><dt>{t("Frame time", "Thời gian khung")}</dt><dd>{formatFrameTime(snapshot?.realtime.lastFrameMilliseconds, isVietnamese)}</dd></div>
              <div>
                <dt>{t("Performance", "Hiệu năng")}</dt>
                <dd className={snapshot?.realtime.performanceBudgetExceeded ? "text-warning" : "text-success"}>
                  {snapshot?.realtime.performanceBudgetExceeded ? t("Over budget", "Quá ngưỡng") : t("Normal", "Bình thường")}
                </dd>
              </div>

              </dl>
            </details>

            {!remoteProcessingAllowed && (
              <div className="inline-notice notice-warning compact-notice">
                <ShieldAlert size={18} />
                <p>{t("External text processing is disabled.", "Xử lý văn bản bên ngoài đang tắt.")}</p>
                <button type="button" className="button button-quiet" onClick={() => navigate("settings")}>{t("Review", "Xem lại")}</button>
              </div>
            )}
          </section>

          <section className="panel popup-panel" aria-labelledby="popup-title">
            <header className="panel-heading">
              <div className="panel-icon panel-icon-blue"><Keyboard size={19} /></div>
              <div>
                <h3 id="popup-title">{t("Selected text", "Văn bản đã chọn")}</h3>
              </div>
            </header>
            <div className="shortcut-row">
              <div>
                <span>{t("Translate selected text", "Dịch văn bản đã chọn")}</span>

              </div>
              <div className="shortcut-controls">
                <kbd>{snapshot?.settings.shortcuts.popup ?? "F6"}</kbd>
                <button
                  type="button"
                  className="icon-button icon-button-small"
                  disabled={disabled || !remoteProcessingAllowed}
                  onClick={() => void run(t("Reading selected text", "Đang đọc văn bản đã chọn"), () => bridge.command("translation.triggerSelectedText"))}
                  aria-label={t("Translate selected text", "Dịch văn bản đã chọn")}
                  title={t("Translate selected text", "Dịch văn bản đã chọn")}
                >
                  <Languages size={15} />
                </button>
              </div>
            </div>
          </section>
        </aside>
      </div>
    </section>
  );
}

function languageLabel(value: string, vietnamese = false) {
  const localized: Record<string, string> = {
    auto: "Tự động nhận diện",
    en: "Tiếng Anh",
    ja: "Tiếng Nhật",
    ko: "Tiếng Hàn",
    zh: "Tiếng Trung",
    vi: "Tiếng Việt"
  };
  return vietnamese ? localized[value] ?? value.toUpperCase() : languages.find(([code]) => code === value)?.[1] ?? value.toUpperCase();
}

function formatFrameTime(milliseconds?: number, vietnamese = false) {
  return milliseconds == null ? (vietnamese ? "Chưa đo" : "Not measured") : `${milliseconds.toFixed(0)} ms`;
}

function formatMode(mode?: string, vietnamese = false) {
  return (vietnamese ? modesVi : modes)[mode ?? ""] ?? (vietnamese ? "Tài liệu" : "Document");
}

const modes: Record<string, string> = {
  fullscreen: "Full screen",
  subtitle: "Subtitle",
  "game-dialogue": "Game dialogue",
  document: "Document",
  comic: "Comic"
};

const modesVi: Record<string, string> = {
  fullscreen: "Toàn màn hình",
  subtitle: "Phụ đề",
  "game-dialogue": "Hội thoại game",
  document: "Tài liệu",
  comic: "Truyện tranh"
};


function formatRealtimeStatus(status: string, vietnamese: boolean) {
  const labels: Record<string, [string, string]> = {
    loading: ["Loading", "Đang tải"],
    stopped: ["Stopped", "Đã dừng"],
    starting: ["Starting", "Đang bắt đầu"],
    running: ["Running", "Đang dịch"],
    stopping: ["Stopping", "Đang dừng"],
    faulted: ["Error", "Lỗi"],
    error: ["Error", "Lỗi"]
  };
  return labels[status]?.[vietnamese ? 1 : 0] ?? status;
}
