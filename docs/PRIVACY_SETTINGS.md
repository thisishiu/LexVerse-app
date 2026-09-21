# Cài đặt privacy và consent

LexVerse tách hai quyết định độc lập: cho phép gửi text đến translation provider và cho phép ghi local operational diagnostics. Hai preference này không cấp paid feature; feature access là boundary riêng.

## Giá trị mặc định

- **External text processing: tắt.** Popup, Quick text và realtime bị chặn ngay trước provider cho tới khi người dùng xác nhận.
- **Local performance diagnostics: bật.** Chỉ ghi typed counter/timing không chứa nội dung và có thể tắt ngay.

Preference được version hóa và lưu atomically tại `%LOCALAPPDATA%\LexVerse\Settings\privacy.json`. File invalid, quá lớn, sai schema hoặc có field lạ quay về safe default. File chỉ chứa schema version và hai boolean.

## Enforcement boundary

`AppCompositionRoot` chỉ tạo một translator chain:

```text
ConsentCheckingTextTranslator
-> DeferredTextTranslator
-> GoogleCloudTextTranslator
```

Vì Popup, Quick text và mọi realtime profile đều dùng chain này, UI route hoặc entitlement mới không thể bypass consent. Tắt external processing sẽ dừng active realtime coordinator và chặn request tiếp theo.

`ConsentAwareOperationalTelemetry` đọc preference trên từng event; tắt diagnostics không cần restart. Essential crash record vẫn được redaction và không chịu trách nhiệm ghi nội dung debug tùy ý.

## Dữ liệu có thể rời thiết bị

Khi người dùng bật external processing, selected text, typed text hoặc OCR text có thể được gửi đến configured translation provider. Hiện provider production composition là Google Cloud Translation V2 qua Application Default Credentials.

Trước public release, UI/policy phải mô tả chính xác provider, mục đích, retention và đường xử lý production. Không được mô tả “local-only translation” khi code vẫn gọi external provider.

## UI hybrid

- React Control Center hiển thị privacy state từ `DesktopSnapshot` và gửi `privacy.updateConsent` qua bridge.
- Native WPF controls là fallback khi WebView2/asset không khả dụng và dùng cùng `IPrivacyPreferencesService`.
- Native host vẫn là nơi hiện confirmation, persist preference, dừng realtime và publish snapshot mới.
- React không nhận OCR text, frame pixel hoặc provider credential qua snapshot.

## Accessibility

React dùng semantic form controls, label, status live region và keyboard-focus state. Native fallback có AutomationProperties/access key/focus style và scrollable layout. Đây là baseline trong source, chưa phải chứng nhận accessibility.

Trước public release phải test thủ công trên đúng candidate:

- keyboard-only và không focus trap;
- Narrator đọc label/status/error/confirmation;
- 200% DPI, text scaling và minimum window size;
- Windows High Contrast;
- focus sau khi accept/deny consent;
- WebView2 và native fallback;
- thu hồi consent khi realtime đang chạy.

## Remote telemetry tương lai

Không có remote telemetry adapter. Muốn thêm phải có preference upload riêng, TLS, schema allow-list phía server, retention/deletion policy, regional/privacy review, rate limit và test chứng minh content field không thể đi vào payload. Local-only operation phải tiếp tục khả dụng.

