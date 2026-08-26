using CmlLib.Core;
using CmlLib.Core.Installer.Forge;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.Installer.NeoForge.Installers;
using CmlLib.Core.Installer.NeoForge.Versions;
using CmlLib.Core.Version;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class NoAdForgeInstaller
    {
        private readonly MinecraftLauncher _launcher;
        private readonly CmlLib.Core.Installer.Forge.Versions.IForgeInstallerVersionMapper _installerMapper;
        private readonly ForgeVersionLoader _versionLoader;

        public NoAdForgeInstaller(MinecraftLauncher launcher) : this(launcher, WebHelper.Client)
        {
        }

        public NoAdForgeInstaller(MinecraftLauncher launcher, HttpClient httpClient)
        {
            _installerMapper = new ForgeInstallerVersionMapper();
            _versionLoader = new ForgeVersionLoader(httpClient);
            _launcher = launcher;
        }

        public Task<string> Install(string mcVersion) =>
            Install(mcVersion, new ForgeInstallOptions());

        public async Task<string> Install(string mcVersion, ForgeInstallOptions options)
        {
            var versions = await _versionLoader.GetForgeVersions(mcVersion);
            var bestVersion =
                versions.FirstOrDefault(v => v.IsRecommendedVersion) ??
                versions.FirstOrDefault(v => v.IsLatestVersion) ??
                versions.FirstOrDefault() ??
                throw new InvalidOperationException("Cannot find any version");

            return await Install(bestVersion, options);
        }

        public Task<IEnumerable<ForgeVersion>> GetForgeVersions(string mcVersion)
        {
            return _versionLoader.GetForgeVersions(mcVersion);
        }

        public Task<string> Install(string mcVersion, string forgeVersion) =>
            Install(mcVersion, forgeVersion, new ForgeInstallOptions());

        public async Task<string> Install(string mcVersion, string forgeVersion, ForgeInstallOptions options)
        {
            var versions = await _versionLoader.GetForgeVersions(mcVersion);

            var foundVersion = versions.FirstOrDefault(v => v.ForgeVersionName == forgeVersion) ??
                throw new InvalidOperationException("Cannot find version name " + forgeVersion);
            return await Install(foundVersion, options);
        }

        public async Task<string> Install(ForgeVersion forgeVersion, ForgeInstallOptions options)
        {
            var installer = _installerMapper.CreateInstaller(forgeVersion);
            if (options.SkipIfAlreadyInstalled && await checkVersionInstalled(installer.VersionName))
                return installer.VersionName;

            var version = await checkAndDownloadVanillaVersion(
                            forgeVersion.MinecraftVersionName,
                            options);

            if (string.IsNullOrEmpty(options.JavaPath))
                options.JavaPath = getJavaPath(version);

            await installer.Install(_launcher.MinecraftPath, _launcher.GameInstaller, options);

            await _launcher.GetAllVersionsAsync();
            return installer.VersionName;
        }
        private async Task<IVersion> checkAndDownloadVanillaVersion(
                    string mcVersion,
                    ForgeInstallOptions options)
        {
            var version = await _launcher.GetVersionAsync(mcVersion);
            await _launcher.InstallAsync(version, options.FileProgress, options.ByteProgress, options.CancellationToken);

            return version;
        }
        private async Task<bool> checkVersionInstalled(string versionName)
        {
            try
            {
                await _launcher.GetVersionAsync(versionName);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private string getJavaPath(IVersion version)
        {
            var javaPath = _launcher.GetJavaPath(version);
            if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
                javaPath = _launcher.GetDefaultJavaPath();
            if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath))
                throw new InvalidOperationException("Cannot find any java binary. Set java binary path");

            return javaPath;
        }
    }
    public class NoAdNeoForgeInstaller
    {
        private readonly MinecraftLauncher _launcher;
        private readonly CmlLib.Core.Installer.NeoForge.Versions.IForgeInstallerVersionMapper _installerMapper;
        private readonly NeoForgeVersionLoader _versionLoader;

        public NoAdNeoForgeInstaller(MinecraftLauncher launcher) : this(launcher, WebHelper.Client)
        {
        }

        public NoAdNeoForgeInstaller(MinecraftLauncher launcher, HttpClient httpClient)
        {
            _installerMapper = new NeoForgeInstallerVersionMapper();
            _versionLoader = new NeoForgeVersionLoader(httpClient);
            _launcher = launcher;
        }

        public Task<string> Install(string mcVersion)
        {
            return Install(mcVersion, new NeoForgeInstallOptions());
        }

        public async Task<string> Install(string mcVersion, NeoForgeInstallOptions options)
        {
            var versions = await _versionLoader.GetNeoForgeVersions(mcVersion);
            NeoForgeVersion neoForgeVersion = versions.FirstOrDefault() ?? throw new InvalidOperationException("Cannot find any version");
            return await Install(neoForgeVersion, options);
        }

        public Task<string> Install(string mcVersion, string neoForgeVersion)
        {
            return Install(mcVersion, neoForgeVersion, new NeoForgeInstallOptions());
        }

        public async Task<string> Install(string mcVersion, string neoForgeVersion, NeoForgeInstallOptions options)
        {
            var versions = await _versionLoader.GetNeoForgeVersions(mcVersion);
            NeoForgeVersion foundVersion = versions.LastOrDefault(v => v.VersionName == neoForgeVersion) ?? throw new InvalidOperationException("Cannot find version name " + neoForgeVersion);
            return await Install(foundVersion, options);
        }

        public async Task<string> Install(NeoForgeVersion neoForgeVersion, NeoForgeInstallOptions options)
        {
            CmlLib.Core.Installer.NeoForge.Installers.IForgeInstaller installer = _installerMapper.CreateInstaller(neoForgeVersion);

            if (options.SkipIfAlreadyInstalled && await checkVersionInstalled(installer.VersionName))
            {
                return installer.VersionName;
            }

            IVersion version = await checkAndDownloadVanillaVersion(neoForgeVersion.MinecraftVersion, options);

            if (string.IsNullOrEmpty(options.JavaPath))
            {
                options.JavaPath = getJavaPath(version);
            }

            await installer.Install(_launcher.MinecraftPath, _launcher.GameInstaller, options);
            await _launcher.GetAllVersionsAsync();
            return installer.VersionName;
        }

        private async Task<IVersion> checkAndDownloadVanillaVersion(string mcVersion, NeoForgeInstallOptions options)
        {
            IVersion version = await _launcher.GetVersionAsync(mcVersion);

            await _launcher.InstallAsync(version, options.FileProgress, options.ByteProgress, options.CancellationToken);

            return version;
        }

        public Task<IEnumerable<NeoForgeVersion>> GetNeoForgeVersions(string mcVersion)
        {
            return _versionLoader.GetNeoForgeVersions(mcVersion);
        }

        private async Task<bool> checkVersionInstalled(string versionName)
        {
            try
            {
                await _launcher.GetVersionAsync(versionName);
                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        private string getJavaPath(IVersion version)
        {
            string text = _launcher.GetJavaPath(version);
            if (string.IsNullOrEmpty(text) || !File.Exists(text))
            {
                text = _launcher.GetDefaultJavaPath();
            }

            if (string.IsNullOrEmpty(text) || !File.Exists(text))
            {
                throw new InvalidOperationException("Cannot find any java binary. Set java binary path");
            }

            return text;
        }
    }
}
