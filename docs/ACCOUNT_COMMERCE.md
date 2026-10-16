# Tích hợp account và commerce phía desktop

## Trạng thái và ranh giới

Repository hiện có **client extension point** cho account/commerce. Các adapter chỉ hoạt động khi đủ cấu hình HTTPS và không thuộc trải nghiệm free MVP mặc định. Repository này không chứa OAuth server, account API, catalog authority, payment backend, webhook processor hoặc purchase ledger.

Thiếu hoặc sai cấu hình đưa app về explicit free mode; Popup và Document-region vẫn không yêu cầu account.

## Đăng nhập native bằng OAuth

`OAuthPkceAccountSessionProvider` dùng trình duyệt hệ thống và polling HTTPS với PKCE S256:

1. Tạo verifier ngẫu nhiên 32 byte và gửi challenge S256 tới `POST /oauth/desktop/requests`.
2. Giữ `request_secret` trong bộ nhớ, mở `verification_uri` trong trình duyệt.
3. Người dùng đăng nhập, xác minh email và xác nhận tài khoản bằng nút kết nối.
4. Poll endpoint tạo yêu cầu + `/token` mỗi 3 giây, gửi secret và verifier; tăng chờ khi `slow_down`, 429 hoặc lỗi mạng tạm thời.
5. Nhận token một lần, lấy profile và lưu DPAPI. Hủy hoặc hết hạn 15 phút sẽ dừng polling.

Không mở TCP listener, không dùng callback localhost và không cần đăng ký giao thức Windows.

Access token được refresh trước expiry một phút. Refresh token và profile tối thiểu được mã hóa bằng Windows DPAPI cho current user và lưu atomically tại `%LOCALAPPDATA%\LexVerse\Account\session.dat`. Sign-out xóa store và invalidate paid access trong bộ nhớ ngay lập tức.

Biến môi trường:

- `LEXVERSE_OAUTH_DESKTOP_REQUEST_ENDPOINT`
- `LEXVERSE_OAUTH_TOKEN_ENDPOINT`
- `LEXVERSE_ACCOUNT_ENDPOINT`
- `LEXVERSE_OAUTH_CLIENT_ID`

Endpoint phải là absolute HTTPS URI không chứa credential. Đặt `LEXVERSE_OAUTH_DESKTOP_REQUEST_ENDPOINT=https://lexverse.world/oauth/desktop/requests`; endpoint refresh vẫn là `/oauth/token`. Không có client secret cố định trong desktop. Server trả URL trình duyệt cùng origin với endpoint tạo yêu cầu.

Token response dùng field chuẩn:

```json
{
  "access_token": "opaque-token",
  "refresh_token": "opaque-refresh-token",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

Account endpoint trả bounded JSON:

```json
{
  "subjectId": "opaque-stable-account-id",
  "displayName": "Reader"
}
```

`subjectId` phải do server tạo và trùng subject được ký trong entitlement; không dùng display name làm identity.

## Catalog từ xa

`LEXVERSE_CATALOG_ENDPOINT` trả metadata để hiển thị, không cấp quyền:

```json
{
  "revision": "catalog-42",
  "publishedAtUtc": "2026-07-20T12:00:00Z",
  "plans": [{
    "id": "pro",
    "displayName": "LexVerse Pro",
    "features": ["translation.full-screen"],
    "isPublic": true
  }],
  "offers": [{
    "id": "pro-monthly-vnd",
    "planId": "pro",
    "amount": 99000,
    "currencyCode": "VND",
    "billingPeriod": { "count": 1, "unit": "Month" },
    "trialPeriod": { "count": 7, "unit": "Day" },
    "isPublic": true
  }]
}
```

Client giới hạn response/count/length và map vào `ProductCatalog`. Catalog, price hoặc UI state không bao giờ tự cấp feature access.

## Checkout và restore

Biến môi trường:

- `LEXVERSE_CHECKOUT_ENDPOINT`
- `LEXVERSE_RESTORE_ENDPOINT`
- `LEXVERSE_ENTITLEMENT_ENDPOINT`

Checkout gửi authenticated POST với `Idempotency-Key` mới và chỉ có `offerId`:

```json
{ "offerId": "pro-monthly-vnd" }
```

Backend external phải đọc lại offer/price authoritative, bind checkout với subject và trả short-lived HTTPS hosted-checkout URI. Desktop không gửi giá và redirect thành công không phải bằng chứng mua hàng.

Restore gửi authenticated POST với object rỗng. Backend external phải reconcile provider state idempotently, sau đó client refresh signed entitlement. Khi refresh lỗi, account có thể vẫn signed in nhưng feature trả phí phải fail closed nếu không có signed cache còn hạn.

## Điều kiện trước khi kích hoạt UI

- OAuth registration và endpoint production đã deploy.
- Catalog/checkout/restore có authority, rate limit, deadline và audit không chứa token/PII thừa.
- Payment webhook được verify và xử lý idempotent; refund/renewal/expiry được reconcile.
- Entitlement issuer và key rotation hoạt động end-to-end.
- Sandbox test bao phủ duplicate click, delayed webhook, account switch, restore thiết bị thứ hai và token revocation.
- Account UI pass keyboard, Narrator, DPI và shutdown-cancellation.

Không chọn database hoặc service topology trong tài liệu client này.

