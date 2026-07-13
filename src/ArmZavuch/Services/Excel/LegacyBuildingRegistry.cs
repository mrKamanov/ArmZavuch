namespace ArmZavuch.Services.Excel;

/// <summary>Сводит «Савельича» и «Савельича вторая смена» к одному зданию.</summary>
public sealed class LegacyBuildingRegistry
{
    private readonly Dictionary<string, string> _canonicalByKey = new(StringComparer.OrdinalIgnoreCase);

    public string Register(string raw)
    {
        var normalized = LegacyImportNormalizer.NormalizeBuilding(raw);
        var key = LegacyImportNormalizer.GetBuildingMergeKey(normalized);
        if (_canonicalByKey.TryGetValue(key, out var existing))
        {
            if (LegacyImportNormalizer.PreferBuildingName(normalized, existing))
                _canonicalByKey[key] = normalized;
            return _canonicalByKey[key];
        }

        _canonicalByKey[key] = normalized;
        return normalized;
    }

    public IEnumerable<string> AllCanonical => _canonicalByKey.Values.Distinct(StringComparer.OrdinalIgnoreCase);
}
