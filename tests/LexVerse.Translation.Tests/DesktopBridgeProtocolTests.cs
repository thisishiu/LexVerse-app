using System.Text.Json;
using LexVerse.Application.Desktop;
using Xunit;

namespace LexVerse.Translation.Tests;

public sealed class DesktopBridgeProtocolTests
{
    [Fact]
    public void TryParseCommand_AcceptsVersionedAllowListEnvelopeShape()
    {
        const string json = """
            {"version":1,"id":"request-1","type":"app.getSnapshot","payload":{}}
            """;

        var parsed = DesktopBridgeProtocol.TryParseCommand(json, out var command, out var errorCode);

        Assert.True(parsed, errorCode);
        Assert.NotNull(command);
        Assert.Equal("request-1", command.Id);
        Assert.Equal("app.getSnapshot", command.Type);
    }

    [Fact]
    public void TryParseCommand_RejectsUnknownTopLevelProperties()
    {
        const string json = """
            {"version":1,"id":"request-1","type":"app.getSnapshot","payload":{},"secret":"no"}
            """;

        Assert.False(DesktopBridgeProtocol.TryParseCommand(json, out _, out var errorCode));
        Assert.Equal("invalid-json", errorCode);
    }

    [Fact]
    public void TryParseCommand_RejectsOversizedMessagesBeforeParsing()
    {
        var json = new string('x', DesktopBridgeProtocol.MaximumMessageBytes + 1);

        Assert.False(DesktopBridgeProtocol.TryParseCommand(json, out _, out var errorCode));
        Assert.Equal("invalid-message-size", errorCode);
    }

    [Fact]
    public void DeserializePayload_RejectsUnknownFields()
    {
        using var document = JsonDocument.Parse("""{"enabled":true,"unexpected":1}""");

        Assert.Throws<JsonException>(() =>
            DesktopBridgeProtocol.DeserializePayload<TestPayload>(document.RootElement));
    }

    [Fact]
    public void SerializedFailureDoesNotContainExceptionDetails()
    {
        var json = DesktopBridgeProtocol.SerializeResult("request-1", false, errorCode: "invalid-command");

        Assert.Contains("invalid-command", json, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record TestPayload(bool Enabled);
}
