# Product Backlog: đưa LexVerse Demo lên Production

## 1. Mục tiêu sản phẩm

Phát hành một ứng dụng Windows ổn định cho hai luồng miễn phí cốt lõi:

1. Dịch văn bản được bôi chọn bằng Popup/F6 và Quick text.
2. Chọn một vùng tài liệu và liên tục hiển thị bản dịch bằng overlay.

Tài khoản, gói trả phí, thanh toán, quota và các chế độ Full screen/Comic/Subtitle/Game là giai đoạn sau MVP. Các extension point có thể tồn tại trong mã nguồn nhưng không được làm cản trở trải nghiệm miễn phí khi production chưa cấu hình commerce.

Backlog này được xây dựng từ:

- `docs/architecture/LexVerse-diagram.drawio.html` với các luồng đăng ký/đăng nhập, refresh token, entitlement, trust boundary, kiến trúc local–cloud, quota, dữ liệu và thanh toán webhook.
- `docs/MVP_PRODUCTION_SCOPE.md`, `docs/PRODUCTION_ARCHITECTURE.md` và trạng thái hiện tại trong `docs/PROJECT_STATE.md`.

## 2. Những gì đã có trong baseline

Không đưa lại các phần sau thành hạng mục “xây mới”; backlog chỉ giữ việc kiểm thử, hoàn thiện hoặc tích hợp production còn thiếu:

- WPF production shell và composition root.
- Popup/F6, Quick text và Document-region realtime pipeline.
- Capture → OCR → chuẩn hóa → cache → dịch → ánh xạ tọa độ → overlay.
- Consent trước khi gửi nội dung ra dịch vụ ngoài; local diagnostics có thể tắt.
- Feature gate, OAuth/PKCE, catalog, checkout/restore client và ES256 signed entitlement client-side ở trạng thái extension point.
- Crash redaction, operational telemetry cục bộ, startup/memory budget.
- CI build/test, đóng gói MSIX/App Installer, workflow tạo signed release candidate và tài liệu rehearsal.
- Accessibility baseline, keyboard navigation và hỗ trợ DPI hiện tại.

## 3. Quy ước backlog

| Thuộc tính | Ý nghĩa |
|---|---|
| P0 | Bắt buộc trước public MVP |
| P1 | Sau khi MVP miễn phí được xác nhận; cần cho tài khoản/PRO |
| P2 | Mở rộng sản phẩm và tối ưu tăng trưởng |
| SP | Story point tương đối: 1, 2, 3, 5, 8, 13 |
| Trạng thái | `TODO`, `VERIFY` (đã có nền tảng nhưng chưa có bằng chứng production), `DEFERRED` |

## 4. Các quyết định kiến trúc cần chốt trước khi triển khai

| ID | Ưu tiên | Hạng mục | Tiêu chí nghiệm thu | SP | Trạng thái |
|---|---|---|---|---:|---|
| ADR-01 | P0 | Chốt đường đi của nội dung dịch trong production | Có ADR so sánh desktop gọi provider trực tiếp và Cloud Translation Gateway; phương án được chọn không phát hành service-account/API secret trong client; mô tả timeout, quota, chi phí và dữ liệu gửi đi | 3 | TODO |
| ADR-02 | P0 | Chốt dữ liệu cloud dùng PostgreSQL hay MongoDB | Chọn một hệ quản trị làm source of truth; cập nhật sơ đồ/schema/migration plan; loại bỏ mâu thuẫn PostgreSQL–MongoDB trong sơ đồ hiện tại | 2 | TODO |
| ADR-03 | P0 | Chốt free MVP không tài khoản | API contract cho Popup/Document hoạt động không đăng nhập; quy định rate limit/chống lạm dụng; account và PRO không xuất hiện khi chưa cấu hình | 3 | TODO |
| ADR-04 | P1 | Chốt ranh giới dịch vụ backend | Có quyết định modular monolith hay tách Auth/Entitlement/Billing/Usage; nêu ownership dữ liệu, transaction boundary và hướng scale | 3 | DEFERRED |

## 5. Backlog P0 — Public MVP miễn phí

