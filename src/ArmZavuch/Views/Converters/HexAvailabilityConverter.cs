using System.Globalization;
using System.Windows.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Views.Converters;

/// <summary>True, если цвет из палитры доступен (не занят другим зданием).</summary>
public sealed class HexAvailabilityConverter : IMultiValueConverter
{
    public static HexAvailabilityConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string hex)
            return true;

        var normalized = BuildingColors.Normalize(hex);
        if (values[1] is not System.Collections.IEnumerable blocked)
            return true;

        foreach (var item in blocked)
        {
            if (item is string blockedHex
                && BuildingColors.Normalize(blockedHex).Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
