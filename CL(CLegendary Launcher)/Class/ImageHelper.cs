using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_.Class
{
    public static class ImageHelper
    {
        public static BitmapImage LoadOptimizedImage(string path, int decodeWidth = 0)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                var uri = new Uri(path, UriKind.RelativeOrAbsolute);
                return LoadFromUri(uri, decodeWidth);
            }
            catch
            {
                return null;
            }
        }
        public static BitmapImage LoadFromBytes(byte[] imageData, int decodeWidth = 0)
        {
            if (imageData == null || imageData.Length == 0) return null;

            try
            {
                using (var stream = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;

                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile; 

                    if (decodeWidth > 0)
                    {
                        bitmap.DecodePixelWidth = decodeWidth;
                    }

                    bitmap.EndInit();

                    if (bitmap.CanFreeze) bitmap.Freeze();

                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private static BitmapImage LoadFromUri(Uri uri, int decodeWidth)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;

            bitmap.CacheOption = BitmapCacheOption.OnLoad;

            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            if (decodeWidth > 0)
            {
                bitmap.DecodePixelWidth = decodeWidth;
            }

            bitmap.EndInit();

            if (bitmap.CanFreeze) bitmap.Freeze();

            return bitmap;
        }
    }
}