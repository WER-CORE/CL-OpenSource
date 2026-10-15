using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CL.Core.Models;
using CL.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CL_CLegendary_Launcher_.Avalonia.Views.Controls
{
    // Голова гравця для акаунта: локальна іконка одразу, а для онлайн-акаунтів підвантажується з мережі.
    public class AccountAvatar : Image
    {
        public static readonly StyledProperty<ProfileItem?> ProfileProperty =
            AvaloniaProperty.Register<AccountAvatar, ProfileItem?>(nameof(Profile));

        private static readonly Dictionary<string, Task<Bitmap?>> Downloads = new Dictionary<string, Task<Bitmap?>>();

        public ProfileItem? Profile
        {
            get => GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Image);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ProfileProperty)
                _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            var profile = Profile;
            Source = profile == null ? null : LoadAsset(profile.TypeAccount);

            string? url = profile?.ImageUrl;
            if (url == null || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return;

            if (!Downloads.TryGetValue(url, out var download))
                Downloads[url] = download = DownloadAsync(url);

            var bitmap = await download;
            if (bitmap != null && Profile == profile)
                Source = bitmap;
        }

        private static Bitmap LoadAsset(AccountType type)
        {
            using var stream = AssetLoader.Open(new Uri($"avares://CL.Avalonia/Assets/Accounts/{type}.png"));
            return new Bitmap(stream);
        }

        private static async Task<Bitmap?> DownloadAsync(string url)
        {
            try
            {
                var bytes = await WebHelper.Client.GetByteArrayAsync(url);
                return new Bitmap(new MemoryStream(bytes));
            }
            catch
            {
                return null;
            }
        }
    }
}
