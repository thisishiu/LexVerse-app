const entries: [string, string, string, string][] = [
  ["Restore shortcut defaults", "Khôi phục phím tắt mặc định", "Restore the default key combinations, then press Save changes to apply.", "Đưa các phím tắt về mặc định, sau đó bấm Lưu thay đổi để áp dụng."],
  ["Review", "Xem lại", "Open Settings to review external text processing consent.", "Mở Cài đặt để xem lại quyền xử lý văn bản bên ngoài."],
  ["External processing is off", "Xử lý bên ngoài đang tắt", "Open Settings and enable external text processing before translating.", "Mở Cài đặt và bật xử lý văn bản bên ngoài trước khi dịch."],
  ["Text translator", "Dịch văn bản", "Enter or paste text, choose the languages, then press Translate.", "Nhập hoặc dán văn bản, chọn ngôn ngữ rồi bấm Dịch."],
  ["Live translation", "Dịch thời gian thực", "Choose an app, region or full screen. Recognized text is translated into an overlay.", "Chọn app, vùng hoặc toàn màn hình. Chữ nhận dạng được sẽ được dịch trên lớp phủ."],
  ["Selected text", "Văn bản đã chọn", "Select text in another app, then use the popup shortcut to translate it.", "Bôi đen chữ trong app khác rồi dùng phím tắt popup để dịch."],
  ["Translate app", "Dịch theo app", "Open the picker, select a window and press Start. Translation follows that window.", "Mở danh sách, chọn cửa sổ và bấm Bắt đầu. Bản dịch theo cửa sổ đã chọn."],
  ["Region", "Vùng", "Drag a rectangle on screen to translate only that area.", "Kéo một hình chữ nhật trên màn hình để chỉ dịch vùng đó."],
  ["Full screen", "Toàn màn hình", "Translate visible text across the screen using the selected OCR mode.", "Dịch chữ hiển thị trên toàn màn hình bằng chế độ OCR đã chọn."],
  ["Stop live translation", "Dừng dịch realtime", "Stop the current screen translation session.", "Dừng phiên dịch màn hình hiện tại."],
  ["Clear overlay", "Xóa lớp phủ", "Remove displayed translation blocks. Use Stop to end the session.", "Xóa các khối bản dịch đang hiện. Dùng Dừng để kết thúc phiên dịch."],
  ["From", "Từ", "Choose the source language, or Auto detect if you are unsure.", "Chọn ngôn ngữ gốc hoặc Tự động nhận diện nếu chưa biết."],
  ["To", "Sang", "Choose the language for your translation.", "Chọn ngôn ngữ bạn muốn nhận bản dịch."],
  ["Swap languages", "Đổi chiều ngôn ngữ", "Swap source and target languages. Choose a specific source language first.", "Đổi ngôn ngữ nguồn và đích. Cần chọn ngôn ngữ nguồn cụ thể trước."],
  ["Copy translation", "Sao chép bản dịch", "Copy the translated text to your clipboard.", "Sao chép bản dịch vào bộ nhớ tạm."],
  ["Translate", "Dịch", "Translate the entered text. External text processing must be enabled in Settings.", "Dịch văn bản đã nhập. Cần bật xử lý văn bản bên ngoài trong Cài đặt."],
  ["Translate selected text", "Dịch văn bản đã chọn", "Read and translate the current text selection using the popup.", "Đọc và dịch đoạn chữ đang bôi đen bằng popup."],
  ["Display language", "Ngôn ngữ hiển thị", "Choose English or Vietnamese for the app interface, then save changes.", "Chọn tiếng Anh hoặc tiếng Việt cho giao diện rồi lưu thay đổi."],
  ["Source language", "Ngôn ngữ nguồn", "Set the default language of the original text.", "Đặt ngôn ngữ mặc định của văn bản gốc."],
  ["Target language", "Ngôn ngữ đích", "Set the default language of translated text.", "Đặt ngôn ngữ mặc định của bản dịch."],
  ["Ignored terms", "Từ bỏ qua", "Enter comma-separated terms such as HP, MP, EXP to keep unchanged.", "Nhập các từ cách nhau bằng dấu phẩy, như HP, MP, EXP, để giữ nguyên."],
  ["Translation instruction", "Văn phong dịch", "Describe the tone and translation rules you want, then save changes.", "Mô tả giọng văn và quy tắc dịch mong muốn rồi lưu thay đổi."],
  ["Preserve acronyms and technical terms", "Giữ acronym và thuật ngữ kỹ thuật", "Keep abbreviations and technical terminology unchanged when translating.", "Giữ nguyên chữ viết tắt và thuật ngữ kỹ thuật khi dịch."],
  ["Close popup on outside click", "Đóng popup khi bấm bên ngoài", "Enable to dismiss the translation popup by clicking outside it.", "Bật để đóng popup dịch khi bấm ra ngoài."],
  ["Allow popup dragging", "Cho phép kéo popup", "Enable to move the translation popup by dragging it.", "Bật để di chuyển popup dịch bằng cách kéo."],
  ["Show popup while translating", "Hiện popup trong khi đang dịch", "Show the popup before the translation finishes so you can see progress.", "Hiện popup trước khi dịch xong để theo dõi quá trình."],
  ["External text processing", "Xử lý văn bản bên ngoài", "Allow selected, typed and OCR text to be sent to the translation service. This setting saves immediately.", "Cho phép gửi văn bản đã chọn, nhập và OCR tới dịch vụ dịch. Tùy chọn này lưu ngay."],
  ["Local performance diagnostics", "Chẩn đoán hiệu năng cục bộ", "Store local timing metrics without text content. This setting saves immediately.", "Lưu chỉ số thời gian cục bộ, không chứa nội dung văn bản. Tùy chọn này lưu ngay."],
  ["Scan interval", "Chu kỳ quét", "Lower values scan more often and use more resources. Save changes to apply.", "Giá trị thấp quét thường xuyên hơn và tốn tài nguyên hơn. Lưu để áp dụng."],
  ["Overlay opacity", "Độ mờ lớp phủ", "Adjust how opaque the translation overlay appears.", "Điều chỉnh độ đậm của lớp phủ bản dịch."],
  ["Text scale", "Cỡ chữ", "Adjust the size of translated text in the overlay.", "Điều chỉnh kích thước chữ dịch trên lớp phủ."],
  ["Overlay palette", "Bảng màu lớp phủ", "Choose dark or light backgrounds for translation blocks.", "Chọn nền tối hoặc sáng cho các khối bản dịch."],
  ["Show OCR debug boxes", "Hiện khung debug OCR", "Show source text boundaries to inspect recognition and alignment.", "Hiện ranh giới chữ gốc để kiểm tra nhận dạng và căn chỉnh."],
  ["Popup translation", "Dịch bằng popup", "Click the shortcut and press a new key combination to translate selected text.", "Bấm phím tắt rồi nhấn tổ hợp mới để dịch chữ đã chọn."],
  ["Region translation", "Dịch theo vùng", "Click the shortcut and press a new key combination to select a translation region.", "Bấm phím tắt rồi nhấn tổ hợp mới để chọn vùng dịch."],
  ["Full-screen realtime", "Dịch toàn màn hình", "Click the shortcut and press a new key combination for full-screen translation.", "Bấm phím tắt rồi nhấn tổ hợp mới để dịch toàn màn hình."],
  ["Stop realtime", "Dừng dịch realtime", "Click the shortcut and press a new key combination to stop translation.", "Bấm phím tắt rồi nhấn tổ hợp mới để dừng dịch."],
  ["Save changes", "Lưu thay đổi", "Apply your settings. Each shortcut must use a different key combination.", "Áp dụng cài đặt. Mỗi phím tắt cần có một tổ hợp khác nhau."],
  ["Help and source", "Trợ giúp và mã nguồn", "Open the project help and source page in your browser.", "Mở trang trợ giúp và mã nguồn của dự án trong trình duyệt."],
  ["Sign in or create account", "Đăng nhập hoặc tạo tài khoản", "Open the browser to sign in or register, then return to LexVerse.", "Mở trình duyệt để đăng nhập hoặc đăng ký rồi quay lại LexVerse."],
  ["Sign out", "Đăng xuất", "End your current account session on this device.", "Kết thúc phiên tài khoản hiện tại trên thiết bị này."],
  ["Retry", "Thử lại", "Start the account sign-in flow again.", "Bắt đầu lại quá trình đăng nhập tài khoản."],
  ["Reopen browser", "Mở lại trình duyệt", "Reopen the current sign-in link in your browser.", "Mở lại liên kết đăng nhập hiện tại trong trình duyệt."],
  ["Copy link", "Sao chép liên kết", "Copy the sign-in link and paste it into your browser.", "Sao chép liên kết đăng nhập rồi dán vào trình duyệt."],
  ["Cancel", "Hủy", "Cancel the pending sign-in attempt.", "Hủy lần đăng nhập đang chờ."],
  ["Sync access", "Đồng bộ", "Refresh your account access after a purchase or plan change.", "Làm mới quyền tài khoản sau khi mua hoặc đổi gói."],
  ["Refresh access", "Làm mới quyền truy cập", "Check the service for your latest payment and access status.", "Kiểm tra trạng thái thanh toán và quyền truy cập mới nhất."],
  ["Continue to checkout", "Tiếp tục thanh toán", "Select a plan and sign in first, then open secure checkout in your browser.", "Chọn gói và đăng nhập trước, sau đó mở trang thanh toán trong trình duyệt."],
  ["Plans", "Các gói", "Choose an available plan. Access updates after payment confirmation.", "Chọn một gói khả dụng. Quyền sử dụng cập nhật sau khi xác nhận thanh toán."],
  ["LexVerse account", "Tài khoản LexVerse", "Sign in to manage your account and purchased access.", "Đăng nhập để quản lý tài khoản và quyền sử dụng đã mua."],
];
function originalText(label: string) {
  const entry = entries.find(([en, vi]) => en === label || vi === label);
  return entry ? entry[entry[1] === label ? 3 : 2] : label;
}
export function helpLabel(label: string) {
  return `${entries.some(entry => entry[1] === label) ? "Hướng dẫn" : "Help"}: ${label}`;
}

