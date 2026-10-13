# Trạng thái dự án LexVerse

Đây là tài liệu bàn giao kỹ thuật chuẩn của repository. Hãy đọc file này trước khi thay đổi code hoặc kiến trúc và cập nhật lại sau mỗi thay đổi quan trọng.

## Mốc tham chiếu

- Ngày rà soát: 2026-09-21.
- Nhánh: `feature/hybrid-desktop-ui`.
- Commit làm nguồn sự thật: `27c5ffa` (`docs: document hybrid desktop architecture`).
- Upstream: `origin/feature/hybrid-desktop-ui` tại cùng commit.
- Solution: `LexVerse.slnx`, gồm 10 project .NET; frontend React nằm trong `src/LexVerse.Desktop.UI` và được build bởi `LexVerse.App.csproj`.
- Working tree hiện có các thay đổi tài liệu của lần rà soát này; không có product code nào bị thay đổi.

## Mục tiêu và phạm vi MVP

LexVerse là ứng dụng Windows dịch nội dung trên màn hình theo thời gian gần thực và đặt bản dịch đúng vị trí bằng overlay. MVP công khai chỉ gồm hai workflow miễn phí:

1. Popup/F6 và Quick text.
2. Chọn vùng tài liệu rồi liên tục OCR, dịch và hiển thị overlay.

`MvpProductPolicy.FreeFeatures` cấp miễn phí đúng ba feature key:

- `translation.popup`
- `translation.region`
- `mode.document`

Full screen, Comic/manga, Subtitle, Game dialogue, account, commerce và paid entitlement không thuộc trải nghiệm MVP mặc định.

## Kiến trúc đang chạy

```text
React 19 + TypeScript + Vite
        │ Desktop bridge v1 qua WebView2
        ▼
LexVerse.App — WPF host, native fallback, composition root
        │
        ├── LexVerse.Application — use case, coordinator, privacy, feature gate
        ├── LexVerse.Core — contract, model, pipeline, cache, geometry
        ├── LexVerse.Infrastructure — Windows capture, adapter mạng/lưu trữ
        ├── LexVerse.OCR — Windows OCR
        └── LexVerse.Overlay — WPF overlay
```

| Thành phần | Trách nhiệm hiện tại |
|---|---|
| `src/LexVerse.Desktop.UI` | Control Center React, quick translation, Document-region, privacy, account/plan có điều kiện và Settings route nội bộ. |
| `src/LexVerse.App` | WPF process, WebView2 host, native WPF fallback, F6, selection window, composition và adapter bridge. |
| `src/LexVerse.Application` | Popup use case, realtime coordinator, feature access, commerce orchestration, privacy và telemetry contract. |
| `src/LexVerse.Core` | OCR/translation model, pipeline, cache, geometry, product model và policy độc lập nền tảng. |
| `src/LexVerse.Infrastructure` | Windows Graphics Capture, Win32, Google translator, OAuth/catalog/checkout/entitlement client, DPAPI và file settings/log. |
| `src/LexVerse.OCR` | Adapter Windows OCR và kết quả debug layout. |
| `src/LexVerse.Overlay` | Cửa sổ overlay WPF và chuyển physical pixel sang DIP. |
| `samples/` | Demo và visual/manual test; không phải production composition root. |
| `tests/LexVerse.Translation.Tests` | Test Core, Application, adapter bảo mật, commerce, privacy, telemetry và bridge. |

## Hybrid UI và bridge

- WPF phục vụ asset đã đóng gói qua virtual host `https://app.lexverse.local`.
- Nếu asset hoặc WebView2 Runtime không dùng được, native WPF Control Center vẫn là fallback.
- Navigation ngoài origin, popup, download và permission request bị chặn; DevTools và context menu mặc định bị tắt trong host.
- Bridge dùng envelope JSON version `1`, tối đa 256 KiB, phân biệt hoa thường và từ chối field lạ.
- Native side effect được serialize bằng command gate; frontend dùng timeout 30 giây và nhận event `app.snapshot`.
- React chỉ nhận trạng thái điều khiển đã lấy mẫu. Capture frame, OCR result và overlay frame không đi qua bridge.

Command allow-list hiện có:

```text
app.getSnapshot
translation.translateQuickText
realtime.selectRegion
realtime.start
realtime.stop
settings.update
privacy.updateConsent
account.signIn
account.signOut
commerce.checkout
commerce.restore
external.openHelp
```

## Luồng runtime

Popup/Quick text:

```text
F6 hoặc React
-> PopupTranslationUseCase
-> FeatureGate(translation.popup)
-> selected text hoặc quick text
-> ConsentCheckingTextTranslator
-> DeferredTextTranslator
-> GoogleCloudTextTranslator
-> native popup hoặc snapshot React
```

