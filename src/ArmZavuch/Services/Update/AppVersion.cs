using System.Reflection;

namespace ArmZavuch.Services.Update;

/// <summary>Текущая версия сборки для отображения и сравнения с релизами GitHub.</summary>
public static class AppVersion
{
    public static string Current =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?.Split('+')[0]
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static string Display => $"{AppBranding.ProductName} {Current}";

    public static bool IsRemoteNewer(string remoteVersion) =>
        Compare(Normalize(remoteVersion), Normalize(Current)) > 0;

    private static Version Normalize(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        var plus = text.IndexOf('+');
        if (plus >= 0)
            text = text[..plus];
        return Version.TryParse(text, out var version) ? version : new Version(0, 0);
    }

    private static int Compare(Version left, Version right)
    {
        if (left.Major != right.Major) return left.Major.CompareTo(right.Major);
        if (left.Minor != right.Minor) return left.Minor.CompareTo(right.Minor);
        if (left.Build != right.Build) return left.Build.CompareTo(right.Build);
        return left.Revision.CompareTo(right.Revision);
    }
}
