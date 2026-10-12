using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LexVerse.Application.Desktop;

public static class DesktopBridgeProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumMessageBytes = 256 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static bool TryParseCommand(
        string json,
        out DesktopBridgeCommand? command,
        out string errorCode)
    {
        command = null;
        errorCode = string.Empty;
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > MaximumMessageBytes)
        {
            errorCode = "invalid-message-size";
            return false;
        }

        try
        {
            command = JsonSerializer.Deserialize<DesktopBridgeCommand>(json, JsonOptions);
            if (command is null || command.Version != CurrentVersion)
            {
                errorCode = "unsupported-version";
                command = null;
                return false;
            }

            if (string.IsNullOrWhiteSpace(command.Id) || command.Id.Length > 128 ||
                string.IsNullOrWhiteSpace(command.Type) || command.Type.Length > 96)
            {
                errorCode = "invalid-envelope";
                command = null;
                return false;
            }

            command = command with
            {
                Id = command.Id.Trim(),
                Type = command.Type.Trim(),
                Payload = command.Payload.Clone()
            };
            return true;
        }
        catch (JsonException)
        {
            errorCode = "invalid-json";
            return false;
        }
    }

    public static T DeserializePayload<T>(JsonElement payload) where T : class =>
        payload.Deserialize<T>(JsonOptions)
        ?? throw new DesktopBridgePayloadException("invalid-payload");

    public static string SerializeResult(
        string id,
        bool ok,
        object? payload = null,
        string? errorCode = null) =>
        JsonSerializer.Serialize(new DesktopBridgeResult(
            CurrentVersion,
            id,
            ok,
            payload,
            ok ? null : errorCode ?? "operation-failed"), JsonOptions);

    public static string SerializeEvent(string type, object payload) =>
        JsonSerializer.Serialize(
            new DesktopBridgeEvent(CurrentVersion, type, payload),
            JsonOptions);
}

public sealed record DesktopBridgeCommand(
    int Version,
    string Id,
    string Type,
    JsonElement Payload);

public sealed record DesktopBridgeResult(
    int Version,
    string Id,
    bool Ok,
    object? Payload,
    string? ErrorCode);

public sealed record DesktopBridgeEvent(
    int Version,
    string Type,
    object Payload);

public sealed class DesktopBridgePayloadException(string errorCode) : Exception(errorCode)
{
    public string ErrorCode { get; } = errorCode;
}
