using System.Net;
using System.Text.Json;

namespace LexVerse.Infrastructure.Bedrock;

public static class BedrockErrorFormatter
{
    public static string Format(HttpStatusCode statusCode, string responseBody, BedrockSettings settings)
    {
        var message = TryReadMessage(responseBody) ?? responseBody;
        var lines = new List<string>
        {
            $"Amazon Bedrock popup translation failed with {(int)statusCode} {statusCode}.",
            message
        };

        if (message.Contains("on-demand throughput isn't supported", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"Current model/profile: {settings.ModelId}");
            lines.Add("Action: Use an inference profile ID or ARN in LLM_MODEL. For us-* and eu-* regions, Lexverse can auto-convert base Anthropic IDs to us.<model-id> or eu.<model-id>.");
        }

        if (message.Contains("model identifier is invalid", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"Current model/profile: {settings.ModelId}");
            lines.Add($"Current region: {settings.Region}");
            lines.Add("Action: Set LLM_MODEL to a valid Bedrock inference profile ID/ARN for this account and region, or switch AWS_BEDROCK_REGION to a region where that inference profile exists.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string? TryReadMessage(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
