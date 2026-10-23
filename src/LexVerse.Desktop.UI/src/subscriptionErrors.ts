const messages: Record<string, [string, string]> = {
  "subscription-sign-in-required": ["Your session could not be verified. Sign in again, then retry sync.", "Không xác thực được phiên đăng nhập. Hãy đăng nhập lại rồi đồng bộ."],
  "subscription-endpoint-unavailable": ["The subscription endpoint is unavailable. Check the server version and configuration, then retry.", "API gói chưa khả dụng. Hãy kiểm tra phiên bản và cấu hình server rồi thử lại."],
  "subscription-timeout": ["Sync timed out. Check your connection and retry.", "Đồng bộ quá thời gian chờ. Hãy kiểm tra kết nối rồi thử lại."],
  "subscription-invalid-response": ["The server returned invalid plan data. Please retry or contact support.", "Server trả dữ liệu gói không hợp lệ. Hãy thử lại hoặc liên hệ hỗ trợ."],
  "subscription-unavailable": ["Could not load your plan. Check your connection and retry sync.", "Không tải được gói. Hãy kiểm tra kết nối rồi đồng bộ lại."]
};

export function subscriptionErrorMessage(code: string, vietnamese: boolean) {
  return (messages[code] ?? messages["subscription-unavailable"])[vietnamese ? 1 : 0];
}
