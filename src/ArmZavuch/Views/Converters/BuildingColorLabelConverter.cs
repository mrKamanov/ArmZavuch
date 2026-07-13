using System.Globalization;
using System.Windows.Data;
using ArmZavuch.Models;

namespace ArmZavuch.Views.Converters;

public sealed class BuildingColorLabelConverter : IValueConverter
{
    public static BuildingColorLabelConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string hex)
            return "—";

        return BuildingColors.Palette
                   .FirstOrDefault(c => BuildingColors.Normalize(c.Hex)
                       .Equals(BuildingColors.Normalize(hex), StringComparison.OrdinalIgnoreCase))
                   ?.Label
               ?? "Свой цвет";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