### Epic P0-A — Translation service dùng được cho khách hàng

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| MVP-001 | Là người dùng mới, tôi có thể dịch mà không tự cài Google credential | Bản MSIX sạch thực hiện được Popup và Document translation; không có secret/provider credential trong package, log hoặc process command line | ADR-01, ADR-03 | 13 | TODO |
| MVP-002 | Xây Translation Gateway hoặc adapter production theo ADR-01 | Nhận batch text, source/target language và request ID; có giới hạn kích thước/count; timeout/cancellation; trả lỗi ổn định; chỉ gateway giữ provider secret | ADR-01 | 8 | TODO |
| MVP-003 | Thêm production translator adapter trong desktop | Cấu hình endpoint theo environment/channel; retry có giới hạn và jitter; không retry lỗi không an toàn; Popup và realtime dùng chung consent boundary | MVP-002 | 5 | TODO |
| MVP-004 | Kiểm soát abuse và chi phí cho luồng miễn phí | Có per-IP/per-install rate limit, request size limit, concurrency limit và budget alert; khi hết hạn mức UI báo có thể phục hồi, không crash | MVP-002 | 5 | TODO |
| MVP-005 | Hoàn thiện chính sách cache | Cache key gồm text chuẩn hóa, source, target và mode; có TTL/size bound; không ghi nội dung nhạy cảm xuống telemetry; test cache hit khi zoom/pan/quay lại đoạn cũ | MVP-003 | 3 | VERIFY |

### Epic P0-B — Trải nghiệm Popup và Document hoàn chỉnh

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| MVP-006 | Hoàn thiện first-run và consent | Người dùng hiểu nội dung nào rời thiết bị, provider/mục đích/retention; từ chối không gửi request; thu hồi consent dừng realtime ngay; lựa chọn tồn tại qua restart | MVP-003 | 5 | VERIFY |
| MVP-007 | Kiểm thử E2E Popup/F6 trên ứng dụng phổ biến | Pass trên trình duyệt, Office/PDF reader và ứng dụng text chuẩn; clipboard được khôi phục khi có thể; không selection có thông báo rõ; hotkey conflict có hướng xử lý | MVP-003 | 5 | VERIFY |
| MVP-008 | Kiểm thử E2E Document-region | Chọn vùng ở 100%, 125%, 150%, 200% DPI và multi-monitor; overlay đúng tọa độ, chỉ nằm trong vùng; ẩn khi app nguồn mất focus/minimize; Stop giải phóng tài nguyên | MVP-003 | 8 | VERIFY |
| MVP-009 | Giảm flicker và ổn định bố cục overlay | Box ổn định khi text không đổi; không nhảy vị trí vượt ngưỡng đã định; palette và cỡ chữ vẫn dễ đọc; có checklist visual QA được ký duyệt | MVP-008 | 5 | VERIFY |
| MVP-010 | Thông báo lỗi có khả năng phục hồi | Mất mạng, provider timeout/429/5xx, thiếu OCR language pack, capture permission và clipboard failure đều có message hành động được; retry/stop không cần restart app | MVP-003 | 5 | VERIFY |
| MVP-011 | Hoàn thiện settings production | Đổi ngôn ngữ nguồn/đích, shortcut, ignored terms, font/overlay và privacy được lưu theo schema version; cấu hình hỏng quay về default an toàn; migration có test | — | 5 | VERIFY |
| MVP-012 | Hoàn thiện localization tiếng Việt/Anh | Không còn mojibake/hard-coded fallback sai ngôn ngữ; mọi chuỗi chính có resource; layout không vỡ với chuỗi dài; fallback locale xác định | MVP-011 | 3 | TODO |

