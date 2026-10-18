using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CL_CLegendary_Launcher_.Avalonia.Services;

namespace CL_CLegendary_Launcher_.Avalonia.Converters
{
    public class BitmapAssetValueConverter : IValueConverter
    {
        public static BitmapAssetValueConverter Instance = new BitmapAssetValueConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string rawUri && !string.IsNullOrWhiteSpace(rawUri))
            {
                return AvaloniaImageHelper.LoadOptimizedImage(rawUri);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
