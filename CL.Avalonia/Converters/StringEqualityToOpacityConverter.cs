using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace CL_CLegendary_Launcher_.Avalonia.Converters;

public class StringEqualityToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? current = value?.ToString();
        string? target = parameter?.ToString();
        return current == target ? 1.0 : 0.5;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