### Epic P0-C — Bảo mật, riêng tư và pháp lý

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| MVP-013 | Threat model cho desktop và Translation Gateway | Bao phủ capture/clipboard, local files, HTTPS API, provider, update channel và supply chain; có trust boundary đúng như sơ đồ; mọi rủi ro cao có owner/mitigation | ADR-01 | 5 | TODO |
| MVP-014 | Hardening giao tiếp mạng | Chỉ HTTPS; hostname/certificate validation chuẩn; deadline và response-size bound; không URL credential; log không chứa token/text; test malformed/oversized response | MVP-002, MVP-003 | 5 | TODO |
| MVP-015 | Rà soát secret và artifact | Quét source, history phù hợp, package và CI artifact; không có `.env`, service-account JSON, PFX/password, token hay endpoint nội bộ nhạy cảm | — | 3 | TODO |
| MVP-016 | Chuẩn bị Privacy Policy, Terms và data disclosure | Link mở được từ app/installer; mô tả screen text gửi đi đâu, local diagnostics, retention, quyền xóa dữ liệu và kênh liên hệ; nội dung khớp hành vi thực tế | MVP-006 | 5 | TODO |
| MVP-017 | Security/dependency release gate | NuGet/action audit không có lỗ hổng High/Critical chưa được chấp nhận; dependency lock/pinning hợp lý; findings có SLA và owner | — | 3 | VERIFY |

### Epic P0-D — Chất lượng, hiệu năng và khả năng vận hành

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| MVP-018 | Chạy full Release test gate | Build warnings-as-errors và toàn bộ automated tests pass trên commit RC; kết quả được lưu cùng artifact | Các P0 code item | 2 | VERIFY |
| MVP-019 | Thiết lập performance baseline thực tế | Startup ≤ 5 giây, mục tiêu ≤ 3 giây; initial working set ≤ 250 MiB; đo p50/p95 capture, OCR, translation, total frame trên máy thấp/trung bình | MVP-003 | 5 | VERIFY |
| MVP-020 | Soak test realtime | Chạy Document-region liên tục ít nhất 2 giờ; không tăng memory/handle/thread không giới hạn; Stop/Close hoàn tất có thời hạn; có báo cáo và biểu đồ | MVP-008 | 5 | TODO |
| MVP-021 | Accessibility certification nội bộ | Keyboard-only, Narrator, High Contrast, text scaling, 200% DPI, minimum window; không focus trap; status quan trọng được đọc; mọi lỗi P0/P1 được sửa | MVP-012 | 5 | VERIFY |
| MVP-022 | Local diagnostics và support bundle an toàn | Người dùng bật/tắt được; export chỉ chứa schema cho phép, không text/tokens/path nhạy cảm; file bounded/rotated; có hướng dẫn gửi support | — | 5 | VERIFY |
| MVP-023 | Crash/recovery validation | Unhandled UI/background exception không làm lộ dữ liệu; lần chạy sau mở được; file settings/cache hỏng không chặn startup; crash fingerprint đủ để nhóm lỗi | — | 3 | VERIFY |

### Epic P0-E — Đóng gói, phát hành và hỗ trợ

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| MVP-024 | Cấu hình signing identity và protected environments | Certificate hợp lệ, Code Signing EKU, timestamp; PFX chỉ vào signing job sau approval; cleanup luôn chạy; publisher khớp manifest | — | 5 | TODO |
| MVP-025 | Dựng HTTPS release host cho Beta/Stable | MSIX, checksum và App Installer có URI/MIME đúng; artifact versioned bất biến; live pointer cập nhật sau cùng; có retention policy | MVP-024 | 5 | TODO |
| MVP-026 | Chạy signed release-candidate workflow | Commit reachable từ `main`; build/test/audit/startup/package/sign/verify/attestation đều pass; metadata chứa commit, run và SHA-256 | MVP-024, MVP-025 | 3 | TODO |
| MVP-027 | Clean-VM Beta rehearsal | Pass install, first run, consent, Popup, Document-region, DPI, accessibility, update, rollback, repair và uninstall trên VM 100%/200% DPI; lưu bằng chứng | MVP-026 | 8 | TODO |
| MVP-028 | Hoàn thiện update và rollback runbook | Roll forward/back bằng App Installer version mới; settings tương thích; xác định rollback owner và thời gian quyết định; không ghi đè MSIX versioned | MVP-027 | 3 | TODO |
| MVP-029 | Thiết lập kênh hỗ trợ và incident playbook | Có support contact, mẫu thu thập dữ liệu an toàn, phân loại severity, SLA phản hồi, kill switch/config rollback và owner trực | MVP-022 | 5 | TODO |
| MVP-030 | Public Beta go/no-go | Không còn defect P0/P1; ký duyệt Product/Engineering/Security/Privacy/QA; release notes và known issues công khai; có kế hoạch đo 7 ngày đầu | MVP-001…029 | 2 | TODO |