Document-region:

```text
React command
-> native region selector
-> RealtimeTranslationCoordinator
-> FeatureGate(translation.region)
-> Windows Graphics Capture
-> region/change policy
-> Windows OCR
-> normalize/group
-> InMemoryTranslationCache
-> translate cache miss
-> CoordinateMapper
-> WpfRealtimeOverlayOutput
```

Realtime coordinator sở hữu start/restart/stop, cancellation, cadence, state, telemetry và cleanup. Overlay được giữ native để không truyền dữ liệu khung hình qua frontend.

`mode.document` nằm trong free-feature matrix và xác định phạm vi sản phẩm, nhưng coordinator hiện chỉ demand feature theo capture kind (`translation.region` hoặc `translation.full-screen`); không có lần demand riêng cho processing mode.

## Trạng thái capability

| Capability | Trạng thái | Ghi chú |
|---|---|---|
| Popup/F6, Quick text | Đã triển khai và hiển thị | Miễn phí nhưng vẫn cần consent và provider dịch hoạt động. |
| Document-region | Đã triển khai và hiển thị | Miễn phí; overlay native, OCR cục bộ. |
| React/WebView2 Control Center | Đã triển khai và hiển thị | Có native WPF fallback. |
| Privacy settings, local diagnostics | Đã triển khai và hiển thị | External text processing mặc định tắt. |
| Full screen, Comic, Subtitle, Game | Có code/policy nhưng dormant | Không thuộc UI MVP mặc định; không được coi là release-ready. |
| OAuth, catalog, checkout/restore | Client extension point có điều kiện | Chỉ bật khi đủ cấu hình HTTPS; backend không có trong repo. |
| ES256 signed entitlement | Client verifier/cache có điều kiện | Issuer, key rotation và entitlement service là external. |
| Remote telemetry | Chưa triển khai | Hiện chỉ có local content-free telemetry. |
| Translation gateway | Chưa triển khai | Production app hiện tạo `GoogleCloudTextTranslator` trực tiếp. |
| MSIX/App Installer và release workflow | Đã có automation | Chưa có bằng chứng signing/host/clean-VM production thật. |

## Giới hạn production quan trọng

- `GoogleCloudTextTranslator` dùng Google Cloud Translation V2 và Application Default Credentials. Package Windows sạch chưa thể dịch nếu người dùng không có credential phù hợp. Không được đóng gói service-account secret vào client.
- Repository không chứa OAuth server, account API, catalog authority, payment webhook/ledger, entitlement issuer, quota service hoặc Translation Gateway.
- Account/commerce UI chỉ ẩn khi cấu hình thiếu; việc có client adapter không chứng minh backend tồn tại.
- `TranslationPromptOptions.Instruction` chưa được Google provider sử dụng như một AI prompt.
- Chưa có clean-VM evidence cho WebView2 dependency, signed install/update/rollback, accessibility, multi-monitor DPI và capture exclusion.

## Quy tắc kỹ thuật

- UI chỉ thu thập intent và render state; không đặt capture/OCR loop, billing rule hoặc provider construction trong React.
- Contract và logic độc lập mode ở Core; orchestration ở Application; Windows/external adapter ở Infrastructure.
- Giữ luồng `capture -> region -> trigger -> OCR -> normalize -> cache -> translate -> coordinate map -> overlay`.
- Tách rõ frame pixel, physical screen pixel và WPF DIP.
- Không gửi text OCR, bản dịch, clipboard, pixel, tọa độ, title, token hoặc identifier vào telemetry.
- Mọi remote call phải có HTTPS, giới hạn kích thước, deadline, cancellation và lỗi ổn định.

## Kiểm chứng tại lần cập nhật tài liệu

Kết quả đã chạy lại trên working tree hiện tại:

- `npm run check`: pass; TypeScript typecheck và Vitest pass 1 test file, 2/2 test.
- `dotnet test tests/LexVerse.Translation.Tests/LexVerse.Translation.Tests.csproj --configuration Release`: pass 95/95 test, 0 warning.
- Parse `data-mxgraph`, JSON và embedded `mxfile` XML: pass; có đúng 3 trang với lần lượt 31, 46 và 25 `mxCell`.
- `git diff --check`: pass; chỉ có cảnh báo line-ending LF → CRLF của working copy trên Windows.
- Visual smoke cho tab/zoom/layout chưa chạy vì không có browser tích hợp khả dụng trong môi trường hiện tại; cần mở file HTML thủ công trước khi merge.

