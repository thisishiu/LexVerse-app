const messages: Record<string, [string, string]> = {
  "translation-network-unavailable": ["Cannot connect to the translation server. Check your connection and try again.", "Không kết nối được máy chủ dịch. Kiểm tra mạng rồi thử lại."],
  "translation-timeout": ["Translation timed out. Please try again later.", "Dịch quá thời gian chờ. Vui lòng thử lại sau."],
  "translation-quota-exceeded": ["Your translation quota is exhausted. Check your plan or wait for the next period.", "Đã hết quota dịch. Kiểm tra gói hoặc chờ kỳ tiếp theo."],
  "translation-rate-limited": ["Too many translation requests. Wait and try again.", "Yêu cầu dịch quá nhanh. Vui lòng chờ rồi thử lại."],
  "translation-access-denied": ["You do not have access to this translation feature. Check your plan and refresh access.", "Bạn chưa có quyền dùng tính năng dịch này. Kiểm tra gói và đồng bộ quyền."],
  "translation-sign-in-required": ["Your session is invalid. Please sign in again.", "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại."],
  "translation-consent-required": ["External text processing is off. Enable it in Settings to translate.", "Xử lý văn bản bên ngoài đang tắt. Bật trong Cài đặt để dịch."],
  "translation-service-unavailable": ["The translation service is unavailable. Please try again later.", "Dịch vụ dịch đang gặp sự cố. Vui lòng thử lại sau."],
  "capture-failed": ["Capture failed. This app may block capture or Windows may not support it. Check permissions and select the target again.", "Không chụp được nội dung. App có thể chặn chụp hoặc Windows không hỗ trợ. Kiểm tra quyền và chọn lại đối tượng dịch."],
  "window-unavailable": ["The selected window is no longer available. Refresh the list and select it again.", "Cửa sổ đã chọn không còn khả dụng. Làm mới danh sách và chọn lại."],
  "ocr-failed": ["Text recognition failed. Check the installed OCR language and try again.", "Không nhận dạng được chữ. Kiểm tra ngôn ngữ OCR đã cài rồi thử lại."],
  "selection-failed": ["Cannot read selected text. Select text in an app with the same permissions as LexVerse.", "Không đọc được văn bản đã chọn. Chọn lại văn bản trong ứng dụng có cùng quyền với LexVerse."],
  "translation-unknown": ["The cause of the translation failure is unknown. Try again; contact support if it continues.", "Chưa xác định được nguyên nhân lỗi dịch. Vui lòng thử lại; liên hệ hỗ trợ nếu lỗi tiếp diễn."]
};

export function translationErrorMessage(code: string, vietnamese: boolean): string {
  return (messages[code] ?? messages["translation-unknown"])[vietnamese ? 1 : 0];
}

export function isTranslationError(code: string): boolean {
  return code in messages;
}
