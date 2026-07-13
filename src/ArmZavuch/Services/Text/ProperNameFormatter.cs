using System.Text;

namespace ArmZavuch.Services.Text;

/// <summary>Нормализация написания имён собственных и названий.</summary>
public static class ProperNameFormatter
{
    private static readonly HashSet<string> LowerParticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "и", "в", "во", "на", "по", "с", "к", "о", "об", "для", "от", "из", "у", "при", "без"
    };

    public static string FormatPersonName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(FormatHyphenatedPart));
    }

    public static string FormatTitle(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var words = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(words.Length);
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (i > 0 && LowerParticles.Contains(w))
                result.Add(w.ToLowerInvariant());
            else
                result.Add(FormatHyphenatedPart(w));
        }
        return string.Join(" ", result);
    }

    public static string FormatBuildingOrAddress(string input) => FormatTitle(input);

    private static string FormatHyphenatedPart(string part)
    {
        if (part.Contains('-'))
            return string.Join("-", part.Split('-').Select(CapitalizeToken));
        return CapitalizeToken(part);
    }

    private static string CapitalizeToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return token;

        var lower = token.ToLowerInvariant();
        if (lower.Length == 1)
            return lower.ToUpperInvariant();

        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }
}
