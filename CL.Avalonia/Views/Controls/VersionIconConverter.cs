using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CL_CLegendary_Launcher_.Avalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Controls
{
    public class VersionIconConverter : IValueConverter
    {
        public static readonly VersionIconConverter Instance = new VersionIconConverter();

        private readonly Dictionary<VersionKind, Bitmap> _icons = new Dictionary<VersionKind, Bitmap>();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not VersionKind kind)
                return null;

            if (!_icons.TryGetValue(kind, out var icon))
            {
                using var stream = AssetLoader.Open(new Uri($"avares://CL.Avalonia/Assets/Versions/{kind}.png"));
                _icons[kind] = icon = new Bitmap(stream);
            }

            return icon;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
