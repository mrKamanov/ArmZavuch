using System.Globalization;
using System.Windows.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Views.Converters;

public sealed class HexMatchConverter : IMultiValueConverter
{
    public static HexMatchConverter Instance { get; } = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string left || values[1] is not string right)
            return false;

        return BuildingColors.Normalize(left)
            .Equals(BuildingColors.Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