## 6. Backlog P1 — Account, PRO, entitlement và billing

Chỉ bắt đầu activation UI của nhóm này sau khi MVP miễn phí ổn định. Client extension point đã có nhưng backend production trong sơ đồ chưa tồn tại.

### Epic P1-A — Cloud foundation và identity

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| PRO-001 | Dựng Cloud API sau gateway/load balancer | IaC cho dev/staging/prod; health/readiness; autoscale; TLS; rate limit; correlation ID; zero-downtime deployment | ADR-04 | 13 | DEFERRED |
| PRO-002 | Tạo schema và migration database | Có users, devices, refresh sessions, plans, features, plan features, subscriptions, payments, usage records, webhook events, audit logs; index/unique/FK rõ; backup/restore test | ADR-02 | 8 | DEFERRED |
| PRO-003 | Đăng ký tài khoản an toàn | Email được chuẩn hóa; password policy và hash hiện đại; duplicate email không rò rỉ thông tin quá mức; tạo FREE plan idempotent; rate limit | PRO-001, PRO-002 | 8 | DEFERRED |
| PRO-004 | Đăng nhập và refresh-token rotation | Access token ngắn hạn; refresh token chỉ lưu hash server-side; device/session binding; revoke/reuse detection; 401 nhất quán | PRO-003 | 8 | DEFERRED |
| PRO-005 | Xác minh email, quên/đặt lại mật khẩu | Token một lần, có expiry, chống enumeration và rate limit; email template production; audit event; session cũ được thu hồi khi cần | PRO-003 | 8 | DEFERRED |
| PRO-006 | Quản lý thiết bị và phiên đăng nhập | Người dùng xem/revoke phiên; giới hạn thiết bị theo policy; last-seen không chứa dữ liệu thừa; sign-out all devices hoạt động | PRO-004 | 5 | DEFERRED |
| PRO-007 | Xóa tài khoản và dữ liệu | Luồng xác nhận/re-auth; retention/anonymization theo policy; subscription được xử lý; audit có kiểm soát; có bằng chứng hoàn tất | PRO-003 | 8 | DEFERRED |

### Epic P1-B — Catalog, entitlement và quota

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| PRO-008 | Dịch vụ catalog công khai | Trả plans/offers có revision; validation currency/period/trial; cache/CDN phù hợp; client không dùng giá để cấp quyền | PRO-001, PRO-002 | 5 | DEFERRED |
| PRO-009 | Dịch vụ resolve entitlement | Từ subscription + plan features tạo grants theo subject; fail closed; response bounded; expiry ngắn; revision thay đổi khi revoke | PRO-002 | 8 | DEFERRED |
| PRO-010 | Ký ES256 entitlement và rotation key | Private key ở KMS/HSM; `keyId` rotation có overlap; client pin public key; test unknown/expired/tampered/subject mismatch | PRO-009 | 8 | DEFERRED |
| PRO-011 | Startup refresh/offline entitlement E2E | Refresh token → access token → `/me/entitlements` → signed envelope; offline cache tối đa 72 giờ; account switch không dùng cache cũ | PRO-004, PRO-010 | 5 | DEFERRED |
| PRO-012 | Kiểm tra quota nguyên tử | Token/user/plan/feature hợp lệ mới được dùng; concurrent requests không vượt quota; usage ghi idempotent; deny reason rõ; timezone/period reset test | PRO-009 | 8 | DEFERRED |
| PRO-013 | Kích hoạt Account & plan UI | Chỉ hiện khi cấu hình backend hợp lệ; PKCE system browser; DPAPI session; sign-out khóa quyền ngay; accessibility pass | PRO-004, PRO-008, PRO-011 | 5 | DEFERRED |

