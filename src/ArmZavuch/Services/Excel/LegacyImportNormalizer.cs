using System.Globalization;
using System.Text.RegularExpressions;
using ArmZavuch.Services.Text;

namespace ArmZavuch.Services.Excel;

/// <summary>Нормализация «грязных» данных из legacy-расписания.</summary>
public static class LegacyImportNormalizer
{
    public const string DefaultBuilding = "Основное здание";

    public readonly record struct LegacySubjectParse(string Name, IReadOnlyList<string> Rooms, bool HasSubgroupInName);

    public readonly record struct LegacyRoomColumnParse(IReadOnlyList<string> Teachers, IReadOnlyList<string> Rooms);

    private static readonly Regex GluedTeacherRegex = new(
        @"^(?<surname>[А-ЯЁ][а-яё]{2,})(?<initials>[А-ЯЁ]{1,3})$",
        RegexOptions.CultureInvariant);

    private static readonly Regex BuildingShiftSuffixRegex = new(
        @"\s+(?:\(?\s*)?(?:2|вторая|ii|ii\s*я)\s*[-–]?\s*(?:я\s+)?смена\s*\)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ShiftTeacherPrefixRegex = new(
        @"^(?:вторая\s*)?(?:2\s*)?смена",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex RoomSplitRegex = new(
        @"\s*(?:/|,|\||\s+и\s+|(?<=\d)и(?=\d))\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SubjectRoomRegex = new(
        @"\(\s*каб\.?\s*(?<num1>\d+)\s*\)|(?:\bкаб(?:инет)?\.?\s*№?\s*(?<num2>\d+)\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GluedGroupSuffixRegex = new(
        @"(?<=[а-яё])группа\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> SubjectAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["дкп"] = "ДКП (духовное краеведение Подмосковья)",
        ["трудтехнология"] = "Труд/технология",
        ["труд/технология"] = "Труд/технология",
        ["иностранныйязык"] = "Иностранный язык"
    };

    public static string StripPunctuation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var chars = value.Where(c => !char.IsPunctuation(c) || c == '-' || c == '/').ToArray();
        return Regex.Replace(new string(chars), @"\s+", " ").Trim();
    }

    public static string NormalizeBuilding(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultBuilding;

        var text = StripPunctuation(raw);
        text = StripShiftTeacherPrefix(text);
        text = BuildingShiftSuffixRegex.Replace(text, "").Trim();
        if (text.Length == 0 || LooksLikeOnlyTeachers(text))
            return DefaultBuilding;

        return ProperNameFormatter.FormatBuildingOrAddress(text);
    }

    public static string GetBuildingMergeKey(string normalizedBuilding)
    {
        var core = normalizedBuilding.Split(',')[0].Trim();
        return core.ToLowerInvariant();
    }

    public static bool PreferBuildingName(string candidate, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
            return true;

        var candidateHasShift = candidate.Contains("смен", StringComparison.OrdinalIgnoreCase);
        var currentHasShift = current.Contains("смен", StringComparison.OrdinalIgnoreCase);
        if (currentHasShift && !candidateHasShift)
            return true;
        if (candidateHasShift && !currentHasShift)
            return false;

        return candidate.Length < current.Length;
    }

    public static LegacySubjectParse ParseSubjectCell(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new("", [], false);

        var hasSlash = raw.Contains('/');
        var text = StripPunctuation(raw);
        var rooms = ExtractSubjectRooms(ref text);
        text = StripSubjectNoise(text);
        text = CollapseSpaces(text);

        if (hasSlash)
        {
            var aliasApplied = TryApplySubjectAlias(text.Split('/')[0], out var aliasFull);
            var parts = text.Split('/');
            if (aliasApplied)
                parts[0] = aliasFull.Contains('/') ? aliasFull.Split('/')[0] : aliasFull;

            var formatted = string.Join("/", parts.Select(p => ProperNameFormatter.FormatTitle(p.Trim())));
            if (aliasApplied && aliasFull.Contains('/'))
                formatted = aliasFull;
            else if (aliasApplied)
                formatted = aliasFull;

            return new(formatted, rooms, true);
        }

        if (TryApplySubjectAlias(text, out var alias))
            text = alias;

        return new(FormatSubjectName(text), rooms, false);
    }

    public static LegacyRoomColumnParse ParseRoomColumn(string? raw)
    {
        raw = StripPunctuation(raw);
        if (string.IsNullOrWhiteSpace(raw))
            return new([], []);

        if (IsSportRoom(raw))
            return new([], ["с/з"]);

        if (ShouldParseAsTeachers(raw))
            return new(ParseTeacherTokens(raw), []);

        return new([], ParseRoomNumbers(raw));
    }

