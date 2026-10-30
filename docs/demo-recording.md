# Quay video bằng thao tác thật

Nhánh `demo/recordly-showcase` mở giao diện LexVerse gốc và một app nội dung riêng cho từng ca. Không có slide, con trỏ giả hay tự phát. Bạn tự bấm nút, kéo chọn vùng, chọn cửa sổ và bôi đen văn bản. Bản dịch được soạn sẵn, chạy offline và hiện bên trong app nội dung để quay được.

Các app nội dung có tên, biểu tượng và giao diện riêng; không chứa tên LexVerse, nhãn demo hay hướng dẫn quay. Hướng dẫn chỉ nằm trong tài liệu này.

## Mở từng ca

Đóng phiên quay trước khi mở ca mới để F6 không bị tranh chấp. Từ thư mục gốc của repo:

```powershell
.\scripts\record-article.ps1
.\scripts\record-game.ps1 -SkipBuild
.\scripts\record-letters.ps1 -SkipBuild
.\scripts\record-comic.ps1 -SkipBuild
```

Mỗi lệnh mở **một ca riêng**. Lệnh đầu build ứng dụng; `-SkipBuild` dùng bản Debug đã build. Nếu PowerShell chặn script theo policy của máy, có thể chạy trực tiếp:

```powershell
dotnet run --project src/LexVerse.App -- --demo --case article
# Thay article bằng game, letters hoặc comic.
```

Chạy không có `--demo` để mở app dịch thật. Chế độ quay dùng thiết lập và quyền tính năng trong bộ nhớ, không thay tài khoản hoặc cấu hình thường ngày. Mặc định đặt sẵn cặp ngôn ngữ và chế độ OCR phù hợp từng ca; vẫn có thể thay đổi qua giao diện gốc. Với dịch màn hình/popup, đổi ngôn ngữ trong **Cài đặt**. Hai bộ chọn cạnh ô nhập văn bản áp dụng cho dịch văn bản nhập tay như trong app gốc.

## Bố trí và Recordly

### Chỉ mở cửa sổ nội dung, không mở LexVerse

```powershell
# Mở cả bốn cửa sổ (build một lần trước khi mở).
.\scripts\open-recording-content.ps1

# Hoặc chỉ mở các ca được chọn, dùng bản đã build.
.\scripts\open-recording-content.ps1 -Case article,letters -SkipBuild
```

Các cửa sổ chạy độc lập; tự đóng từng cửa sổ khi dùng xong. Không có phiên demo LexVerse điều khiển bản dịch soạn sẵn hoặc F6. Có thể dùng app dịch thật để dịch nội dung các cửa sổ này.

1. Đóng phiên LexVerse thật đang chạy để giải phóng F6. Mở một ca; đợi app nội dung xuất hiện.
2. Ở 1920×1080 hoặc 2560×1440, đặt LexVerse và app nội dung cạnh nhau nếu đủ chỗ; nếu không, chuyển bằng Alt+Tab. Cửa sổ nội dung tự chọn kích thước theo màn hình; vẫn có thể di chuyển, resize và phóng to như cửa sổ bình thường.
3. Trong Recordly chọn quay **toàn màn hình**, bật con trỏ thật. Cần quay được cả thao tác trên LexVerse, bộ chọn vùng/cửa sổ và nội dung được dịch.
4. Quay thử 5–10 giây trước take chính; kiểm tra chữ Nhật/Hàn/Trung, box dịch và popup có trong bản ghi. Font Việt đi kèm ứng dụng; CJK dùng font Windows cục bộ.
5. Giữ kết quả 4–6 giây trước thao tác tiếp theo để người xem đọc được. Tắt thông báo desktop trước khi quay nếu cần.

Không có nút điều khiển quay trong app mẫu. Dùng nút dừng và xóa lớp phủ của LexVerse để thực hiện lại một lượt dịch. Đóng LexVerse sẽ đóng app nội dung của ca đó.

## Ca 1 — Fieldnotes Reader: Anh → Việt, dịch vùng

Lệnh: `scripts/record-article.ps1`. Cặp mặc định: English → Vietnamese, chế độ Tài liệu.

