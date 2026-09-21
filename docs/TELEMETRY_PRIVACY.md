# Operational telemetry cục bộ và privacy

Control dành cho người dùng và consent gửi text được mô tả trong [PRIVACY_SETTINGS.md](PRIVACY_SETTINGS.md).

## Trạng thái hiện tại

LexVerse chỉ có local operational telemetry. Không có upload endpoint hoặc remote adapter. Record mới có thể bị tắt ngay từ Control Center; preference được kiểm tra trước mỗi event.

## Data boundary

`OperationalEvent` là contract strongly typed, không chứa dictionary, arbitrary string payload hoặc object mở rộng. Schema cho phép:

- event/outcome/failure enum;
- correlation ID ngẫu nhiên chỉ tồn tại trong một realtime session;
- capture kind và processing mode;
- stage duration, block/cache/frame count và budget state;
- module count và process working-set size.

Schema không cho phép:

- OCR/selected/typed/translated text;
- clipboard, prompt hoặc provider response;
- screen pixel, coordinate hoặc window title;
- file path, account/device identifier hoặc token;
- raw exception message hoặc stack trace.

Crash record chỉ giữ exception/inner type, HRESULT, timestamp và SHA-256 fingerprint để group lỗi mà không lưu raw stack/message.

## Lưu trữ và backpressure

- Event đi qua bounded in-memory channel với drop-oldest policy.
- Background writer batch/flush mà không block WPF hoặc realtime loop.
- Log JSONL nằm dưới `%LOCALAPPDATA%\LexVerse\Logs`.
- Mỗi file tối đa 1 MiB và giữ tối đa ba rotated file.
- Shutdown cố flush trong deadline hữu hạn; telemetry failure là fail-open đối với translation.

Realtime ghi frame đầu, mỗi frame thứ 20 và mọi frame vượt budget. Session-closing event giữ tổng frame và tổng violation để tránh ghi từng frame.

## Warning budget hiện tại

| Mode | Capture | OCR | Translation | Tổng |
|---|---:|---:|---:|---:|
| Document/default | 120 ms | 900 ms | 1.500 ms | 2.000 ms |
| Subtitle | 100 ms | 650 ms | 1.200 ms | 1.600 ms |
| Game dialogue | 100 ms | 650 ms | 1.200 ms | 1.600 ms |
| Comic/manga | 150 ms | 1.200 ms | 1.800 ms | 2.500 ms |

Budget chỉ tạo warning/metric; không hủy translation. CI có gate riêng: Control Center responsive trong 5 giây, mục tiêu 3 giây và initial working set tối đa 250 MiB.

## Quy tắc mở rộng

- Không thêm free-form payload vào `OperationalEvent`.
- Không dùng stable device/account ID làm correlation ID.
- Không log error body từ provider hoặc token endpoint.
- Thêm field mới phải có privacy review và serialization test.
- Remote enablement cần consent riêng, documented processor/retention/deletion, TLS, rate limit và server-side schema enforcement.

