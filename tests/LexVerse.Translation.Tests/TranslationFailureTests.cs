using System.Net;
using LexVerse.Application.Diagnostics;
using LexVerse.Application.Privacy;
using LexVerse.Core.Pipeline;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class TranslationFailureTests
{
    [Fact]
    public void ClassifiesKnownFailuresWithoutExposingExceptionText()
    {
        (Exception Error, string Code)[] cases = [
            (new HttpRequestException("private host"), "translation-network-unavailable"),
            (new TaskCanceledException(), "translation-timeout"),
            (new TimeoutException(), "translation-timeout"),
            (new RemoteProcessingConsentRequiredException(), "translation-consent-required"),
            (new HttpRequestException(null, null, HttpStatusCode.Unauthorized), "translation-sign-in-required"),
            (new HttpRequestException(null, null, HttpStatusCode.Forbidden), "translation-access-denied"),
            (new HttpRequestException(null, null, HttpStatusCode.TooManyRequests), "translation-rate-limited"),
            (new HttpRequestException(null, null, HttpStatusCode.BadGateway), "translation-service-unavailable"),
            (new HttpRequestException(null, null, HttpStatusCode.GatewayTimeout), "translation-timeout"),
            (new TranslationFailureException("translation-quota-exceeded"), "translation-quota-exceeded"),
            (new PipelineStageException("capture", new Exception("private")), "capture-failed"),
            (new PipelineStageException("ocr", new Exception("private")), "ocr-failed"),
            (new Exception("private"), "translation-unknown")
        ];
        foreach (var (error, code) in cases)
        {
            Assert.Equal(code, TranslationFailure.Code(error));
            Assert.DoesNotContain("private", TranslationFailure.Message(code, false));
            Assert.NotEqual(TranslationFailure.Message(code, false), TranslationFailure.Message(code, true));
        }
    }
}