1. Đọc/lướt bài **A quieter way to work**. Cuộn một chút để phần mở bài nằm giữa cửa sổ.
2. Chuyển sang LexVerse, bấm **Vùng**. LexVerse vẫn mở bên dưới lớp chọn; kéo vùng trên bài viết rồi thả chuột.
3. Kéo vùng quanh đoạn “Great tools disappear into your workflow…”; nhả chuột. Chỉ các khối có tâm nằm trong vùng chọn được dịch.
4. Giữ hình 4–6 giây. Cuộn nhẹ: box rời khỏi vùng chọn sẽ ẩn; nội dung mới đi vào vùng được dịch.
5. Trở lại LexVerse, bấm dừng để ẩn bản dịch. Có thể kéo vùng khác để quay lại.
6. Take bổ sung: dùng **Dịch theo app**, chọn **Fieldnotes Reader** và bắt đầu. Cuộn xuống rồi chọn bài **The art of noticing** ở thanh thư viện: nội dung mới được dịch khi phiên vẫn chạy.

Kết quả chính:

> Great tools disappear into your workflow. They help you understand more, without asking you to leave the page.

→ Công cụ tốt hòa vào nhịp làm việc. Chúng giúp bạn hiểu nhiều hơn mà không cần rời khỏi trang đang đọc.

> Keep your focus. Let curiosity lead the way.

→ Giữ trọn sự tập trung. Để trí tò mò dẫn lối.

## Ca 2 — Starfall: Nhật → Anh, dịch theo app

Lệnh: `scripts/record-game.ps1`. Cặp mặc định: Japanese → English, chế độ Hội thoại game.

1. Trên LexVerse bấm **Dịch theo app**, chọn cửa sổ **Starfall**, bấm bắt đầu.
2. Đợi các dòng tiếng Nhật trong tiêu đề phụ, thanh nhiệm vụ, nội dung thẻ và nút điều khiển chuyển sang tiếng Anh; giữ nhiệm vụ đầu 4–6 giây. LYRA, HP, MP và EXP giữ nguyên.
3. Bấm nhiệm vụ **灯台の約束** ở bên trái hoặc nút chuyển nhiệm vụ ở cuối thẻ. Chờ các box của nhiệm vụ mới hiện lần lượt; bản dịch nhiệm vụ cũ phải biến mất.
4. Bấm nút theo dõi nhiệm vụ để có thêm tương tác thật trong khung hình.
5. Dừng bằng nút dừng của LexVerse; tất cả box sẽ ẩn. Bắt đầu lại để quay lượt khác.

Nguồn: 星の谷への旅 → Journey to Starfall Valley.

Nguồn: 夜明けまでに、古い灯台へ向かおう。 → Reach the old lighthouse before dawn.

Nhiệm vụ thứ hai: 灯台の約束 → The Lighthouse Promise.

## Ca 3 — Postbox: Hàn → Việt, popup từ lựa chọn thật

Lệnh: `scripts/record-letters.ps1`. Cặp mặc định: Korean → Vietnamese.

1. Mở thư đầu trong **Postbox**. Kéo chuột bôi đen trọn câu/đoạn: **천천히 가도 괜찮아요. 중요한 건 멈추지 않는 거예요.**
2. Nhấn **F6** khi con trỏ đang ở Postbox. Popup hiện gần phần được chọn, chứa bản dịch và từ vựng.
3. Kéo tiêu đề popup một đoạn ngắn, giữ 4–6 giây, bấm × để đóng. Bấm dừng/xóa lớp phủ trên LexVerse cũng đóng popup đang hiện.
4. Chọn câu **당신의 새로운 시작을 응원해요.**, nhấn F6 để nhận kết quả khác.
5. Take bổ sung: mở thư **주말에 만나요**, bôi đen **토요일 오후에 강변을 함께 걸을까요?**, nhấn F6.

Kết quả chính: “Đi chậm cũng không sao. Điều quan trọng là đừng dừng lại.” Từ vựng: 천천히 — chậm rãi, từ tốn; 멈추다 — dừng lại.

Thư thứ hai: “Chiều thứ Bảy mình cùng đi dạo bên bờ sông nhé?”

