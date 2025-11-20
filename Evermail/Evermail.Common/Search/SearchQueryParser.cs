using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Evermail.Common.Search;

public static class SearchQueryParser
{
    private static readonly Regex TokenRegex = new(@"(?:""[^""]+"")|(?:\S+)", RegexOptions.Compiled);
    private static readonly HashSet<string> BooleanOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "AND",
        "OR",
        "NOT"
    };

    public static IReadOnlyList<string> ExtractTerms(string? query, IEnumerable<string>? stopWords = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<string>();
        }

        HashSet<string>? normalizedStopWords = null;
        if (stopWords is not null)
        {
            normalizedStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in stopWords)
            {
                if (!string.IsNullOrWhiteSpace(word))
                {
                    normalizedStopWords.Add(word.Trim('"'));
                }
            }
        }

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in TokenRegex.Matches(query))
        {
            var token = match.Value.Trim();
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var normalized = token.Trim('"');
            if (BooleanOperators.Contains(normalized))
            {
                continue;
            }

            if (normalizedStopWords is not null && normalizedStopWords.Contains(normalized))
            {
                continue;
            }

            if (seen.Add(normalized))
            {
                results.Add(normalized);
            }
        }

        return results;
    }
}

