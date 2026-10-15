using Avalonia.Markup.Xaml;
using CL.Core.Services;
using System;

namespace CL_CLegendary_Launcher_.Avalonia.Localization
{
    // Ключі ті самі, що й у WPF-клієнті, тож обидва беруть переклад з одного файлу Locales/*.json.
    public class LocExtension : MarkupExtension
    {
        public LocExtension(string key, string fallback)
        {
            Key = key;
            Fallback = fallback;
        }

        public string Key { get; set; }
        public string Fallback { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
            => LocalizationManager.GetString(Key, Fallback);
    }
}