Nếu không bôi đen, không xuất hiện bản dịch. Dữ liệu hỗ trợ trọn các câu/đoạn đã soạn và hai câu riêng của đoạn đầu, không dịch tùy ý từng từ. F6 bị app khác giữ sẽ có trạng thái báo trong LexVerse; đóng phiên cũ hoặc đổi shortcut trong Cài đặt.

## Ca 4 — After Rain Reader: Trung → Việt, truyện

Lệnh: `scripts/record-comic.ps1`. Cặp mặc định: Chinese → Vietnamese, chế độ Truyện tranh.

1. Mở trang 01, chuyển sang LexVerse và bấm **Toàn màn hình** hoặc chọn app **After Rain Reader**.
2. Hai bóng thoại hiện bản dịch bên trong khung truyện. Giữ hình 4–6 giây.
3. Bấm **下一页 →** ở thanh dưới. Trang 02 xuất hiện và nhận bản dịch mới; không còn bản dịch của trang 01.
4. Có thể bấm **← 上一页** để quay lại, rồi dừng/xóa lớp phủ trong LexVerse.

Trang 01:

- 你看，第一颗星星！ → Nhìn kìa, ngôi sao đầu tiên!
- 明天也一起看吧。 → Ngày mai mình lại cùng ngắm nhé.

Trang 02:

- 雨终于停了。 → Cuối cùng mưa cũng tạnh rồi.
- 走吧，我们一起回家。 → Đi thôi, mình cùng về nhà nào.

## Giới hạn và kiểm chứng

- Mỗi ca chỉ dịch app nội dung đi kèm và cặp ngôn ngữ được chuẩn bị. Chọn cửa sổ khác hoặc cặp khác sẽ báo rõ, không gọi dịch thật.
- Dịch vùng bám theo khung màn hình đã chọn: khi cuộn, box rời khỏi khung sẽ ẩn và khối mới đi vào khung sẽ được dịch. Muốn dịch toàn bộ trang/bài liên tục, dùng dịch theo app hoặc toàn màn hình.
- Dừng hoặc xóa lớp phủ đều ẩn box/popup và dừng phiên demo. Bắt đầu phiên mới xóa kết quả trước.
- Box/popup nằm trong WebView của app nội dung, không bật khả năng ghi hình cho overlay thật và không chạy OCR thật.
- Nội dung dữ liệu: `src/LexVerse.Desktop.UI/src/samples/content.ts`. Hai trạng thái mỗi app là nội dung độc lập, không có lịch phát tự động.

Kiểm tra tự động từ repo app:

```powershell
# Sau khi build: kiểm tra WebView2 và named pipe của bốn app thật.
node scripts/demo-qa.mjs --native

# Kiểm tra trình duyệt: chạy preview từ thư mục frontend ở một terminal khác.
# npx vite preview --configLoader runner --host 127.0.0.1 --port 5186
node scripts/demo-qa.mjs
```

Script trình duyệt dùng Playwright có sẵn tại `.ui-qa/node_modules` của workspace, hoặc module URL qua `PLAYWRIGHT_MODULE`. Ảnh kiểm tra nằm trong `.tmp/sample-qa/`. Tỷ lệ device scale 1/1.5 kiểm tra bố cục trên trình duyệt; vẫn cần quay thử Recordly và kiểm tra DPI Windows native trước take chính.

Kết quả kiểm chứng bản này: build WPF/frontend thành công, không warning/lỗi compiler; 58/58 test frontend qua. Kiểm tra trình duyệt qua cho bốn app ở device scale 1 và 1.5, cùng kích thước 2560×1440: dịch vùng/cuộn, đổi bài/nhiệm vụ/trang, toàn bộ chữ Nhật hiển thị trong Starfall, nội dung bản dịch mới, popup của hai thư, kéo/đóng, dừng/xóa/bắt đầu lại và không có request tài nguyên ra ngoài. Smoke test native qua cho bốn WebView2 với named pipe thật, cùng khởi động MainWindow gốc và tiến trình nội dung độc lập. Chưa quay video Recordly thực tế hoặc tự động kiểm tra toàn bộ thao tác chuột/phím tắt xuyên qua hai cửa sổ native.
