# Dịch theo app

Triển khai server hỗ trợ `translation.window` trước desktop. Quyền này độc lập với
dịch vùng/toàn màn hình và không nằm trong quyền miễn phí mặc định. Quản trị viên
bật quyền trong gói bằng quy tắc áp dụng gói hiện hành; không cần migration dữ liệu.

Trong khu vực dịch trực tiếp, chọn **Dịch theo app**, chọn một cửa sổ rồi **Bắt đầu**.
**Làm mới** hủy lựa chọn cũ. **Dừng** kết thúc phiên. Không lưu lựa chọn qua lần mở app.
Không bao gồm popup hay cửa sổ khác của cùng ứng dụng.

Capture dùng Windows Graphics Capture trực tiếp trên HWND đã kiểm tra danh tính.
Không có fallback sang capture màn hình. Khi cửa sổ bị che, OCR tiếp tục nếu Windows
cung cấp frame; overlay đặt ngay trên cửa sổ đích và cắt vùng bị cửa sổ phía trước che.
Vùng cắt dùng hình chữ nhật cửa sổ, nên cửa sổ trong suốt/không chữ nhật có thể khiến
một phần overlay bị ẩn nhiều hơn vùng che thực tế.

Thu nhỏ/ẩn tạm dừng OCR và xóa frame overlay cũ; khôi phục lấy frame mới. Đóng cửa sổ
hủy xử lý và giải phóng capture. Khi resize/đổi DPI, kết quả cũ bị loại. Di chuyển
cùng kích thước/DPI cập nhật vị trí overlay độc lập với nhịp OCR.

## Kiểm tra Windows trước phát hành

Popup chọn cửa sổ hiển thị lưới ảnh thật, cập nhật các thẻ đang nhìn thấy sau mỗi
đợt chụp khoảng 2 giây. Ảnh JPEG tối đa 480×270 được giữ trong bộ nhớ, không lưu
file hay gửi tới OCR/dịch vụ dịch. Hai cửa sổ được chụp đồng thời; mỗi cửa sổ có
timeout 1 giây. Cửa sổ thu nhỏ, chặn capture hoặc ảnh trống hiện placeholder;
cửa sổ đã đóng bị khóa lựa chọn. Cập nhật ảnh không đổi lựa chọn hiện tại.

Bridge bổ sung `realtime.getWindowPreviews` với `{ sessionId, windowIds }`
(tối đa 12 ID trong một đợt); trả `{ id, status, imageDataUrl }` cho từng cửa sổ,
với status `available`, `unavailable` hoặc `closed`. `realtime.cancelWindowPreviews`
nhận `{ sessionId }`, hủy phiên khi đóng/làm mới popup. Các lệnh preview không giữ
hàng đợi lệnh dịch; ID cửa sổ vẫn được kiểm tra lại trước khi bắt đầu dịch.

Mỗi trang có một nút i ở góc phải dưới để bật chế độ giải thích. Các vùng có
hướng dẫn được đánh dấu và chỉ mở chú thích khi bấm, kể cả tùy chọn đang bị khóa.
Nút i hoặc Esc thoát; chuyển trang/tab cũng thoát. Popup dùng nút i riêng và Esc
thoát giải thích trước khi đóng popup. Bridge `app.setExplanationMode` nhận
`{ active }` để chặn phím tắt popup native khi app đang có focus. Animation chuyển
chế độ tuân theo tùy chọn giảm chuyển động của hệ thống.

Các ca nghiệm thu bổ sung:

- Kiểm tra một nút i trên từng trang và trong popup, bằng cả tiếng Việt/Anh.
- Bật giải thích, bấm nút, nhập liệu, dùng bàn phím; không phát sinh lệnh hay đổi cài đặt.
- Kiểm tra Esc hai lần trong popup và chuyển tab/trang khi đang giải thích.
- Mở nhiều cửa sổ cùng app; kiểm tra ảnh đúng cửa sổ khi bị che, resize và đổi DPI.
- Làm mới/đóng popup khi chụp đang chờ; không hiện ảnh cũ, không giữ capture nền.

- Dùng hai app có chữ khác nhau; che một phần/toàn bộ app đích. Xác nhận không dịch
  chữ app phía trước và bản dịch/khung debug không đè lên app đó.
- Alt-Tab, mở popup của app đích và đổi thứ tự cửa sổ; kiểm tra click xuyên và không lấy focus.
- Di chuyển liên tục, resize, maximize, chuyển màn hình DPI 100%/150% và tọa độ âm.
- Minimize/restore, ẩn/hiện và đóng app khi OCR/dịch đang chờ; không hiện frame cũ.
- Thử app chặn capture: báo lỗi phù hợp và không chuyển sang OCR màn hình.
- Dừng/đổi target nhiều lần; kiểm tra không còn overlay, capture hay tác vụ của phiên cũ.
- Kiểm tra lại dịch vùng/toàn màn hình, quota, quyền và đồng ý xử lý văn bản bên ngoài.

Các ca kiểm tra trực tiếp trên desktop tương tác cần được thực hiện riêng;
unit test không xác nhận được compositor, z-order và DPI thực tế của Windows.
