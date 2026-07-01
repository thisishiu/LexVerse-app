using System.Net;
using System.Text.Json;

namespace LexVerse.Infrastructure.Google;

public static class GoogleApiErrorFormatter
{
    public static string Format(string providerName, HttpStatusCode statusCode, string responseBody)
    {
        var parsed = TryParse(responseBody);

        if (parsed is null)
        {
            return $"{providerName} failed with {(int)statusCode} {statusCode}. {responseBody}";
        }

        var action = parsed.Service switch
        {
            "vision.googleapis.com" => "Enable Cloud Vision API for this Google Cloud project, then wait a few minutes and retry.",
            "translate.googleapis.com" or "translation.googleapis.com" => "Enable Cloud Translation API for this Google Cloud project, then wait a few minutes and retry.",
            _ when parsed.Reason == "SERVICE_DISABLED" => "Enable the required Google Cloud API for this project, then wait a few minutes and retry.",
            _ => "Check the Google Cloud service account, API permissions, billing, and project configuration."
        };

        var lines = new List<string>
        {
            $"{providerName} is not ready.",
            $"Google returned {(int)statusCode} {statusCode}: {parsed.Message}",
            $"Action: {action}"
        };

        if (!string.IsNullOrWhiteSpace(parsed.Link))
        {
            lines.Add($"Link: {parsed.Link}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static ParsedGoogleError? TryParse(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return null;
            }

            var message = ReadString(error, "message") ?? "Unknown Google API error.";
            var reason = string.Empty;
            var service = string.Empty;
            var link = string.Empty;

            if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    if (detail.TryGetProperty("reason", out var reasonElement))
                    {
                        reason = reasonElement.GetString() ?? reason;
                    }

                    if (detail.TryGetProperty("metadata", out var metadata)
                        && metadata.TryGetProperty("service", out var serviceElement))
                    {
                        service = serviceElement.GetString() ?? service;
                    }

                    if (detail.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
                    {
                        link = links.EnumerateArray()
                            .Select(item => ReadString(item, "url"))
                            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                            ?? link;
                    }
                }
            }

            return new ParsedGoogleError(message, reason, service, link);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
    }

    private sealed record ParsedGoogleError(string Message, string Reason, string Service, string Link);
}
