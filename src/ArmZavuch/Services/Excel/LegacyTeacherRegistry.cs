namespace ArmZavuch.Services.Excel;

/// <summary>Сводит варианты ФИО (Каманов / КамановСД / Каманов СД) к одному каноническому.</summary>
public sealed class LegacyTeacherRegistry
{
    private readonly Dictionary<string, string> _canonicalByKey = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var key = LegacyImportNormalizer.GetTeacherMergeKey(raw);
        if (key.Length == 0)
            return;

        var display = LegacyImportNormalizer.FormatTeacherDisplay(raw);
        if (_canonicalByKey.TryGetValue(key, out var existing))
        {
            if (LegacyImportNormalizer.PreferTeacherName(display, existing))
                _canonicalByKey[key] = display;
        }
        else
        {
            _canonicalByKey[key] = display;
        }
    }

    public string Resolve(string raw)
    {
        var key = LegacyImportNormalizer.GetTeacherMergeKey(raw);
        if (key.Length == 0)
            return LegacyImportNormalizer.FormatTeacherDisplay(raw);

        return _canonicalByKey.TryGetValue(key, out var canonical)
            ? canonical
            : LegacyImportNormalizer.FormatTeacherDisplay(raw);
    }

    public IEnumerable<string> AllCanonical => _canonicalByKey.Values.Distinct(StringComparer.OrdinalIgnoreCase);

    public bool ContainsKey(string raw) =>
        _canonicalByKey.ContainsKey(LegacyImportNormalizer.GetTeacherMergeKey(raw));
}