### Epic P1-C — Billing và subscription

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| PRO-014 | Tạo hosted checkout | Client chỉ gửi `offerId`; backend đọc giá authoritative; idempotency key chống double checkout; URL HTTPS ngắn hạn, không credential | PRO-004, PRO-008 | 8 | DEFERRED |
| PRO-015 | Xử lý payment webhook | Verify chữ ký trước khi xử lý; event ID unique; duplicate/out-of-order được xử lý idempotent; redirect success không bao giờ tự cấp PRO | PRO-014 | 8 | DEFERRED |
| PRO-016 | Purchase ledger và subscription state machine | Lưu payment/subscription transactionally; bao phủ active, trialing, past_due, canceled, expired, refunded; entitlement phản ánh trạng thái | PRO-015, PRO-009 | 13 | DEFERRED |
| PRO-017 | Restore/reconciliation | Authenticated restore đối soát với provider; chạy lặp không tạo trùng; xử lý delayed webhook/refund/cancel; sau đó refresh signed entitlement | PRO-016 | 8 | DEFERRED |
| PRO-018 | Sandbox payment E2E | Test success, cancel, duplicate click, webhook retry, delayed webhook, refund, renewal, expiry, account thứ hai và thiết bị thứ hai | PRO-014…017 | 8 | DEFERRED |
| PRO-019 | Billing operations | Dashboard/runbook tra cứu bằng opaque ID, retry DLQ, reconciliation định kỳ, alert mismatch và quy trình hoàn tiền có audit | PRO-016 | 8 | DEFERRED |

### Epic P1-D — Production operations cho cloud

| ID | User story / công việc | Tiêu chí nghiệm thu | Phụ thuộc | SP | Trạng thái |
|---|---|---|---|---:|---|
| PRO-020 | Observability không thu thập nội dung | Metrics latency/error/saturation/cost; trace theo correlation ID; log không text/token/PII không cần thiết; dashboard và alert có owner | PRO-001 | 8 | DEFERRED |
| PRO-021 | Backup, restore và disaster recovery | Xác định RPO/RTO; backup mã hóa; restore rehearsal; key/secret recovery; runbook và owner | PRO-002 | 5 | DEFERRED |
| PRO-022 | Load/capacity/cost test | Đạt tải mục tiêu với p95 SLO; autoscale không phá quota/idempotency; có cost per active user/request và budget alert | PRO-012, PRO-020 | 8 | DEFERRED |
| PRO-023 | Pen test và abuse test | Bao phủ auth, OAuth callback, IDOR, replay, webhook forgery, quota race, SSRF/open redirect, key rotation; High/Critical được đóng | PRO-018 | 8 | DEFERRED |

## 7. Backlog P2 — Mở rộng sau product-market validation

| ID | Hạng mục | Tiêu chí nghiệm thu cấp cao | SP | Trạng thái |
|---|---|---|---:|---|
| FUT-001 | Full-screen translation | Multi-monitor chính xác, policy OCR riêng, feature key/quota riêng, không tự OCR UI LexVerse | 8 | DEFERRED |
| FUT-002 | Comic/manga mode | Gom text theo bubble; reading order; all-caps không bị coi là acronym; visual QA trên bộ ảnh chuẩn | 13 | DEFERRED |
| FUT-003 | Subtitle/movie mode | Change detection, debounce và placement riêng; ưu tiên latency; không để subtitle cũ tồn tại | 13 | DEFERRED |
| FUT-004 | Game dialogue mode | Region/trigger/cache/prompt riêng; glossary/ignored terms; frame budget và hotkey phù hợp game | 13 | DEFERRED |
| FUT-005 | AI translation/prompt-aware provider | Thực sự dùng `TranslationPromptOptions.Instruction`; safety/cost/privacy evaluation; fallback provider | 8 | DEFERRED |
| FUT-006 | AI annotation cho Important terms | Provider-backed contextual explanation; Wikipedia/local fallback; citation/source và latency budget | 8 | DEFERRED |
| FUT-007 | Remote telemetry opt-in | Consent riêng, schema allowlist, retention/deletion, batching/backoff và privacy review trước khi bật | 8 | DEFERRED |

## 8. Thứ tự triển khai đề xuất

### Milestone 0 — Production decisions

- Hoàn tất ADR-01, ADR-02 và ADR-03.
- Đặt KPI/SLO, owner và môi trường Beta.
- Kết quả: đội có một target architecture duy nhất và không còn phụ thuộc credential cục bộ của developer.

### Milestone 1 — Internal Alpha

- MVP-001 đến MVP-017.
- Kết quả: package cài sạch dùng được với dịch vụ production/staging, hai workflow cốt lõi hoàn chỉnh, privacy/security được duyệt.

