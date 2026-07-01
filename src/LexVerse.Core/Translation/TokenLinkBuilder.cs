using System.Text.RegularExpressions;
using LexVerse.Core.Models;

namespace LexVerse.Core.Translation;

public static partial class TokenLinkBuilder
{
    public static IReadOnlyList<TokenLink> Build(string sourceText, string translatedText)
    {
        var sourceTokens = WordRegex().Matches(sourceText).Select(match => match.Value).ToArray();
        var targetTokens = WordRegex().Matches(translatedText).Select(match => match.Value).ToArray();
        var count = Math.Max(sourceTokens.Length, targetTokens.Length);
        var links = new List<TokenLink>(count);

        for (var index = 0; index < count; index++)
        {
            links.Add(new TokenLink(
                index,
                index < sourceTokens.Length ? sourceTokens[index] : string.Empty,
                index < targetTokens.Length ? targetTokens[index] : string.Empty));
        }

        return links;
    }

    [GeneratedRegex(@"[\p{L}\p{N}']+")]
    private static partial Regex WordRegex();
}
