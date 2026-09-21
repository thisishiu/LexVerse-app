# Product Backlog: đưa LexVerse lên Production

## 1. Mục tiêu

Phát hành hai workflow Windows miễn phí:

1. Popup/F6 và Quick text.
2. Document-region OCR, dịch và native overlay.

Account/PRO, Full screen, Comic, Subtitle và Game là sau MVP. Trạng thái code chuẩn nằm ở [PROJECT_STATE.md](PROJECT_STATE.md); acceptance chuẩn nằm ở [MVP_PRODUCTION_SCOPE.md](MVP_PRODUCTION_SCOPE.md).

## 2. Baseline đã có

- React/Vite Control Center chạy trong WebView2 và native WPF fallback.
- Bridge v1 bounded/strict/allow-listed với snapshot event.
- Popup/F6, Quick text và Document-region end-to-end.
- Capture → OCR → normalize/group → cache → translate → coordinate map → overlay.
- Consent boundary dùng chung, local diagnostics có thể tắt và crash redaction.
- Free feature matrix, conditional account/commerce client và signed-entitlement verifier/cache.
- CI, MSIX/App Installer automation, signed-candidate workflow và release rehearsal checklist.

Baseline không bao gồm Translation Gateway, backend account/payment/entitlement/quota, production signing infrastructure hoặc clean-VM evidence.

## 3. Quy ước trạng thái

| Trạng thái | Ý nghĩa |
|---|---|
| `TODO` | Chưa có implementation/evidence cần thiết. |
| `VERIFY` | Code nền tảng đã có nhưng cần test hoặc evidence production. |
| `DONE` | Đã có code và automated evidence phù hợp trong repo. |
| `DEFERRED` | Không thuộc MVP hiện tại. |

Story point chỉ là ước lượng tương đối: 1, 2, 3, 5, 8, 13.

## 4. Quyết định kiến trúc

| ID | Hạng mục | Tiêu chí nghiệm thu | SP | Trạng thái |
|---|---|---|---:|---|
| ADR-01 | Chọn đường translation production | So sánh direct provider và gateway; không ship secret; chốt timeout/quota/cost/privacy/failure contract | 3 | `TODO` |
| ADR-02 | Chốt free MVP không account | API/rate-limit/abuse policy cho Popup/Document; account UI không xuất hiện khi unconfigured | 3 | `VERIFY` |
| ADR-03 | Ranh giới backend PRO | Ownership cho identity, catalog, billing, entitlement, quota và transaction; không chọn DB khi chưa có backend repo | 3 | `DEFERRED` |

Không còn ADR chọn PostgreSQL hay MongoDB trong backlog desktop. Database là quyết định của backend sau khi ownership và workload rõ ràng.

## 5. P0 — Public MVP

### P0-A: Translation production

| ID | Công việc | Tiêu chí nghiệm thu | SP | Trạng thái |
|---|---|---|---:|---|
| MVP-001 | Package sạch dịch được | Popup/Document hoạt động không cần người dùng cài ADC; không có provider secret trong package/log/artifact | 13 | `TODO` |
| MVP-002 | Translation Gateway hoặc adapter đã chốt | Bounded batch, source/target, request ID, deadline, cancellation, stable error và provider credential chỉ ở trusted boundary | 8 | `TODO` |
| MVP-003 | Desktop production translator | Cùng consent boundary, endpoint theo channel, bounded retry và lỗi recoverable | 5 | `TODO` |
| MVP-004 | Abuse/cost control | Rate/concurrency/size/quota limit, budget alert và UI recovery khi hết hạn mức | 5 | `TODO` |
| MVP-005 | Cache policy production | Key có normalized text/source/target/mode/prompt; size bound và test hit/miss/zoom/pan | 3 | `VERIFY` |

### P0-B: Trải nghiệm và accessibility

| ID | Công việc | Tiêu chí nghiệm thu | SP | Trạng thái |
|---|---|---|---:|---|
| MVP-006 | First-run và consent | Giải thích provider/data/retention; deny không gửi; revoke dừng realtime; persist qua restart | 5 | `VERIFY` |
| MVP-007 | Popup/F6 E2E | Browser, Office/PDF, clipboard restore, no-selection, hotkey conflict và cancellation | 5 | `VERIFY` |
| MVP-008 | Document-region E2E | 100/125/150/200% DPI, multi-monitor, correct bounds, Stop/failure cleanup | 8 | `VERIFY` |
| MVP-009 | Overlay stability | Không stale/flicker vượt ngưỡng; palette/font/placement có visual evidence | 5 | `VERIFY` |
| MVP-010 | Recovery UX | Network/timeout/429/5xx/OCR pack/capture/clipboard đều có stable actionable error | 5 | `VERIFY` |
| MVP-011 | Settings production | Language/shortcut/ignored terms/overlay/privacy persist và migrate theo version | 5 | `VERIFY` |
| MVP-012 | Localization Việt/Anh | Không mojibake, không hard-coded fallback sai locale, layout chịu chuỗi dài | 3 | `TODO` |
| MVP-013 | Hybrid accessibility | React và native fallback pass keyboard, Narrator, High Contrast, scaling và focus | 5 | `VERIFY` |

### P0-C: Security và privacy