    public static IReadOnlyList<string> ParseRoomNumbers(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        raw = StripPunctuation(raw);
        if (IsSportRoom(raw))
            return ["с/з"];

        if (Regex.IsMatch(raw, @"^\d+$") && raw.Length >= 4 && raw.Length % 2 == 0)
        {
            return Enumerable.Range(0, raw.Length / 2)
                .Select(i => NormalizeRoomToken(raw.Substring(i * 2, 2)))
                .Where(p => p.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var parts = RoomSplitRegex.Split(raw)
            .Select(NormalizeRoomToken)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return parts;
    }

    public static IReadOnlyList<string> ParseTeacherTokens(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        raw = StripShiftTeacherPrefix(StripPunctuation(raw));
        var tokens = new List<string>();

        foreach (var part in raw.Split('/'))
        {
            var piece = part.Trim();
            if (piece.Length == 0 || IsNoiseTeacherToken(piece))
                continue;

            if (piece.Contains(' '))
            {
                foreach (var segment in piece.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    tokens.AddRange(SplitConcatenatedTeachers(segment));
            }
            else
            {
                tokens.AddRange(SplitConcatenatedTeachers(piece));
            }
        }

        return tokens;
    }

    public static IReadOnlyList<string> SplitConcatenatedTeachers(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0)
            return [];

        var dp = TrySplitTeachersDp(raw, 0);
        if (dp is { Count: > 0 })
            return dp.Select(FormatTeacherDisplay).ToList();

        if (GluedTeacherRegex.IsMatch(raw))
            return [FormatTeacherDisplay(raw)];

        return [FormatTeacherDisplay(raw)];
    }

    public static string FormatTeacherDisplay(string raw)
    {
        raw = StripPunctuation(raw);
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var surname = ProperNameFormatter.FormatPersonName(parts[0]);
            var initials = string.Concat(parts.Skip(1)).ToUpperInvariant();
            return initials.Length > 0 ? $"{surname} {initials}" : surname;
        }

        var glued = GluedTeacherRegex.Match(raw);
        if (glued.Success)
        {
            var surname = ProperNameFormatter.FormatPersonName(glued.Groups["surname"].Value);
            var initials = glued.Groups["initials"].Value.ToUpperInvariant();
            return $"{surname} {initials}";
        }

        return ProperNameFormatter.FormatPersonName(raw);
    }

    public static string GetTeacherMergeKey(string raw)
    {
        var display = FormatTeacherDisplay(raw);
        if (string.IsNullOrWhiteSpace(display))
            return "";

        return display.Split(' ')[0].ToLowerInvariant();
    }

    public static bool PreferTeacherName(string candidate, string current)
    {
        if (string.IsNullOrWhiteSpace(current))
            return true;

        var candidateParts = candidate.Split(' ');
        var currentParts = current.Split(' ');
        if (candidateParts.Length > currentParts.Length)
            return true;
        if (candidateParts.Length < currentParts.Length)
            return false;

        if (candidateParts.Length >= 2 && currentParts.Length >= 2)
        {
            var candidateInitials = candidateParts[^1];
            var currentInitials = currentParts[^1];
            return candidateInitials.Length > currentInitials.Length;
        }

        return candidate.Length > current.Length;
    }

    public static bool LooksLikeTeacher(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        value = StripPunctuation(value);
        if (value.Contains('/'))
            return true;

        if (GluedTeacherRegex.IsMatch(value))
            return true;

        if (Regex.IsMatch(value, @"^[А-ЯЁ][а-яё]+\s+[А-ЯЁ]{1,3}$", RegexOptions.CultureInvariant))
            return true;

        return TrySplitTeachersDp(value, 0) is { Count: >= 2 };
    }

    public static bool DetectSubgroups(string? subjectRaw, IReadOnlyList<string> rooms, IReadOnlyList<string> teachers) =>
        (subjectRaw?.Contains('/') ?? false)
        || rooms.Count > 1
        || teachers.Count > 1;

    private static List<string>? TrySplitTeachersDp(string s, int pos)
    {
        if (pos == s.Length)
            return [];

        if (!TrySurnameEnd(s, pos, out var surEnd))
            return null;

        List<string>? best = null;
        var maxInitials = Math.Min(3, s.Length - surEnd);
        for (var iniLen = maxInitials; iniLen >= 0; iniLen--)
        {
            if (iniLen > 0 && !IsLetterRun(s, surEnd, iniLen))
                continue;

            var next = surEnd + iniLen;
            var rest = TrySplitTeachersDp(s, next);
            if (rest is null)
                continue;

            var chunk = s[pos..next];
            best = [chunk, ..rest];
            break;
        }

        return best;
    }

    private static bool TrySurnameEnd(string s, int pos, out int end)
    {
        end = pos;
        if (pos >= s.Length || !IsNameLetter(s[pos]))
            return false;

        end = pos + 1;
        while (end < s.Length && IsNameLetter(s[end]) && char.IsLower(s[end]))
            end++;

        return end - pos >= 4;
    }

    private static bool IsNameLetter(char c) =>
        (c >= 'A' && c <= 'Z')
        || (c >= 'a' && c <= 'z')
        || (c >= 'А' && c <= 'я')
        || c is 'Ё' or 'ё';

    private static bool IsLetterRun(string s, int pos, int len)
    {
        for (var i = 0; i < len; i++)
        {
            if (!IsNameLetter(s[pos + i]))
                return false;
        }

        return true;
    }

    private static string StripShiftTeacherPrefix(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var text = raw.Trim();
        text = ShiftTeacherPrefixRegex.Replace(text, "");
        return text.Trim();
    }

    private static bool LooksLikeOnlyTeachers(string text)
    {
        var split = TrySplitTeachersDp(text, 0);
        if (split is not { Count: > 0 })
            return false;

        return split.All(t => FormatTeacherDisplay(t).Contains(' ', StringComparison.Ordinal));
    }

    private static bool ShouldParseAsTeachers(string raw)
    {
        if (Regex.IsMatch(raw, @"^\d+$"))
            return false;

        var roomParts = RoomSplitRegex.Split(raw)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (roomParts.Count > 0 && roomParts.All(IsSimpleRoomToken))
            return false;

        if (!raw.Any(IsNameLetter))
            return false;

        var split = TrySplitTeachersDp(raw, 0);
        if (split is { Count: >= 2 })
            return true;

        if (GluedTeacherRegex.IsMatch(raw))
            return true;

        return split is { Count: 1 }
               && FormatTeacherDisplay(split[0]).Contains(' ', StringComparison.Ordinal)
               && !IsSimpleRoomToken(raw);
    }

    private static bool IsSimpleRoomToken(string token)
    {
        token = token.Trim();
        if (token.Length == 0)
            return true;

        if (IsSportRoom(token))
            return true;

        return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsSportRoom(string raw) =>
        raw.Equals("сз", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("с/з", StringComparison.OrdinalIgnoreCase)
        || raw.Contains("спорт", StringComparison.OrdinalIgnoreCase);

    private static List<string> ExtractSubjectRooms(ref string text)
    {
        var rooms = new List<string>();
        foreach (Match match in SubjectRoomRegex.Matches(text))
        {
            var num = match.Groups["num1"].Success
                ? match.Groups["num1"].Value
                : match.Groups["num2"].Value;
            if (string.IsNullOrWhiteSpace(num))
                continue;

            rooms.Add(NormalizeRoomToken(num));
        }

        text = SubjectRoomRegex.Replace(text, " ");
        return rooms;
    }

    private static string StripSubjectNoise(string text)
    {
        text = GluedGroupSuffixRegex.Replace(text, "");
        text = Regex.Replace(text, @"\bгруппа\s*$", "", RegexOptions.IgnoreCase);
        return text.Trim();
    }

    private static string CollapseSpaces(string text) =>
        Regex.Replace(text, @"\s+", " ").Trim();

    private static bool TryApplySubjectAlias(string text, out string alias)
    {
        alias = "";
        var key = AliasKey(text);
        if (SubjectAliases.TryGetValue(key, out alias!))
            return true;

        alias = "";
        return false;
    }

    private static string AliasKey(string text) =>
        Regex.Replace(text, @"[\s/]+", "").ToLowerInvariant();

    private static string FormatSubjectName(string text)
    {
        if (text.Contains('/'))
        {
            return string.Join("/",
                text.Split('/').Select(p => ProperNameFormatter.FormatTitle(p.Trim())));
        }

        return ProperNameFormatter.FormatTitle(text);
    }

    private static string NormalizeRoomToken(string token)
    {
        token = token.Trim();
        if (token.Length == 0)
            return "";

        if (token.Equals("сз", StringComparison.OrdinalIgnoreCase))
            return "с/з";

        if (double.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
            return ((int)n).ToString(CultureInfo.InvariantCulture);

        return token;
    }

    private static bool IsNoiseTeacherToken(string token) =>
        token.Equals("с/з", StringComparison.OrdinalIgnoreCase)
        || token.Equals("сз", StringComparison.OrdinalIgnoreCase);
}
