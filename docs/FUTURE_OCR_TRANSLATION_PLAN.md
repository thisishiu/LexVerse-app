# Kế hoạch OCR và dịch sau MVP

Tài liệu này chỉ mô tả hướng mở rộng sau khi Popup/Quick text và Document-region đạt tiêu chí production trong [MVP_PRODUCTION_SCOPE.md](MVP_PRODUCTION_SCOPE.md). Hiện trạng chuẩn nằm ở [PROJECT_STATE.md](PROJECT_STATE.md).

## Nền tảng đã có

LexVerse đã có pipeline end-to-end, không còn ở trạng thái OCR demo:

```text
Windows Graphics Capture
-> chọn OCR region
-> frame-change/trigger policy
-> Windows OCR
-> normalize và group text block
-> translation cache
-> external translation cho cache miss
-> map frame pixel sang physical screen
-> native WPF overlay
```

Các contract/policy đã tồn tại:

- `IOcrRegionProvider`, `IOcrTriggerPolicy`, `OcrProcessingMode` và `RealtimeTranslationOptions`.
- `IOcrService`, `IOcrTextNormalizer`, layout grouping và comic speech-bubble grouping dựa trên text geometry.
- `ITextTranslator`, `ITranslationCache`, `TranslationCacheKey` và `InMemoryTranslationCache`.
- `CoordinateMapper`, `OverlayRenderFrame` và `RealtimeTranslationCoordinator`.
- Policy/budget cho Document, FullScreen, Comic, Subtitle và GameDialogue.

Production UI hiện chỉ expose Document-region. Việc có option/policy cho mode khác không đồng nghĩa các mode đó đã sẵn sàng phát hành.

## Nguyên tắc mở rộng

- Mỗi mode được phép có region, trigger, cadence, grouping, cache, prompt và overlay policy riêng.
- Shared contract không được hard-code hành vi Document.
- Ưu tiên selected region thay vì full screen để giảm OCR cost và tránh bỏ lỡ thay đổi nhỏ.
- Cache key tối thiểu phải chứa normalized text, source language, target language, mode và prompt key.
- Overlay phải giữ coordinate-space rõ ràng giữa frame pixel, physical screen pixel và DIP.
- Mỗi mode mới cần automated test cho policy/geometry/cache và visual dataset riêng.

## Hướng phát triển theo mode

### Full screen

Trạng thái: pipeline option và feature key đã có; dormant ngoài MVP.

- Xác định đúng monitor và multi-monitor DPI.
- Có cadence/filter mạnh hơn selected region.
- Không OCR cửa sổ LexVerse; xác minh display-affinity fallback trên các bản Windows hỗ trợ.
- Đặt memory/latency budget và soak-test riêng.

### Comic/manga

Trạng thái: text-geometry grouping đã có; image-based balloon segmentation chưa có.

- Duy trì one-bubble/one-overlay và reading order.
- Không coi toàn bộ text viết hoa là acronym cần giữ nguyên.
- Bổ sung dataset cho bubble lệch, lồng nhau, không đều và nhiều cột.
- Chỉ thêm image segmentation nếu geometry grouping không đạt benchmark.

### Subtitle/movie

Trạng thái: option/budget đã có; UX và E2E chưa có.

- Region nhỏ, cadence nhanh, debounce theo nội dung.
- Xóa subtitle cũ đúng lúc và tránh flicker khi frame thay đổi nhẹ.
- Đo capture-to-overlay p50/p95 trên video thật.

### Game dialogue

Trạng thái: option/budget đã có; UX và E2E chưa có.

- Region/trigger theo hộp thoại và hotkey phù hợp game.
- Glossary/ignored terms và prompt riêng theo trò chơi.
- Kiểm tra exclusive/full-screen behavior, focus và anti-cheat compatibility trước khi phát hành.

## Provider dịch tương lai

Google Cloud Translation V2 hiện không thực thi `TranslationPromptOptions.Instruction` như AI prompt. Provider prompt-aware tương lai phải:

- dùng contract hiện có thay vì bypass consent/cache;
- có bounded batch, timeout, cancellation và stable error code;
- định nghĩa privacy/retention/cost/fallback;
- tách provider credential khỏi desktop package;
- test cache key khi prompt/style thay đổi.

Translation Gateway cho free MVP là khoảng trống production ưu tiên cao hơn các mode sau MVP.

## Thứ tự đề xuất

1. Hoàn thiện Translation Gateway và evidence cho hai workflow MVP.
2. Thiết lập image/text benchmark, latency budget và visual acceptance cho từng mode.
3. Đưa Full screen vào internal test trước vì pipeline gần hoàn chỉnh nhất.
4. Hoàn thiện Comic geometry benchmark rồi mới quyết định segmentation.
5. Thử Subtitle và Game với policy độc lập, không thêm conditional lớn vào UI/pipeline chung.
6. Chỉ expose mode khi feature gate, settings, accessibility, telemetry và clean-VM E2E đều đạt.