export function helpId(label: string) {
  const customLabels = [
    ["About OCR modes", "Hướng dẫn chế độ OCR"],
    ["Payment details", "Thông tin thanh toán"],
    ["Window selection help", "Hướng dẫn chọn cửa sổ"],
    ["Search help", "Hướng dẫn tìm kiếm"],
    ["Refresh help", "Hướng dẫn làm mới"],
    ["Cancel help", "Hướng dẫn hủy"],
    ["Start help", "Hướng dẫn bắt đầu"]
  ];
  const canonical = [...entries, ...customLabels].find(([en, vi]) => en === label || vi === label)?.[0] ?? label;
  return Array.from(canonical).reduce((hash, char) => Math.imul(31, hash) + char.charCodeAt(0) | 0, 0).toString(36);
}


// Explicit outcomes for every boolean option; guidance never toggles it itself.
const toggles: Record<string, [string, string, string, string]> = {
  "External text processing": ["Văn bản đã chọn, nhập và OCR được phép gửi tới dịch vụ dịch.", "Không cho phép gửi văn bản tới dịch vụ dịch; các thao tác dịch cần quyền này sẽ bị chặn.", "Selected, typed and OCR text may be sent to the translation service.", "Text cannot be sent to the translation service; translation actions requiring consent are blocked."],
  "Local performance diagnostics": ["Lưu chỉ số thời gian cục bộ, không chứa nội dung văn bản.", "Không thu thập chỉ số chẩn đoán cục bộ mới.", "Save local timing metrics without text content.", "Stop collecting new local diagnostic metrics."],
  "Preserve acronyms and technical terms": ["Yêu cầu giữ nguyên chữ viết tắt và thuật ngữ kỹ thuật.", "Không áp dụng yêu cầu giữ nguyên bổ sung này.", "Ask the translator to preserve abbreviations and technical terms.", "Do not apply this additional preservation instruction."],
  "Close popup on outside click": ["Bấm ngoài popup để đóng.", "Popup không tự đóng khi bấm ngoài; dùng nút đóng.", "Click outside the popup to dismiss it.", "Outside clicks leave the popup open; use its close button."],
  "Allow popup dragging": ["Kéo thanh tiêu đề để di chuyển popup.", "Không thể kéo popup bằng thanh tiêu đề.", "Drag the title bar to move the popup.", "The popup cannot be moved by dragging its title bar."],
  "Show OCR debug boxes": ["Hiện khung chữ gốc để kiểm tra nhận dạng và căn chỉnh.", "Ẩn các khung kiểm tra OCR.", "Show source text boundaries for recognition and alignment inspection.", "Hide OCR debug boundaries."]
};
export function helpText(label: string, supplied?: string) {
  const entry = entries.find(([en, vi]) => en === label || vi === label);
  const vi = entry ? entry[1] === label : /[À-ỹ]/.test(label);
  const base = supplied ?? originalText(label);
  const toggle = entry && toggles[entry[0]];
  if (toggle) {
    const immediate = entry[0] === "External text processing" || entry[0] === "Local performance diagnostics";
    return vi ? `### Công dụng\n${base}\n\n### Khi bật\n${toggle[0]}\n\n### Khi tắt\n${toggle[1]}\n\n### Cách lưu\n${immediate ? "Lưu ngay sau khi được xác nhận. Không cần bấm Lưu thay đổi." : "Bấm **Lưu thay đổi** để áp dụng."}`
      : `### Purpose\n${base}\n\n### When enabled\n${toggle[2]}\n\n### When disabled\n${toggle[3]}\n\n### Saving\n${immediate ? "Saves immediately after confirmation. No need to press Save changes." : "Press **Save changes** to apply."}`;
  }
  const settings = entry && ["Display language", "Source language", "Target language", "Ignored terms", "Translation instruction", "Scan interval", "Overlay opacity", "Text scale", "Overlay palette", "Popup translation", "Region translation", "Full-screen realtime", "Stop realtime", "Restore shortcut defaults"].includes(entry[0]);
  return vi ? `### Công dụng và cách dùng\n${base}\n\n### Kết quả hoặc lưu ý\n${settings ? "Các thay đổi chỉ áp dụng sau khi bấm **Lưu thay đổi**." : "Trong chế độ hướng dẫn, thao tác này chỉ mở giải thích. Thoát hướng dẫn để sử dụng chức năng."}`
    : `### Purpose and use\n${base}\n\n### Result or note\n${settings ? "Changes apply after pressing **Save changes**." : "In guide mode this opens an explanation only. Exit the guide to use the feature."}`;
}
