# Protocol signed entitlement phía desktop

## Trạng thái và authority

Client verifier, HTTPS provider và signed offline cache đã được triển khai. Entitlement API, issuer, private key, purchase source of truth và key-rotation operation là **external/deferred** và không nằm trong repository này.

Catalog chỉ mô tả plan/offer; chỉ signed entitlement hợp lệ mới cấp paid feature. Free MVP vẫn do `MvpProductPolicy` cấp độc lập.

## Envelope

API trả tối đa 256 KiB UTF-8 JSON:

```json
{
  "schema": "lexverse.entitlements.v1",
  "keyId": "prod-2026-07",
  "algorithm": "ES256",
  "payload": "base64url(raw UTF-8 payload JSON)",
  "signature": "base64url(64-byte IEEE-P1363 signature)"
}
```

`signature` là ECDSA P-256/SHA-256 trên **đúng decoded payload bytes**, không phải JSON được serialize lại. Client chọn pinned public key theo `keyId`; private key không được xuất hiện trong desktop, source, log hoặc CI artifact.

Payload:

```json
{
  "schemaVersion": 1,
  "subjectId": "opaque-account-subject",
  "revision": "monotonic-or-unique-revision",
  "issuedAtUtc": "2026-07-20T12:00:00Z",
  "notBeforeUtc": "2026-07-20T12:00:00Z",
  "expiresAtUtc": "2026-07-23T12:00:00Z",
  "grants": [{
    "feature": "translation.full-screen",
    "startsAtUtc": null,
    "endsAtUtc": null,
    "source": "subscription"
  }]
}
```

Verifier từ chối unknown field/schema/algorithm/key/subject, chữ ký sai, dữ liệu quá giới hạn, claim malformed/future/expired và envelope dài hơn 72 giờ. Mỗi grant bị clamp theo `expiresAtUtc`, kể cả khi `endsAtUtc` là `null`.

## Online và offline

- Remote request chỉ dùng HTTPS và có deadline tám giây bao phủ header/content.
- Verified online response dùng được ngay; việc ghi cache là best effort.
- Exact signed envelope được lưu atomically dưới `%LOCALAPPDATA%\LexVerse\Entitlements`.
- Offline read áp dụng lại cùng size limit, signature/key/subject/time validation.
- Không có unsigned format và không có grace period sau expiry.
- Account switch/sign-out invalidate cache trong bộ nhớ; envelope subject khác không được tái sử dụng.

## Cấu hình desktop

- `LEXVERSE_ENTITLEMENT_ENDPOINT`: absolute HTTPS URI.
- `LEXVERSE_ENTITLEMENT_KEY_ID`: pinned public-key identifier.
- `LEXVERSE_ENTITLEMENT_PUBLIC_KEY_PEM`: P-256 public key PEM; chấp nhận escaped `\n`.

Subject và bearer token lấy từ OAuth account session. Thiếu hoặc sai cấu hình không chặn startup; paid capability fail closed trong khi free MVP tiếp tục hoạt động.

## Yêu cầu cho service external

- Authenticate caller và derive `subjectId` phía server.
- Tính grant từ purchase/subscription authority; không tin plan, price hoặc feature do client gửi.
- Giữ private key trong KMS/HSM hoặc secret boundary tương đương.
- Ship public key mới trong client trước khi issuer chuyển `keyId`; cho phép overlap khi rotation.
- Rate-limit issuance, tạo revision có thể audit và không ghi token/PII/content không cần thiết.
- Revoke bằng authoritative state và envelope lifetime ngắn; test tamper, expiry, unknown key, wrong subject và account switch.

