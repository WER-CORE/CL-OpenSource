using CL_CLegendary_Launcher_.Class;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CL_CLegendary_Launcher_.Class
{
    public static class WebHelper
    {
        private static readonly List<BrowserInfo> _supportedBrowsers = new List<BrowserInfo>
        {
            new BrowserInfo("chrome.exe", @"Google\Chrome\Application\chrome.exe"),
            new BrowserInfo("firefox.exe", @"Mozilla Firefox\firefox.exe"),
            new BrowserInfo("brave.exe", @"BraveSoftware\Brave-Browser\Application\brave.exe"),
            new BrowserInfo("vivaldi.exe", @"Vivaldi\Application\vivaldi.exe"),
            new BrowserInfo("launcher.exe", @"Opera GX\launcher.exe"),
            new BrowserInfo("launcher.exe", @"Opera\launcher.exe"),
            new BrowserInfo("waterfox.exe", @"Waterfox\waterfox.exe"),
            new BrowserInfo("librewolf.exe", @"LibreWolf\librewolf.exe"),
            new BrowserInfo("palemoon.exe", @"Pale Moon\palemoon.exe"),
            new BrowserInfo("thorium.exe", @"Thorium\Application\thorium.exe"),
            new BrowserInfo("iron.exe", @"SRWare Iron\iron.exe"),
            new BrowserInfo("epic.exe", @"Epic Privacy Browser\Application\epic.exe"),
            new BrowserInfo("AvastBrowser.exe", @"AVAST Software\Browser\Application\AvastBrowser.exe"),
            new BrowserInfo("CCleanerBrowser.exe", @"CCleaner Browser\Application\CCleanerBrowser.exe"),
            new BrowserInfo("chromium.exe", @"Chromium\Application\chromium.exe"),
            new BrowserInfo("msedge.exe", @"Microsoft\Edge\Application\msedge.exe")
        };

        public static void OpenUrl(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url) || url == "-")
                    return;

                bool launched = false;

                foreach (var browser in _supportedBrowsers)
                {
                    string path = GetBrowserPath(browser);
                    if (!string.IsNullOrEmpty(path))
                    {
                        try
                        {
                            Process.Start(path, url);
                            launched = true;
                            break;
                        }
                        catch { }
                    }
                }

                if (!launched)
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    launched = true;
                }
            }
            catch (Exception ex)
            {
                MascotMessageBox.Show(
                    string.Format(LocalizationManager.GetString("Dialogs.UrlOpenErrorDesc", "Не вдалося відкрити посилання.\nДеталі: {0}"), ex.Message),
                    LocalizationManager.GetString("Dialogs.UrlOpenErrorTitle", "Помилка браузера"),
                    Windows.MascotEmotion.Sad);
            }
        }

        private static string GetBrowserPath(BrowserInfo browser)
        {
            var searchFolders = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            foreach (var folder in searchFolders)
            {
                if (string.IsNullOrEmpty(folder)) continue;

                string fullPath = Path.Combine(folder, browser.RelativePath);

                if (File.Exists(fullPath))
                    return fullPath;

                string simplePath = Path.Combine(folder, browser.ExeName);
                if (File.Exists(simplePath))
                    return simplePath;
            }

            return null;
        }
        private class BrowserInfo
        {
            public string ExeName { get; }
            public string RelativePath { get; }

            public BrowserInfo(string exeName, string relativePath)
            {
                ExeName = exeName;
                RelativePath = relativePath;
            }
        }
    }
}