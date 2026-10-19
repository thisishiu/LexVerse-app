import { useCallback, useEffect, useState } from "react";
import { Activity, CircleAlert, Keyboard, LoaderCircle, X } from "lucide-react";
import { routes, routeIds } from "./app/routes";
import type { AppRoute } from "./app/route-types";
import { useDesktopApp } from "./app/useDesktopApp";
import { StartupIntro } from "./app/StartupIntro";
import "./styles.css";
import { isTranslationError, translationErrorMessage } from "./translationErrors";

function routeFromHash(): AppRoute {
  const candidate = window.location.hash.replace(/^#\/?/, "") as AppRoute;
  return routeIds.includes(candidate) ? candidate : "workspace";
}

function errorMessage(error: string, vietnamese: boolean) {
  if (isTranslationError(error)) return translationErrorMessage(error, vietnamese);
  const messages: Record<string, [string, string]> = {
    "consent-declined": ["Privacy consent was not changed.", "Quyền xử lý dữ liệu không được thay đổi."],
    "invalid-offer": ["That plan is no longer available. Refresh access and try again.", "Gói này không còn khả dụng. Hãy đồng bộ quyền truy cập rồi thử lại."],
    "invalid-text": ["Enter valid text before translating.", "Hãy nhập văn bản hợp lệ trước khi dịch."],
    "invalid-shortcut": ["Choose a valid shortcut for every action.", "Hãy chọn phím tắt hợp lệ cho từng thao tác."],
    "shortcut-unavailable": ["That popup shortcut is already used by another application.", "Phím tắt popup đang được ứng dụng khác sử dụng."],
    "native-host-unavailable": ["This action is available in the LexVerse desktop app.", "Thao tác này chỉ khả dụng trong ứng dụng LexVerse desktop."],
    "operation-cancelled": ["The operation was cancelled.", "Thao tác đã bị hủy."],
    "operation-failed": ["LexVerse could not complete the operation.", "LexVerse không thể hoàn tất thao tác."],
    "bridge-timeout": ["The desktop service did not respond in time.", "Dịch vụ desktop không phản hồi kịp thời."]
  };
  return messages[error]?.[vietnamese ? 1 : 0] ?? error.replaceAll("-", " ");
}

export function App() {
  const app = useDesktopApp();
  const [introActive, setIntroActive] = useState(() =>
    Boolean(window.chrome?.webview) || new URLSearchParams(window.location.search).has("intro"));
  const finishIntro = useCallback(() => {
    if (!window.chrome?.webview) {
      setIntroActive(false);
      return;
    }
    void app.bridge.command("app.introCompleted")
      .catch((reason: unknown) => app.setError(reason instanceof Error ? reason.message : "operation-failed"))
      .finally(() => setIntroActive(false));
  }, [app.bridge, app.setError]);
  const [route, setRoute] = useState<AppRoute>(routeFromHash);
  const Page = routes[route].component;
  const isVietnamese = app.snapshot?.settings.appLanguage === "vi";
  const t = (english: string, vietnamese: string) => isVietnamese ? vietnamese : english;
  const localizedRoute = routeCopy[route][isVietnamese ? 1 : 0];

  useEffect(() => {
    const onHashChange = () => setRoute(routeFromHash());
    window.addEventListener("hashchange", onHashChange);
    return () => window.removeEventListener("hashchange", onHashChange);
  }, []);

  useEffect(() => {
    document.documentElement.lang = isVietnamese ? "vi" : "en";
  }, [isVietnamese]);

  useEffect(() => {
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (introActive || event.repeat || !app.snapshot || app.busy) return;
      const target = event.target instanceof HTMLElement ? event.target : null;
      if (target?.closest("[data-shortcut-capture]") ||
          target?.matches("input, textarea, select, [contenteditable='true']")) return;

      const pressed = shortcutFromKeyboardEvent(event);
      const shortcuts = app.snapshot.settings.shortcuts;
      const action = (Object.entries(shortcuts) as [keyof typeof shortcuts, string][])
        .find(([, shortcut]) => shortcut.toLowerCase() === pressed.toLowerCase())?.[0];
      if (!action) return;

      event.preventDefault();
      const commands = {
        popup: ["Opening popup", "translation.triggerSelectedText"],
        region: ["Selecting screen region", "realtime.selectRegion"],
        fullScreen: ["Starting full-screen translation", "realtime.startFullScreen"],
        stop: ["Stopping live translation", "realtime.stop"],
        reset: ["Clearing overlay", "realtime.resetOverlay"]
      } as const;
      const [label, command] = commands[action];
      void app.run(label, () => app.bridge.command(command));
    };

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [app.snapshot, app.busy, app.bridge, introActive]);

  function navigate(next: AppRoute) {
    if (window.location.hash !== `#/${next}`) {
      window.location.hash = `/${next}`;
    }
    setRoute(next);
  }

  const connectionLabel = app.busy
    ?? (app.snapshot?.status.startsWith("Preview mode")
      ? t("UI preview", "Bản xem trước")
      : app.snapshot ? t("Desktop ready", "Desktop sẵn sàng") : t("Connecting", "Đang kết nối"));

  return (
    <>
    {introActive && <StartupIntro ready={Boolean(app.snapshot)} onComplete={finishIntro} />}
    <div className="app-shell" inert={introActive}>
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark" aria-hidden="true">Lx</div>
          <div className="brand-copy">
            <strong>LexVerse</strong>

          </div>
        </div>

        <nav className="side-nav" aria-label="Application navigation">
          {routeIds.map((id) => {
            const Icon = routes[id].icon;
            return (
              <button
                type="button"
                key={id}
                className={route === id ? "nav-active" : ""}
                aria-current={route === id ? "page" : undefined}
                aria-label={routeCopy[id][isVietnamese ? 1 : 0].label}
                onClick={() => navigate(id)}
                title={routeCopy[id][isVietnamese ? 1 : 0].label}
              >
                <Icon size={18} strokeWidth={2} />
                <span>{routeCopy[id][isVietnamese ? 1 : 0].label}</span>
              </button>
            );
          })}
        </nav>

        <button type="button" aria-label={t("Open account", "Mở tài khoản")} className="sidebar-account" onClick={() => navigate("account")}>
          <span className="sidebar-avatar" aria-hidden="true">
            {app.snapshot?.commerce.displayName?.trim().charAt(0).toUpperCase() ?? "G"}
          </span>
          <span>
            <strong>{app.snapshot?.commerce.displayName ?? t("Guest", "Khách")}</strong>
          </span>
        </button>
      </aside>

      <main className="app-main">
        <header className="topbar">
          <div className="topbar-title">
            <h1>{localizedRoute.title}</h1>

          </div>
          <div className="topbar-tools">
            <div className="hotkey-status" title={app.snapshot?.hotkey ?? "Global shortcut loading"}>
              <Keyboard size={16} />
              <span>Popup</span>
              <kbd>{app.snapshot?.settings.shortcuts.popup ?? "F6"}</kbd>
            </div>
            <div className={`connection-state ${app.error ? "connection-error" : ""}`}>
              {app.busy ? <LoaderCircle className="spin" size={16} /> : <Activity size={16} />}
              <span>{connectionLabel}</span>
            </div>
          </div>
        </header>

        {app.error && (
          <div className="error-banner" role="alert">
            <CircleAlert size={18} />
            <span>{errorMessage(app.error, isVietnamese)}</span>
            <button type="button" onClick={() => app.setError(undefined)} aria-label={t("Dismiss error", "Đóng thông báo lỗi")} title={t("Dismiss", "Đóng")}>
              <X size={17} />
            </button>
          </div>
        )}

        <div className="page-viewport">
          <Page app={app} navigate={navigate} />
        </div>

        <footer className="app-statusbar">
          <span className={`status-dot ${app.error ? "status-dot-error" : ""}`} />
          <p role="status">{app.busy ?? (app.error ? errorMessage(app.error, Boolean(isVietnamese)) : "")}</p>

        </footer>
      </main>
    </div>
    </>
  );
}

const routeCopy = {
  workspace: [
    { label: "Translate", title: "Translate" },
    { label: "Dịch", title: "Dịch" }
  ],
  account: [
    { label: "Account", title: "Account" },
    { label: "Tài khoản", title: "Tài khoản" }
  ],
  settings: [
    { label: "Settings", title: "Application settings" },
    { label: "Cài đặt", title: "Cài đặt ứng dụng" }
  ]
} as const;

function shortcutFromKeyboardEvent(event: globalThis.KeyboardEvent) {
  const parts: string[] = [];
  if (event.ctrlKey) parts.push("Ctrl");
  if (event.altKey) parts.push("Alt");
  if (event.shiftKey) parts.push("Shift");
  if (event.metaKey) parts.push("Win");
  const key = event.key === " " ? "Space" : event.key.length === 1 ? event.key.toUpperCase() : event.key;
  parts.push(key === "Esc" ? "Escape" : key);
  return parts.join("+");
}
