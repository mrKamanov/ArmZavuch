using System.Text.RegularExpressions;
using ArmZavuch.Services.Text;

namespace ArmZavuch.Services.Catalog;

/// <summary>Очистка «грязных» названий предметов из импорта и ручного ввода.</summary>
public static partial class SubjectNameNormalizer
{
    [GeneratedRegex(@"\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex ParenthesesContent();

    [GeneratedRegex(@"\s+и\s+\d+/\d+\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TrailingRoomAnd();

    [GeneratedRegex(@"\s+\d+/\d+\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingRoom();

    public static IEnumerable<string> LookupVariants(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variant in BuildVariants(rawName.Trim()))
        {
            if (seen.Add(variant))
                yield return variant;
        }
    }

    private static IEnumerable<string> BuildVariants(string raw)
    {
        yield return raw;

        var titled = ProperNameFormatter.FormatTitle(raw);
        if (!titled.Equals(raw, StringComparison.Ordinal))
            yield return titled;

        var noParen = ParenthesesContent().Replace(raw, "").Trim();
        if (noParen.Length > 0)
        {
            yield return noParen;
            yield return ProperNameFormatter.FormatTitle(noParen);
        }

        var noRoom = TrailingRoomAnd().Replace(raw, "").Trim();
        noRoom = TrailingRoom().Replace(noRoom, "").Trim();
        if (noRoom.Length > 0)
        {
            yield return noRoom;
            yield return ProperNameFormatter.FormatTitle(noRoom);
        }

        foreach (var part in raw.Split('/', '\\', '|'))
        {
            var piece = part.Trim();
            if (piece.Length == 0)
                continue;
            yield return piece;
            yield return ProperNameFormatter.FormatTitle(piece);
        }

        if (raw.Contains(" и ", StringComparison.OrdinalIgnoreCase))
        {
            var head = raw.Split(" и ", 2, StringSplitOptions.None)[0].Trim();
            if (head.Length > 0)
            {
                yield return head;
                yield return ProperNameFormatter.FormatTitle(head);
            }
        }
    }
}
