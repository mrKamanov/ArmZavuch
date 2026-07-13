namespace ArmZavuch.Services.Text;

/// <summary>Подсказки по написанию: оформление заголовка, префикс к названию из словаря, опечатки.</summary>
public sealed class TextSuggestionService
{
    public string? FindClosestMatch(string input, IEnumerable<string> dictionary) =>
        FindClosest(input, dictionary);

    public TextSuggestion? SuggestTitle(string input, IEnumerable<string> dictionary)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
            return null;

        var formatted = ProperNameFormatter.FormatTitle(trimmed);
        if (!trimmed.Equals(formatted, StringComparison.Ordinal))
            return new TextSuggestion(trimmed, formatted, 1.0, SuggestionKind.Capitalization);

        var match = FindClosest(trimmed, dictionary);
        if (match is not null && !trimmed.Equals(match, StringComparison.OrdinalIgnoreCase))
            return new TextSuggestion(trimmed, match, 0.85, SuggestionKind.Dictionary);

        return null;
    }

    public TextSuggestion? SuggestPersonName(string input, IEnumerable<string> existingNames)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
            return null;

        var formatted = ProperNameFormatter.FormatPersonName(trimmed);
        if (!trimmed.Equals(formatted, StringComparison.Ordinal))
            return new TextSuggestion(trimmed, formatted, 1.0, SuggestionKind.Capitalization);

        var match = FindClosest(trimmed, existingNames);
        if (match is not null && !trimmed.Equals(match, StringComparison.OrdinalIgnoreCase))
            return new TextSuggestion(trimmed, match, 0.8, SuggestionKind.Dictionary);

        return null;
    }

    private static string? FindClosest(string input, IEnumerable<string> dictionary)
    {
        var exact = dictionary.FirstOrDefault(d =>
            !string.IsNullOrEmpty(d) && d.Equals(input, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        if (input.Length >= 3)
        {
            var prefix = dictionary
                .Where(d => !string.IsNullOrEmpty(d)
                            && d.Length > input.Length
                            && d.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Length)
                .FirstOrDefault();
            if (prefix is not null)
                return prefix;
        }

        var lower = input.ToLowerInvariant();
        var maxDist = Math.Max(2, input.Length / 4);
        string? best = null;
        var bestDist = int.MaxValue;
        foreach (var candidate in dictionary)
        {
            if (string.IsNullOrEmpty(candidate))
                continue;
            if (Math.Abs(candidate.Length - input.Length) > maxDist + 2)
                continue;

            var dist = Levenshtein(lower, candidate.ToLowerInvariant());
            if (dist < bestDist && dist <= maxDist)
            {
                bestDist = dist;
                best = candidate;
            }
        }

        return best;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[b.Length];
    }
}

public sealed class TextSuggestion
{
    public string Original { get; }
    public string Suggested { get; }
    public double Confidence { get; }
    public SuggestionKind Kind { get; }

    public TextSuggestion(string original, string suggested, double confidence, SuggestionKind kind)
    {
        Original = original;
        Suggested = suggested;
        Confidence = confidence;
        Kind = kind;
    }

    public string Hint => Kind == SuggestionKind.Capitalization
        ? $"Оформить: «{Suggested}»?"
        : $"Возможно: «{Suggested}»?";
}

public enum SuggestionKind
{
    Capitalization,
    Dictionary
}
