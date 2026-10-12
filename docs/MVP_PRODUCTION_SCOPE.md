# Phạm vi Production MVP của LexVerse

## Mục tiêu sản phẩm

Phát hành ứng dụng Windows đáng tin cậy cho hai workflow miễn phí:

1. Dịch văn bản được bôi chọn bằng Popup/F6 và văn bản nhập bằng Quick text.
2. Chọn một vùng tài liệu và giữ overlay bản dịch được cập nhật.

Hai workflow này không yêu cầu account, catalog, checkout hoặc paid entitlement. `MvpProductPolicy` cấp miễn phí `translation.popup`, `translation.region` và `mode.document`.

## Trong phạm vi

- React/WebView2 Control Center với native WPF fallback.
- Popup/F6, Quick text và Document-region.
- Windows Graphics Capture, Windows OCR, normalized cache, coordinate mapping và native overlay.
- Consent rõ ràng trước khi gửi selected/typed/OCR text đến provider.
- Local content-free diagnostics có thể tắt.
- Lỗi provider, OCR, capture, clipboard và cấu hình có đường phục hồi, không làm crash app.
- Responsive startup, bounded memory và deterministic shutdown.
- Keyboard navigation, semantics/accessibility cơ bản và high-DPI support.
- Self-contained MSIX/App Installer, CI, signing guard, checksum và release verification.

## Ngoài phạm vi MVP

- Full-screen translation.
- Comic/manga, Subtitle và Game dialogue profile trong UI production.
- Account sign-in, plan trả phí, checkout, restore và purchase UX mặc định.
- Backend identity, catalog authority, payment/webhook, quota và entitlement issuer.
- Remote telemetry và growth/commerce analytics.
- AI translator sử dụng `TranslationPromptOptions.Instruction`.

Client extension point cho các capability trên có thể tiếp tục tồn tại nhưng phải được gắn trạng thái dormant/conditional và không cản trở free MVP.

## Khoảng trống đang chặn release

Production composition hiện dùng `GoogleCloudTextTranslator` trực tiếp và dựa vào Google Application Default Credentials. Vì vậy một Windows package sạch chưa thể thực hiện hai workflow dịch trên máy khách hàng không có credential.

Trước public MVP phải chọn và triển khai đường production cho translation, ưu tiên gateway giữ provider credential phía server. Tuyệt đối không đóng gói service-account JSON, API key hoặc secret dùng chung trong desktop binary, config, log hoặc artifact.

## Tiêu chí nghiệm thu

- Người dùng cài hoặc cập nhật signed package trên Windows sạch mà không cần cài .NET hay tự cấu hình Google credential.
- Khi commerce chưa cấu hình, UI chỉ hiển thị MVP workflow, privacy và trạng thái cần thiết.
- Popup và Document-region không bao giờ yêu cầu upgrade/account.
- Không có text nào rời thiết bị trước explicit consent; thu hồi consent dừng realtime.
- Popup/F6 và Document-region hoạt động trên browser, Office/PDF reader tiêu biểu, 100–200% DPI và multi-monitor.
- Overlay bám đúng physical screen region, không capture UI LexVerse và không để stale box sau Stop/failure.
- Offline/network/provider/OCR/capture failure tạo user-safe error và có thể retry/stop mà không restart app.
- Release build, frontend check và automated test pass; không có warning được bỏ qua.
- Signed clean-VM rehearsal pass install, first launch, WebView2/native fallback, update, rollback, repair, uninstall, keyboard, Narrator, High Contrast và shutdown.

## Trạng thái hiện tại

| Gate | Trạng thái |
|---|---|
| Hai workflow và free feature matrix | Đã triển khai |
| Consent và local diagnostics | Đã triển khai |
| Capture/OCR/cache/mapping/overlay | Đã triển khai, còn manual DPI/E2E evidence |
| Translation cho package sạch | Chưa đạt do phụ thuộc Application Default Credentials |
| Signed package và HTTPS release host thật | Chưa có bằng chứng |
| Clean-VM/accessibility/update rehearsal | Chưa chạy đầy đủ |

Không được tuyên bố public MVP release-ready cho tới khi mọi tiêu chí ở trên có bằng chứng trên đúng release candidate.



## Integration update — 2026-10-12

Merged dev into feature/ui-japanese-translation. Preserved Japanese OCR Latin/ruby filters, rotated overlays, DPI mapping, text layout, Comic mode, native UI and sample redesign. Added hybrid React/WebView2 UI and authenticated server translation from dev. Production translation requires LEXVERSE_TRANSLATION_ENDPOINT, account tokens and entitlements; the legacy free-feature list is no longer applied by AppCompositionRoot. Comic availability therefore depends on server entitlements. Local HTTP is restricted to explicitly enabled development loopback. Google translation remains available in samples.

Backups: backup/dev-before-japanese-merge-20261012 and backup/japanese-before-dev-merge-20261012. Remote branch has not been pushed. Verification: solution build passed (12 projects, zero warnings/errors); OCR tests 43 passed; translation tests 105 passed; frontend TypeScript and 2 tests passed. Manual Windows OCR/overlay checks remain necessary.
