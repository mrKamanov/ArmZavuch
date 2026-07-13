using System.Text.RegularExpressions;

namespace ArmZavuch.Services.Schedule;

/// <summary>
/// Предлагает названия недельных шаблонов: «Неделя А», «Неделя Б» … по русскому алфавиту.
/// </summary>
public static partial class WeekTemplateNameSuggestions
{
    public const string WeekNamePrefix = "Неделя ";

    public static readonly IReadOnlyList<char> WeekLetters =
        "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ".ToCharArray();

    public static string SuggestNext(IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var letter in WeekLetters)
        {
            var name = FormatWeekName(letter);
            if (!existing.Contains(name))
                return name;
        }

        return SuggestNumberedFallback(existing);
    }

    public static string SuggestCopyName(string sourceName, IEnumerable<string> existingNames)
    {
        var existing = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (TryParseWeekLetter(sourceName, out var letter)
            && TryFindLetterIndex(letter, out var index))
        {
            for (var i = index + 1; i < WeekLetters.Count; i++)
            {
                var name = FormatWeekName(WeekLetters[i]);
                if (!existing.Contains(name))
                    return name;
            }
        }

        return SuggestNext(existingNames);
    }

    public static bool TryParseWeekLetter(string name, out char letter)
    {
        letter = default;
        var match = WeekNameRegex().Match(name.Trim());
        if (!match.Success)
            return false;

        letter = char.ToUpperInvariant(match.Groups["letter"].Value[0]);
        return true;
    }

    public static string FormatWeekName(char letter) =>
        WeekNamePrefix + char.ToUpperInvariant(letter);

    private static bool TryFindLetterIndex(char letter, out int index)
    {
        var upper = char.ToUpperInvariant(letter);
        for (var i = 0; i < WeekLetters.Count; i++)
        {
            if (WeekLetters[i] == upper)
            {
                index = i;
                return true;
            }
        }

        index = -1;
        return false;
    }

    private static string SuggestNumberedFallback(HashSet<string> existing)
    {
        if (!existing.Contains("Основная"))
            return "Основная";

        for (var i = 1; i < 100; i++)
        {
            var name = $"Шаблон {i}";
            if (!existing.Contains(name))
                return name;
        }

        return $"Шаблон {DateTime.Now:HHmmss}";
    }

    [GeneratedRegex(@"^неделя\s+(?<letter>\S)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WeekNameRegex();
}
