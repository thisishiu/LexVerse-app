import { useEffect, useState } from "react";
import { GuideOption } from "../../components/GuideOption";
import {
  CloudOff,
  CreditCard,
  ExternalLink,
  LogIn,
  LogOut,
  RefreshCw,
  ShieldCheck,
  UserRound
} from "lucide-react";
import type { RouteProps } from "../../app/route-types";
import { subscriptionErrorMessage } from "../../subscriptionErrors";

export function AccountPage({ app }: RouteProps) {
  const {
    bridge,
    snapshot,
    busy,
    run
  } = app;

  const [plansFailure, setPlansFailure] = useState(false);
  const [manualSync, setManualSync] = useState(false);
  const [syncFailure, setSyncFailure] = useState<string>();
  const commerce = snapshot?.commerce;
  const isVietnamese = snapshot?.settings.appLanguage === "vi";
  const t = (english: string, vietnamese: string) => isVietnamese ? vietnamese : english;
  const configured = commerce?.configured ?? false;
  const signedIn = Boolean(commerce?.displayName);
  const checkoutOpen = commerce?.status.toUpperCase() === "CHECKOUT";
  const serviceError = commerce?.status.toUpperCase() === "RETRY";
  const disabled = Boolean(busy);
  const authentication = commerce?.authentication;
  const authenticating = ["opening", "waiting", "completing"].includes(authentication?.status ?? "");
  const canRecoverSignIn = authentication?.status === "waiting";
  const subscription = commerce?.subscription;
  const syncing = manualSync || (signedIn && commerce?.status === "LOADING");
  const subscriptionError = syncFailure ?? commerce?.subscriptionError;
  useEffect(() => { setSyncFailure(undefined); }, [commerce?.lastSyncedAtUtc, signedIn]);
  async function syncAccess() {
    setManualSync(true);
    setSyncFailure(undefined);
    try {
      await run(t("Syncing access", "Đang đồng bộ quyền truy cập"), async () => {
        try { await bridge.command("commerce.restore"); }
        catch (reason) {
          setSyncFailure(reason instanceof Error ? reason.message : "subscription-unavailable");
          throw reason;
        }
      });
    } finally { setManualSync(false); }
  }
  const date = (value: string) => new Date(value).toLocaleString(isVietnamese ? "vi-VN" : "en-US");
  const authenticationLabel = authentication?.status === "opening" ? t("Opening browser", "Đang mở trình duyệt")
    : authentication?.status === "completing" ? t("Completing sign-in", "Đang hoàn tất đăng nhập")
    : t("Waiting for sign-in", "Đang chờ đăng nhập");

  return (
    <section className="page account-page">
      <header className="page-heading">
        <span className={`status-badge ${signedIn ? "status-success" : configured ? "status-neutral" : "status-warning"}`}>
          <span />
          {authenticating ? authenticationLabel : commerce?.status ?? t("Loading", "Đang tải")}
        </span>
      </header>

      <div className="account-grid">
        <section className="panel identity-panel" aria-labelledby="identity-title">
          <header className="panel-heading">
            <div className="panel-icon panel-icon-blue"><UserRound size={19} /></div>
            <div>
              <div className="heading-with-help"><h3 id="identity-title">{t("LexVerse account", "Tài khoản LexVerse")}</h3><GuideOption label={t("LexVerse account", "Tài khoản LexVerse")} /></div>
            </div>
          </header>

          {configured ? (
            <>
              <div className="account-profile">
                <div className="account-avatar" aria-hidden="true">
                  {signedIn ? commerce?.displayName?.trim().charAt(0).toUpperCase() : <UserRound size={24} />}
                </div>
                <div>
                  <strong>{commerce?.displayName ?? t("No active session", "Chưa có phiên đăng nhập")}</strong>
                  <span>{signedIn ? t("Authenticated", "Đã xác thực") : t("Sign in or create an account", "Đăng nhập hoặc tạo tài khoản")}</span>
                </div>
              </div>

              <dl className="detail-list">
                <div><dt>{t("Session", "Phiên")}</dt><dd>{signedIn ? t("Active", "Đang hoạt động") : t("Signed out", "Đã đăng xuất")}</dd></div>
                <div><dt>{t("Access state", "Trạng thái truy cập")}</dt><dd>{checkoutOpen ? t("Refresh required", "Cần đồng bộ") : commerce?.status ?? t("Loading", "Đang tải")}</dd></div>
              </dl>

              <div className="button-row account-actions">
                {!signedIn && !authenticating ? (
                  <span className="action-with-help"><button
                    className="button button-primary"
                    disabled={disabled}
                    onClick={() => void run(t("Opening sign in", "Đang mở đăng nhập"), () => bridge.command("account.signIn"))}
                  >
                    <LogIn size={17} /> {t("Sign in or create account", "Đăng nhập hoặc tạo tài khoản")} <ExternalLink size={14} />
                  </button><GuideOption label={t("Sign in or create account", "Đăng nhập hoặc tạo tài khoản")} /></span>
                ) : signedIn ? (
                  <span className="action-with-help"><button
                    className="button button-secondary"
                    disabled={disabled}
                    onClick={() => void run(t("Signing out", "Đang đăng xuất"), () => bridge.command("account.signOut"))}
                  >
                    <LogOut size={17} /> {t("Sign out", "Đăng xuất")}
                  </button><GuideOption label={t("Sign out", "Đăng xuất")} /></span>
                ) : null}
                {serviceError && !signedIn && !authenticating && (
                  <span className="action-with-help"><button
                    className="button button-secondary"
                    disabled={disabled}
                    onClick={() => void run(t("Retrying account", "Đang thử lại"), () => bridge.command("account.signIn"))}
                  >
                    <RefreshCw size={17} /> {t("Retry", "Thử lại")}
                  </button><GuideOption label={t("Retry", "Thử lại")} /></span>
                )}
              </div>
              {authenticating && (
                <div className="inline-notice notice-info" role="status">
                  <div>
                    <p>{authentication?.status === "completing"
                      ? t("Completing sign-in…", "Đang hoàn tất đăng nhập…")
                      : t("Complete sign-in in your browser. LexVerse will update automatically. You have 15 minutes.", "Hoàn tất đăng nhập trên trình duyệt. LexVerse sẽ tự cập nhật. Bạn có 15 phút.")}</p>
                    {authentication?.error && <p role="alert">{t("Could not open the browser. Reopen it or copy the link.", "Chưa mở được trình duyệt. Hãy mở lại hoặc sao chép liên kết.")}</p>}
                    <div className="button-row">
                      <span className="action-with-help"><button className="button button-secondary" disabled={disabled || !canRecoverSignIn} onClick={() => void run(t("Opening browser", "Đang mở trình duyệt"), () => bridge.command("account.reopenSignIn"))}>{t("Reopen browser", "Mở lại trình duyệt")}</button><GuideOption label={t("Reopen browser", "Mở lại trình duyệt")} /></span>
                      <span className="action-with-help"><button className="button button-secondary" disabled={disabled || !canRecoverSignIn} onClick={() => void run(t("Copying link", "Đang sao chép liên kết"), () => bridge.command("account.copySignInLink"))}>{t("Copy link", "Sao chép liên kết")}</button><GuideOption label={t("Copy link", "Sao chép liên kết")} /></span>
                      <span className="action-with-help"><button className="button button-quiet" disabled={disabled} onClick={() => void run(t("Canceling sign-in", "Đang hủy đăng nhập"), () => bridge.command("account.cancelSignIn"))}>{t("Cancel", "Hủy")}</button><GuideOption label={t("Cancel", "Hủy")} /></span>
                    </div>
                  </div>
                </div>
              )}
              {authentication?.status === "error" && <p role="alert">{t("Sign-in did not complete, was canceled or expired. Please try again.", "Đăng nhập chưa hoàn tất, đã hủy hoặc hết hạn. Hãy thử lại.")}</p>}
            </>
          ) : (
            <div className="empty-state compact-empty">
              <CloudOff size={24} />
              <div>
                <strong>{t("Account service is not configured", "Dịch vụ tài khoản chưa được cấu hình")}</strong>
                <p>{t("LexVerse is running in Free mode.", "LexVerse đang chạy ở chế độ miễn phí.")}</p>
              </div>
            </div>
          )}
        </section>

        <section className="panel plan-panel" aria-label={t("Plan management", "Quản lý gói")}>
          <header className="panel-heading panel-heading-with-action">
            <div className="panel-heading-copy">
              <div className="panel-icon panel-icon-orange"><CreditCard size={19} /></div>
              <div>
                <div className="heading-with-help"><h3 id="plan-title">{t("Current plan", "Gói đang dùng")}</h3><GuideOption label={t("Plans", "Các gói")} /></div>
              </div>
            </div>
            {signedIn && (
              <span className="action-with-help"><button
                className="button button-quiet"
                disabled={disabled || syncing}
                onClick={() => void syncAccess()}
              >
                <RefreshCw size={16} /> {syncing ? t("Syncing…", "Đang đồng bộ…") : t("Sync access", "Đồng bộ quyền truy cập")}
              </button><GuideOption label={t("Sync access", "Đồng bộ")} /></span>
            )}
          </header>

          <section className="current-plan" aria-label={t("Current plan", "Gói đang dùng")}>
            <dl className="detail-list">
              <div className="current-plan-name"><dt>{t("Plan", "Gói")}</dt><dd>{signedIn ? subscription?.current.displayName ?? (subscriptionError ? t("Could not load plan", "Không tải được gói") : t("Not synced yet", "Chưa đồng bộ")) : t("Free mode", "Chế độ miễn phí")}</dd></div>
              {subscription && <div><dt>{t("Valid from", "Có hiệu lực từ")}</dt><dd>{date(subscription.current.startsAtUtc)}</dd></div>}
              {subscription && <div><dt>{t("Valid until", "Có hiệu lực đến")}</dt><dd>{date(subscription.current.endsAtUtc)}</dd></div>}
              {commerce?.lastSyncedAtUtc && <div><dt>{t("Last synced", "Đồng bộ lần cuối")}</dt><dd>{date(commerce.lastSyncedAtUtc)}</dd></div>}
            </dl>
            {signedIn && (syncing ? <p role="status">{t("Syncing your plan…", "Đang đồng bộ gói đang dùng…")}</p>
              : subscriptionError ? <p role="alert">{subscriptionErrorMessage(subscriptionError, Boolean(isVietnamese))}</p>
              : subscription && commerce?.lastSyncedAtUtc ? <p role="status">{t("Plan synced successfully.", "Đã đồng bộ gói thành công.")}</p> : null)}
            {commerce?.syncError && <p role="status">{t("Some payments could not be checked. LexVerse will retry automatically.", "Chưa kiểm tra được một số giao dịch. LexVerse sẽ tự thử lại.")}</p>}
            {subscription?.upcoming.map(period => <p key={period.id}>{t("Upcoming", "Kỳ sắp tới")}: {period.displayName} · {date(period.startsAtUtc)} → {date(period.endsAtUtc)}</p>)}
            {commerce?.pendingCheckout && <p role="status">{t("Payment pending until", "Đang chờ thanh toán đến")} {date(commerce.pendingCheckout.expiresAtUtc)}. {t("Your current plan stays active.", "Gói đang dùng vẫn được giữ nguyên.")}</p>}
          </section>
          <div className="browse-plans-action">
            <button type="button" className="button button-primary"
              disabled={disabled || !commerce?.plansAvailable}
              aria-describedby={commerce?.plansAvailable ? "plans-web-hint" : undefined}
              onClick={() => {
                setPlansFailure(false);
                void run(t("Opening plans website", "Đang mở trang gói"), async () => {
                  try { await bridge.command("commerce.openPlans"); }
                  catch (reason) { setPlansFailure(true); throw reason; }
                });
              }}>
              {t("View more plan", "Xem thêm gói")} <ExternalLink size={16} aria-hidden="true" />
            </button>
            {commerce?.plansAvailable && <p id="plans-web-hint">{t("After paying on the web, return here to sync your access.", "Sau khi thanh toán trên web, quay lại đây để đồng bộ.")}</p>}
            {plansFailure && <p role="alert">{t("Could not open the browser. Please try again.", "Không mở được trình duyệt. Vui lòng thử lại.")}</p>}
          </div>
        </section>
      </div>

      {checkoutOpen && (
        <div className="inline-notice notice-info" role="status">
          <ShieldCheck size={18} />
          <p><strong>{t("Checkout opened.", "Đã mở trang thanh toán.")}</strong> {t("Access updates after the payment provider confirms the purchase and LexVerse refreshes the entitlement.", "Quyền truy cập sẽ cập nhật sau khi nhà cung cấp xác nhận thanh toán và LexVerse đồng bộ lại.")}</p>
          <span className="action-with-help"><button
            className="button button-secondary"
            disabled={disabled || !signedIn}
            onClick={() => void run(t("Refreshing access", "Đang làm mới quyền truy cập"), () => bridge.command("commerce.restore"))}
          >
            <RefreshCw size={16} /> {t("Refresh access", "Làm mới quyền truy cập")}
          </button><GuideOption label={t("Refresh access", "Làm mới quyền truy cập")} /></span>
        </div>
      )}
    </section>
  );
}
