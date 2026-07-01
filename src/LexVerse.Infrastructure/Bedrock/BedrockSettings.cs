namespace LexVerse.Infrastructure.Bedrock;

public sealed record BedrockSettings(
    string ApiKey,
    string Region,
    string ModelId)
{
    public static BedrockSettings FromEnv(IReadOnlyDictionary<string, string> env)
    {
        var apiKey = env.GetValueOrDefault("AWS_BEDROCK_API_KEY", string.Empty);
        var region = env.GetValueOrDefault("AWS_BEDROCK_REGION", "us-east-1");
        var modelId = env.GetValueOrDefault("LLM_MODEL", string.Empty);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AWS_BEDROCK_API_KEY is missing in .env.");
        }

        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new InvalidOperationException("LLM_MODEL is missing in .env.");
        }

        return new BedrockSettings(apiKey, region, NormalizeModelId(modelId, region));
    }

    public static string NormalizeModelId(string modelId, string region)
    {
        var trimmedModelId = modelId.Trim();
        if (trimmedModelId.StartsWith("arn:", StringComparison.OrdinalIgnoreCase)
            || trimmedModelId.StartsWith("us.", StringComparison.OrdinalIgnoreCase)
            || trimmedModelId.StartsWith("eu.", StringComparison.OrdinalIgnoreCase)
            || trimmedModelId.StartsWith("apac.", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedModelId;
        }

        if (!trimmedModelId.StartsWith("anthropic.", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedModelId;
        }

        var profilePrefix = region.Trim().ToLowerInvariant() switch
        {
            var value when value.StartsWith("us-") => "us",
            var value when value.StartsWith("eu-") => "eu",
            _ => string.Empty
        };

        return string.IsNullOrWhiteSpace(profilePrefix)
            ? trimmedModelId
            : $"{profilePrefix}.{trimmedModelId}";
    }
}
