using System.ComponentModel;
using System.Net;
using LexVerse.Application.Privacy;
using LexVerse.Application.Product;
using LexVerse.Core.Pipeline;

namespace LexVerse.Application.Diagnostics;

public sealed class TranslationFailureException(string code, Exception? inner = null) : Exception(code, inner)
{
    public string Code { get; } = code;
}

public static class TranslationFailure
{
    public static string Code(Exception exception) => exception switch
    {
        TranslationFailureException failure => failure.Code,
        PipelineStageException stage => stage.Stage + "-failed",
        RemoteProcessingConsentRequiredException => "translation-consent-required",
        FeatureAccessDeniedException => "translation-access-denied",
        TimeoutException or OperationCanceledException => "translation-timeout",
        Win32Exception => "selection-failed",
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => "translation-sign-in-required",
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "translation-access-denied",
        HttpRequestException { StatusCode: HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout } => "translation-timeout",
        HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "translation-rate-limited",
        HttpRequestException { StatusCode: not null } => "translation-service-unavailable",
        HttpRequestException => "translation-network-unavailable",
        _ => "translation-unknown"
    };

    public static string Message(string code, bool vi) => code switch
    {
        "translation-network-unavailable" => vi ? "Không kết nối được máy chủ dịch. Kiểm tra mạng rồi thử lại." : "Cannot connect to the translation server. Check your connection and try again.",
        "translation-timeout" => vi ? "Dịch quá thời gian chờ. Vui lòng thử lại sau." : "Translation timed out. Please try again later.",
        "translation-quota-exceeded" => vi ? "Đã hết quota dịch. Kiểm tra gói hoặc chờ kỳ tiếp theo." : "Your translation quota is exhausted. Check your plan or wait for the next period.",
        "translation-rate-limited" => vi ? "Yêu cầu dịch quá nhanh. Vui lòng chờ rồi thử lại." : "Too many translation requests. Wait and try again.",
        "translation-access-denied" => vi ? "Bạn chưa có quyền dùng tính năng dịch này. Kiểm tra gói và đồng bộ quyền." : "You do not have access to this translation feature. Check your plan and refresh access.",
        "translation-sign-in-required" => vi ? "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại." : "Your session is invalid. Please sign in again.",
        "translation-consent-required" => vi ? "Xử lý văn bản bên ngoài đang tắt. Bật trong Cài đặt để dịch." : "External text processing is off. Enable it in Settings to translate.",
        "translation-service-unavailable" => vi ? "Dịch vụ dịch đang gặp sự cố. Vui lòng thử lại sau." : "The translation service is unavailable. Please try again later.",
        "capture-failed" => vi ? "Không chụp được màn hình. Kiểm tra quyền chụp và chọn lại vùng dịch." : "Screen capture failed. Check capture permissions and select the region again.",
        "ocr-failed" => vi ? "Không nhận dạng được chữ. Kiểm tra ngôn ngữ OCR đã cài rồi thử lại." : "Text recognition failed. Check the installed OCR language and try again.",
        "selection-failed" => vi ? "Không đọc được văn bản đã chọn. Chọn lại văn bản trong ứng dụng có cùng quyền với LexVerse." : "Cannot read selected text. Select text in an app with the same permissions as LexVerse.",
        _ => vi ? "Chưa xác định được nguyên nhân lỗi dịch. Vui lòng thử lại; liên hệ hỗ trợ nếu lỗi tiếp diễn." : "The cause of the translation failure is unknown. Try again; contact support if it continues."
    };
}
