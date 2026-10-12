# Kiến trúc production của LexVerse

Tài liệu này mô tả kiến trúc **đang có trong repository** trước, sau đó mới nêu ranh giới target còn thiếu. Snapshot bàn giao ngắn gọn nằm ở [PROJECT_STATE.md](PROJECT_STATE.md); phạm vi phát hành nằm ở [MVP_PRODUCTION_SCOPE.md](MVP_PRODUCTION_SCOPE.md).

## Quy ước trạng thái

| Trạng thái | Ý nghĩa |
|---|---|
| Đã triển khai | Code được composition root sử dụng và xuất hiện trong workflow hiện tại. |
| Dormant/conditional | Client code tồn tại nhưng bị ẩn, phụ thuộc cấu hình hoặc không thuộc MVP. |
| External/deferred | Thành phần không tồn tại trong repository này. |

## Kiến trúc as-built

```text
┌──────────────── LexVerse Desktop process ────────────────┐
│ React/Vite UI                                            │
│      │ bridge v1                                         │
│ WebView2 in WPF MainWindow ── native WPF fallback        │
│      │                                                   │
│ AppCompositionRoot                                       │
│      ├─ Application: use case/coordinator/gate/privacy   │
│      ├─ Core: model/policy/pipeline/cache/geometry       │
│      ├─ Infrastructure: Windows + remote adapters        │
│      ├─ OCR: Windows OCR                                 │
│      └─ Overlay: native WPF windows                      │
└──────────────────────────────────────────────────────────┘
                         │ HTTPS, conditional
                         ▼
              External providers/services
```

Dependency direction thực tế:

- `LexVerse.Core` không phụ thuộc WPF hay provider cụ thể.
- `LexVerse.Application` phụ thuộc Core và định nghĩa orchestration/port.
- `LexVerse.Infrastructure` phụ thuộc Application/Core/OCR để hiện thực adapter.
- `LexVerse.App` là composition root và được phép tham chiếu tất cả module production.
- `LexVerse.Desktop.UI` giao tiếp với native host qua contract JSON, không gọi provider trực tiếp.
- `LexVerse.Overlay` render output trung lập từ pipeline; frame không đi qua React.

## Host hybrid và bridge v1

`MainWindow` khởi tạo native controls trước rồi tải WebView2 bất đồng bộ. Asset frontend được build vào `obj/dist`, copy/publish dưới `DesktopUi/` và phục vụ bằng virtual host `https://app.lexverse.local`. Khi WebView2 Runtime hoặc asset không khả dụng, WebView bị collapse và native WPF Control Center tiếp tục hoạt động.

Bridge có ba envelope:

```json
{ "version": 1, "id": "...", "type": "command", "payload": {} }
{ "version": 1, "id": "...", "ok": true, "payload": {} }
{ "version": 1, "type": "app.snapshot", "payload": {} }
```

Ràng buộc:

- message tối đa 256 KiB;
- version phải bằng `1`;
- `id` tối đa 128 ký tự, `type` tối đa 96 ký tự;
- JSON phân biệt hoa thường và từ chối member lạ;
- native chỉ dispatch command nằm trong allow-list;
- side effect được serialize; lỗi trả stable error code thay vì exception text;
- frontend timeout command sau 30 giây và hủy pending request khi dispose.

WebView2 chặn foreign navigation, new window, download và permission request; DevTools và context menu mặc định bị tắt. Chỉ asset đóng gói được ánh xạ vào origin nội bộ.

## Workflow production hiện tại

### Popup/F6 và Quick text

`PopupTranslationUseCase` kiểm tra `translation.popup` trước khi đọc selected text hoặc gọi translator. Quick text dùng cùng gate và translator. F6/global hotkey, clipboard copy có thời hạn, cancellation và native popup do WPF host quản lý.

### Document-region realtime

`RealtimeTranslationCoordinator` kiểm tra feature trước khi tạo capture resource. `WindowsRealtimePipelineSessionFactory` lắp Windows Graphics Capture, region provider, Windows OCR, cache, translator và mapping. Kết quả được chuyển thành `RealtimeFrameUpdate`; `WpfRealtimeOverlayOutput` reconcile item tại chỗ để giảm flicker và chuyển physical screen coordinate sang WPF DIP.

```text
capture -> region/change policy -> OCR -> normalize/group
-> translation cache -> translate miss -> coordinate mapping -> overlay
```

Control Center, popup và overlay dùng display affinity để tránh bị capture ngược vào pipeline.

## Privacy, telemetry và lỗi

- `ConsentCheckingTextTranslator` là enforcement point dùng chung ngay trước external translator.
- External text processing mặc định tắt; thu hồi consent dừng realtime.
- `ConsentAwareOperationalTelemetry` kiểm tra preference trên từng record.
- `OperationalEvent` là schema typed, không có arbitrary payload chứa nội dung.
- Local JSONL writer bounded/rotated; không có remote telemetry adapter.
- Crash log chỉ giữ type, HRESULT, timestamp và fingerprint; không ghi raw message/stack.

## Product và commerce boundary

MVP miễn phí được quyết định trong `MvpProductPolicy`, không phụ thuộc account. Feature gate vẫn là authoritative execution boundary; việc ẩn button không thay thế authorization.

Các adapter OAuth/PKCE, DPAPI session, catalog, checkout, restore và signed entitlement đã có ở client nhưng chỉ hoạt động khi đủ biến môi trường. Chúng là **dormant/conditional**. Repository không chứa identity server, payment authority, webhook handler, purchase ledger hay entitlement issuer.

## Translation provider và production gap

Composition root hiện tạo:

```text
ConsentCheckingTextTranslator
-> DeferredTextTranslator
-> GoogleCloudTextTranslator
```

Google adapter dùng Application Default Credentials. Cấu trúc này phù hợp development nhưng chưa đáp ứng package sạch cho khách hàng. Target production cần một Translation Gateway hoặc cơ chế credential không phát hành secret trong desktop; quyết định này chưa được hiện thực trong repo.

## Build, distribution và release

- `.NET 10` và Node 24 là toolchain build.
- MSBuild chạy `npm ci` khi cần, `npm run build`, rồi copy asset frontend vào output/publish.
- `scripts/package-msix.ps1` tạo self-contained MSIX, App Installer và checksum; production signing fail closed.
- CI chạy frontend check/build, Release build/test, dependency audit và unsigned packaging smoke.
- Release-candidate workflow có protected environment, certificate validation, signature/timestamp/hash và provenance.

Các workflow này chưa thay thế bằng chứng vận hành thật: certificate, HTTPS host, WebView2 bootstrap/dependency, signed clean-VM install/update/rollback và release promotion vẫn là external prerequisite.

## Target boundary còn thiếu

```text
Desktop client
  -> Translation Gateway (free MVP, chưa có)
  -> OAuth/Account API (deferred)
  -> Catalog/Checkout/Restore API (deferred)
  -> Signed Entitlement API/issuer (deferred)
  -> Payment provider + verified webhook/reconciliation (deferred)
```

Không chọn database hoặc topology dịch vụ trong tài liệu as-built. Những quyết định đó phải được ghi bằng ADR sau khi có backend repository và ownership rõ ràng.

## Nguyên tắc thay đổi

- Không đưa provider construction hoặc realtime loop vào React/WPF event handler.
- Không hard-code document behavior vào shared contract.
- Không cho catalog/redirect/payment response phía client tự cấp quyền.
- Mọi remote adapter phải bounded, cancellable, HTTPS-only và trả lỗi an toàn.
- Thay đổi geometry/overlay phải có test tập trung và visual QA ở nhiều DPI.