### Milestone 2 — Signed Beta

- MVP-018 đến MVP-029.
- Kết quả: signed artifact, clean-VM rehearsal, update/rollback và support readiness có bằng chứng.

### Milestone 3 — Public MVP

- MVP-030 và theo dõi 7 ngày đầu.
- Chỉ mở Stable khi không còn lỗi P0/P1, chi phí nằm trong budget và rollback đã được thử thật.

### Milestone 4 — PRO platform

- PRO-001 đến PRO-023.
- Bật Account/Plan UI sau khi identity, webhook-authoritative billing, signed entitlement và restore E2E đều đạt gate.

## 9. Definition of Ready

Một backlog item chỉ được kéo vào sprint khi:

- Có owner sản phẩm và owner kỹ thuật.
- Có acceptance criteria đo được, dependency và môi trường kiểm thử.
- Với hạng mục gửi dữ liệu ra ngoài: đã xác định loại dữ liệu, consent và retention.
- Với hạng mục billing/entitlement: source of truth và idempotency key đã rõ.
- Với thay đổi overlay/capture: có bộ tình huống visual QA và DPI mục tiêu.

## 10. Definition of Done

- Code review hoàn tất; build Release không warning.
- Unit/integration/E2E phù hợp pass; không gọi provider thật ngoài test được chủ động cho phép.
- Có timeout, cancellation, bounded input/output và user-safe error cho mọi remote call.
- Không secret, token, source text hoặc PII ngoài allowlist xuất hiện trong log/artifact.
- Tài liệu vận hành/cấu hình/migration được cập nhật.
- Nếu ảnh hưởng UI: keyboard, Narrator, High Contrast, 100% và 200% DPI được kiểm tra.
- Nếu ảnh hưởng release: signed artifact, checksum, signature/timestamp và provenance được xác minh.
- Product Owner chấp nhận theo acceptance criteria và không còn defect P0/P1 liên quan.

## 11. Release gates tối thiểu cho Public MVP

1. Người dùng trên Windows sạch cài và chạy được mà không cần .NET runtime hay Google credential thủ công.
2. Popup/F6, Quick text và Document-region đều miễn phí, không yêu cầu account/upgrade.
3. Không có text nào rời máy trước consent; thu hồi consent dừng realtime.
4. Mất mạng/provider/OCR/capture không làm app crash và có đường phục hồi rõ.
5. Startup ≤ 5 giây, initial working set ≤ 250 MiB; soak test không có leak không giới hạn.
6. Build/test/security audit/signature/timestamp/hash/attestation đều pass trên đúng commit phát hành.
7. Install/update/rollback/repair/uninstall pass trên VM 100% và 200% DPI.
8. Không còn defect P0/P1; có rollback owner, support owner và known-issues list.

## 12. Các rủi ro lớn cần theo dõi

| Rủi ro | Tác động | Giảm thiểu trong backlog |
|---|---|---|
| Desktop hiện tạo `GoogleCloudTextTranslator` trực tiếp và dựa vào Application Default Credentials | Khách hàng không thể dùng package sạch hoặc secret bị đóng gói sai cách | ADR-01, MVP-001…004 |
| Sơ đồ dùng cả PostgreSQL và MongoDB | Schema, migration và vận hành bị phân mảnh | ADR-02, PRO-002 |
| Sơ đồ auth-first khác với quyết định free MVP | Có thể vô tình khóa Popup/Document sau login/paywall | ADR-03 và release gate số 2 |
| Redirect thanh toán bị hiểu nhầm là bằng chứng mua hàng | Cấp quyền gian lận | PRO-015…018; chỉ webhook đã verify mới cập nhật subscription |
| OCR/overlay đúng trên máy dev nhưng lệch trên DPI/multi-monitor | Trải nghiệm production không dùng được | MVP-008, MVP-009, MVP-021, MVP-027 |
| Có workflow release nhưng chưa có certificate/host/clean VM thật | Không có bằng chứng artifact phát hành an toàn | MVP-024…028 |
| Telemetry/crash log vô tình chứa nội dung màn hình | Rủi ro riêng tư cao | MVP-013…017, MVP-022…023 |
