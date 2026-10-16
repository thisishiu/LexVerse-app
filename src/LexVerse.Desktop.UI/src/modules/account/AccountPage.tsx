import {
  CheckCircle2,
  CircleDollarSign,
  CloudOff,
  CreditCard,
  ExternalLink,
  KeyRound,
  LogIn,
  LogOut,
  RefreshCw,
  ShieldCheck,
  UserRound
} from "lucide-react";
import type { RouteProps } from "../../app/route-types";

export function AccountPage({ app }: RouteProps) {
  const {
    bridge,
    snapshot,
    selectedOffer,
    setSelectedOffer,
    busy,
    run
  } = app;

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
  const authenticationLabel = authentication?.status === "opening" ? t("Opening browser", "Đang mở trình duyệt")
    : authentication?.status === "completing" ? t("Completing sign-in", "Đang hoàn tất đăng nhập")
    : t("Waiting for sign-in", "Đang chờ đăng nhập");

  return (
    <section className="page account-page">
      <header className="page-heading">
        <div>
          <p className="page-kicker">{t("ACCOUNT", "TÀI KHOẢN")}</p>
          <h2>{t("Identity and access", "Danh tính và quyền truy cập")}</h2>
          <p>{t("Manage your session, plan and current access state.", "Quản lý phiên đăng nhập, gói dịch vụ và quyền sử dụng.")}</p>
        </div>
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
              <p className="panel-kicker">{t("SESSION", "PHIÊN ĐĂNG NHẬP")}</p>
              <h3 id="identity-title">{t("LexVerse account", "Tài khoản LexVerse")}</h3>
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
                <div><dt>{t("Authentication", "Xác thực")}</dt><dd><KeyRound size={15} /> {t("Secure browser", "Trình duyệt bảo mật")}</dd></div>
                <div><dt>{t("Session", "Phiên")}</dt><dd>{signedIn ? t("Active", "Đang hoạt động") : t("Signed out", "Đã đăng xuất")}</dd></div>
                <div><dt>{t("Access state", "Trạng thái truy cập")}</dt><dd>{checkoutOpen ? t("Refresh required", "Cần đồng bộ") : commerce?.status ?? t("Loading", "Đang tải")}</dd></div>
              </dl>

              <div className="button-row account-actions">
                {!signedIn && !authenticating ? (
                  <button
                    className="button button-primary"
                    disabled={disabled}
                    onClick={() => void run(t("Opening sign in", "Đang mở đăng nhập"), () => bridge.command("account.signIn"))}
                  >
                    <LogIn size={17} /> {t("Sign in or create account", "Đăng nhập hoặc tạo tài khoản")} <ExternalLink size={14} />
                  </button>
                ) : signedIn ? (
                  <button
                    className="button button-secondary"
                    disabled={disabled}
                    onClick={() => void run(t("Signing out", "Đang đăng xuất"), () => bridge.command("account.signOut"))}
                  >
                    <LogOut size={17} /> {t("Sign out", "Đăng xuất")}
                  </button>
                ) : null}
                {serviceError && !signedIn && !authenticating && (
                  <button
                    className="button button-secondary"
                    disabled={disabled}
                    onClick={() => void run(t("Retrying account", "Đang thử lại"), () => bridge.command("account.signIn"))}
                  >
                    <RefreshCw size={17} /> {t("Retry", "Thử lại")}
                  </button>
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
                      <button className="button button-secondary" disabled={disabled || !canRecoverSignIn} onClick={() => void run(t("Opening browser", "Đang mở trình duyệt"), () => bridge.command("account.reopenSignIn"))}>{t("Reopen browser", "Mở lại trình duyệt")}</button>
                      <button className="button button-secondary" disabled={disabled || !canRecoverSignIn} onClick={() => void run(t("Copying link", "Đang sao chép liên kết"), () => bridge.command("account.copySignInLink"))}>{t("Copy link", "Sao chép liên kết")}</button>
                      <button className="button button-quiet" disabled={disabled} onClick={() => void run(t("Canceling sign-in", "Đang hủy đăng nhập"), () => bridge.command("account.cancelSignIn"))}>{t("Cancel", "Hủy")}</button>
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

        <section className="panel plan-panel" aria-labelledby="plan-title">
          <header className="panel-heading panel-heading-with-action">
            <div className="panel-heading-copy">
              <div className="panel-icon panel-icon-orange"><CreditCard size={19} /></div>
              <div>
                <p className="panel-kicker">{t("SUBSCRIPTION", "GÓI DỊCH VỤ")}</p>
                <h3 id="plan-title">{t("Plans", "Các gói")}</h3>
              </div>
            </div>
            {signedIn && (
              <button
                className="button button-quiet"
                disabled={disabled}
                onClick={() => void run(t("Restoring access", "Đang đồng bộ quyền truy cập"), () => bridge.command("commerce.restore"))}
              >
                <RefreshCw size={16} /> {t("Sync access", "Đồng bộ")}
              </button>
            )}
          </header>

          {!configured ? (
            <div className="empty-state">
              <CircleDollarSign size={27} />
              <div><strong>{t("Free mode", "Chế độ miễn phí")}</strong><p>{t("Paid plans are unavailable in this configuration.", "Các gói trả phí chưa khả dụng trong cấu hình này.")}</p></div>
            </div>
          ) : commerce?.offers.length ? (
            <div className="offer-list" role="radiogroup" aria-label={t("Available plans", "Các gói khả dụng")}>
              {commerce.offers.map((offer) => (
                <label className={`offer-option ${selectedOffer === offer.id ? "offer-selected" : ""}`} key={offer.id}>
                  <input
                    type="radio"
                    name="plan"
                    value={offer.id}
                    checked={selectedOffer === offer.id}
                    onChange={() => setSelectedOffer(offer.id)}
                  />
                  <span className="offer-radio" aria-hidden="true" />
                  <strong>{offer.label}</strong>
                  {selectedOffer === offer.id && <CheckCircle2 size={18} aria-hidden="true" />}
                </label>
              ))}
            </div>
          ) : (
            <div className="empty-state">
              <CircleDollarSign size={27} />
              <div><strong>{t("No plans available", "Chưa có gói khả dụng")}</strong><p>{t("The product catalog did not return a public offer.", "Danh mục sản phẩm chưa trả về gói công khai.")}</p></div>
            </div>
          )}

          {configured && (
            <div className="plan-footer">
              <div className="verified-payment">
                <ShieldCheck size={17} />
                <span>{t("Provider-verified checkout and signed entitlements", "Thanh toán được xác minh và quyền sử dụng có chữ ký")}</span>
              </div>
              <button
                className="button button-primary"
                disabled={disabled || !signedIn || !selectedOffer}
                onClick={() => void run(t("Opening checkout", "Đang mở thanh toán"), () => bridge.command("commerce.checkout", { offerId: selectedOffer }))}
              >
                <CreditCard size={17} /> {t("Continue to checkout", "Tiếp tục thanh toán")} <ExternalLink size={14} />
              </button>
            </div>
          )}
        </section>
      </div>

      {checkoutOpen && (
        <div className="inline-notice notice-info" role="status">
          <ShieldCheck size={18} />
          <p><strong>{t("Checkout opened.", "Đã mở trang thanh toán.")}</strong> {t("Access updates after the payment provider confirms the purchase and LexVerse refreshes the entitlement.", "Quyền truy cập sẽ cập nhật sau khi nhà cung cấp xác nhận thanh toán và LexVerse đồng bộ lại.")}</p>
          <button
            className="button button-secondary"
            disabled={disabled || !signedIn}
            onClick={() => void run(t("Refreshing access", "Đang làm mới quyền truy cập"), () => bridge.command("commerce.restore"))}
          >
            <RefreshCw size={16} /> {t("Refresh access", "Làm mới quyền truy cập")}
          </button>
        </div>
      )}
    </section>
  );
}