| ID | Công việc | Tiêu chí nghiệm thu | SP | Trạng thái |
|---|---|---|---:|---|
| MVP-014 | Threat model desktop + gateway | Capture/clipboard/WebView bridge/local files/API/provider/update/supply chain có owner và mitigation | 5 | `TODO` |
| MVP-015 | Network hardening | HTTPS, bounded body/header, deadline, cancellation, no URL credential và malformed-response tests | 5 | `VERIFY` |
| MVP-016 | Secret/artifact review | Source/history/package/CI không có `.env`, service-account JSON, PFX, token hoặc shared secret | 3 | `TODO` |
| MVP-017 | Privacy/Terms disclosure | Nội dung gửi đi, local diagnostics, retention, quyền và contact khớp hành vi thật | 5 | `TODO` |
| MVP-018 | Dependency/security gate | Không có High/Critical chưa chấp nhận; lock/pinning và remediation SLA rõ | 3 | `VERIFY` |

### P0-D: Chất lượng và release

| ID | Công việc | Tiêu chí nghiệm thu | SP | Trạng thái |
|---|---|---|---:|---|
| MVP-019 | Release build/test gate | Frontend check, Release build warnings-as-errors và full automated test pass trên RC | 2 | `VERIFY` |
| MVP-020 | Performance baseline | Startup ≤5 giây, mục tiêu ≤3 giây; initial working set ≤250 MiB; stage p50/p95 | 5 | `VERIFY` |
| MVP-021 | Realtime soak | Document-region ≥2 giờ, không tăng memory/handle/thread vô hạn, cleanup có hạn | 5 | `TODO` |
| MVP-022 | Safe support diagnostics | Export allow-listed, bounded, không content/token/path nhạy cảm | 5 | `TODO` |
| MVP-023 | Signing và release host | Certificate/timestamp/publisher, protected environment, HTTPS URI/MIME và immutable artifact | 8 | `TODO` |
| MVP-024 | Signed candidate | Build/test/audit/startup/package/sign/verify/attestation pass trên commit reachable từ `main` | 3 | `TODO` |
| MVP-025 | Clean-VM rehearsal | Install/consent/Popup/Document/DPI/accessibility/update/rollback/repair/uninstall có evidence | 8 | `TODO` |
| MVP-026 | Public Beta go/no-go | Không defect P0/P1; owner Security/Privacy/QA/Support/Rollback ký duyệt | 2 | `TODO` |

## 6. P1 — Account, PRO và commerce

Client extension point đã có nhưng mọi service dưới đây là external/deferred.

| ID | Công việc | Tiêu chí nghiệm thu cấp cao | SP | Trạng thái |
|---|---|---|---:|---|
| PRO-001 | Identity/OAuth service | Public native client + PKCE, token rotation/revocation và account profile authority | 13 | `DEFERRED` |
| PRO-002 | Catalog authority | Versioned plans/offers; price chỉ là display metadata phía client | 5 | `DEFERRED` |
| PRO-003 | Signed entitlement issuer | ES256, subject binding, KMS/HSM, rotation overlap và ≤72 giờ offline envelope | 8 | `DEFERRED` |
| PRO-004 | Atomic quota/usage | Concurrent request không vượt quota; idempotent usage và stable denial | 8 | `DEFERRED` |
| PRO-005 | Hosted checkout | Client chỉ gửi `offerId`; backend reload authoritative offer và dùng idempotency | 8 | `DEFERRED` |
| PRO-006 | Webhook + purchase ledger | Verify signature; duplicate/out-of-order/refund/renewal được reconcile transactionally | 13 | `DEFERRED` |
| PRO-007 | Restore E2E | Multi-device/account, delayed webhook, refund/cancel và entitlement refresh | 8 | `DEFERRED` |
| PRO-008 | Kích hoạt Account UI | Chỉ bật khi service production sẵn sàng; DPAPI/session/accessibility pass | 5 | `DEFERRED` |
| PRO-009 | Cloud operations | Backup/restore, observability không content, capacity/cost và incident runbook | 13 | `DEFERRED` |

## 7. P2 — Mở rộng mode

| ID | Hạng mục | Tiêu chí nghiệm thu cấp cao | SP | Trạng thái |
|---|---|---|---:|---|
| FUT-001 | Full screen | Multi-monitor/DPI, policy/budget riêng và không capture UI LexVerse | 8 | `DEFERRED` |
| FUT-002 | Comic/manga | Bubble grouping/reading order/all-caps và visual benchmark | 13 | `DEFERRED` |
| FUT-003 | Subtitle/movie | Debounce/latency/placement và stale-subtitle cleanup | 13 | `DEFERRED` |
| FUT-004 | Game dialogue | Region/trigger/glossary/prompt/frame budget riêng | 13 | `DEFERRED` |
| FUT-005 | Prompt-aware translator | Dùng thật `TranslationPromptOptions.Instruction`, privacy/cost/fallback | 8 | `DEFERRED` |
| FUT-006 | Remote telemetry opt-in | Consent riêng, allow-list, retention/deletion và regional review | 8 | `DEFERRED` |

## 8. Thứ tự thực hiện

1. Chốt ADR-01 và hoàn thành MVP-001…004.
2. Thu thập manual E2E/accessibility/performance evidence cho workflow đã có.
3. Hoàn thiện signing, release host và clean-VM rehearsal.
4. Public Beta chỉ khi mọi P0 gate có evidence trên đúng candidate.
5. Chỉ bắt đầu activation PRO sau khi identity, webhook-authoritative billing và signed entitlement E2E đều đạt.

## 9. Definition of Done

- Code review hoàn tất; Release build không warning.
- Test phù hợp pass; real provider chỉ được gọi khi chủ động cho phép credential/cost.
- Input/output/deadline/cancellation/error của remote call đều bounded.
- Không secret, token, source text hoặc PII ngoài allow-list trong log/artifact.
- Docs, config contract, migration/runbook và diagram được cập nhật.
- UI change có keyboard/Narrator/High Contrast/DPI evidence.
- Release change có signature/timestamp/hash/provenance và clean-VM evidence.