## Việc tiếp theo được khuyến nghị

1. Chốt ADR cho Translation Gateway để package sạch không phụ thuộc Google credential của máy người dùng.
2. Chạy manual E2E cho Popup và Document-region ở 100–200% DPI, multi-monitor và các ứng dụng nguồn phổ biến.
3. Kiểm tra foreground WebView2/native fallback bằng keyboard, Narrator, High Contrast và capture exclusion.
4. Chuẩn bị certificate, HTTPS release host và clean-VM Beta rehearsal trước khi tuyên bố release-ready.
5. Chỉ kích hoạt account/PRO sau khi có backend authority, webhook reconciliation, signed entitlement issuer và sandbox E2E.


## Integration update — 2026-10-12

Merged dev into feature/ui-japanese-translation. Preserved Japanese OCR Latin/ruby filters, rotated overlays, DPI mapping, text layout, Comic mode, native UI and sample redesign. Added hybrid React/WebView2 UI and authenticated server translation from dev. Production translation requires LEXVERSE_TRANSLATION_ENDPOINT, account tokens and entitlements; the legacy free-feature list is no longer applied by AppCompositionRoot. Comic availability therefore depends on server entitlements. Local HTTP is restricted to explicitly enabled development loopback. Google translation remains available in samples.

Backups: backup/dev-before-japanese-merge-20261012 and backup/japanese-before-dev-merge-20261012. Remote branch has not been pushed. Verification: solution build passed (12 projects, zero warnings/errors); OCR tests 43 passed; translation tests 105 passed; frontend TypeScript and 2 tests passed. Manual Windows OCR/overlay checks remain necessary.

## UI redesign update - 2026-10-12

Nhánh `feature/ui-japanese-translation` đã được fast-forward tới `fc4b17f` trước khi làm lại `src/LexVerse.Desktop.UI`. Diagram nguồn trên diagrams.net (page `m7TsgaFc5hTRnL7zU8nb`) được dùng như sơ đồ nghiệp vụ cho đăng nhập, refresh session, checkout, webhook và entitlement; UI không tạo form email/password giả vì bridge hiện chỉ hỗ trợ đăng nhập PKCE qua trình duyệt bảo mật.

Frontend hiện có ba route hash độc lập:

- `#/workspace`: quick text, lựa chọn ngôn ngữ, live region, trạng thái OCR/performance và F6 popup.
- `#/account`: signed-in/signed-out, offer từ catalog, checkout, sign-out và restore/sync entitlement.
- `#/settings`: consent xử lý text bên ngoài, local diagnostics, ngôn ngữ mặc định và help/source.

Thiết kế mới dùng control-center sáng, sidebar responsive, panel bán kính tối đa 8 px và icon `lucide-react`; đã loại bỏ icon mojibake cùng theme editorial tối trước đó. React vẫn chỉ gửi intent và render snapshot. Allow-list, payload và ownership của WPF/WebView2 bridge không đổi. Query `?preview=ready` và `?preview=signed-in` chỉ tạo snapshot demo khi không có native WebView transport, phục vụ kiểm thử UI tĩnh; khi chạy trong app, snapshot native luôn là nguồn sự thật.

`DesktopBridge.connect()` cho phép đăng ký lại native message listener sau cleanup của React StrictMode; promise bị hủy sau cleanup không còn ghi lỗi vào component đã unmount.

Xác minh trên working tree này:

- `npm run check`: pass, TypeScript và Vitest 2 file / 5 test.
- `npm run build`: pass; production bundle gồm CSS riêng và JS React/WebView.
- `npm audit`: 0 vulnerability sau khi cập nhật transitive dev dependency `source-map-js` lên 1.2.2.
- `dotnet build src/LexVerse.App/LexVerse.App.csproj -c Release --no-restore`: pass, 0 warning / 0 error; frontend được build và đóng gói trong host.
- `dotnet test tests/LexVerse.OCR.Tests/LexVerse.OCR.Tests.csproj -c Release --no-build --no-restore`: pass 43/43.
- Full `dotnet build LexVerse.slnx -c Release` build được app nhưng dừng tại `LexVerse.Translation.Tests`: thiếu sibling fixture `..\lexverse-server\test\fixtures\node-entitlement.json`. Không tạo entitlement fixture giả trong desktop repo.
- `git diff --check`: pass; quét mojibake trong `src/LexVerse.Desktop.UI/src`: sạch.
- Visual QA production bundle: pass ở Workspace 1440x900, Account signed-in 1440x900, Settings 1100x760 và Workspace sidebar thu gọn 800x700; không thấy overlap, tràn chữ hoặc icon lỗi.
