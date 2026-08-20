using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CL.Core.Services
{
    public class ModMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public byte[] IconBytes { get; set; }
    }

    public static class ModMetadataReader
    {
        public static async Task<ModMetadata> ReadMetadataAsync(string jarPath)
        {
            if (string.IsNullOrEmpty(jarPath) || !File.Exists(jarPath)) return null;

            return await Task.Run(() =>
            {
                try
                {
                    using (var archive = ZipFile.OpenRead(jarPath))
                    {
                        var metadata = new ModMetadata();
                        string iconPath = null;

                        var fabricEntry = archive.GetEntry("fabric.mod.json") ?? archive.GetEntry("quilt.mod.json");
                        if (fabricEntry != null)
                        {
                            using (var reader = new StreamReader(fabricEntry.Open()))
                            {
                                var json = reader.ReadToEnd();
                                try
                                {
                                    var jobj = JObject.Parse(json);
                                    metadata.Name = jobj["name"]?.ToString();
                                    metadata.Description = jobj["description"]?.ToString();
                                    
                                    var iconToken = jobj["icon"];
                                    if (iconToken != null)
                                    {
                                        if (iconToken.Type == Newtonsoft.Json.Linq.JTokenType.String)
                                        {
                                            iconPath = iconToken.ToString();
                                        }
                                        else if (iconToken.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                                        {
                                            var firstProp = ((JObject)iconToken).Properties().FirstOrDefault();
                                            if (firstProp != null) iconPath = firstProp.Value.ToString();
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (archive.GetEntry("META-INF/mods.toml") != null)
                        {
                            var tomlEntry = archive.GetEntry("META-INF/mods.toml");
                            using (var reader = new StreamReader(tomlEntry.Open()))
                            {
                                var toml = reader.ReadToEnd();
                                var nameMatch = Regex.Match(toml, @"displayName\s*=\s*""([^""]+)""");
                                if (nameMatch.Success) metadata.Name = nameMatch.Groups[1].Value;

                                var descMatch = Regex.Match(toml, @"description\s*=\s*'''(.*?)'''", RegexOptions.Singleline);
                                if (descMatch.Success) metadata.Description = descMatch.Groups[1].Value.Trim();

                                var logoMatch = Regex.Match(toml, @"logoFile\s*=\s*""([^""]+)""");
                                if (logoMatch.Success) iconPath = logoMatch.Groups[1].Value;
                            }
                        }
                        else if (archive.GetEntry("mcmod.info") != null)
                        {
                            var infoEntry = archive.GetEntry("mcmod.info");
                            using (var reader = new StreamReader(infoEntry.Open()))
                            {
                                var json = reader.ReadToEnd();
                                try
                                {
                                    JToken root = JToken.Parse(json);
                                    JObject modObj = null;
                                    
                                    if (root is JArray arr && arr.Count > 0)
                                    {
                                        modObj = arr[0] as JObject;
                                    }
                                    else if (root is JObject obj && obj["modList"] is JArray modList && modList.Count > 0)
                                    {
                                        modObj = modList[0] as JObject;
                                    }

                                    if (modObj != null)
                                    {
                                        metadata.Name = modObj["name"]?.ToString();
                                        metadata.Description = modObj["description"]?.ToString();
                                        iconPath = modObj["logoFile"]?.ToString();
                                    }
                                }
                                catch { }
                            }
                        }
                        else if (archive.GetEntry("pack.mcmeta") != null)
                        {
                            var packEntry = archive.GetEntry("pack.mcmeta");
                            using (var reader = new StreamReader(packEntry.Open()))
                            {
                                var json = reader.ReadToEnd();
                                try
                                {
                                    var jobj = JObject.Parse(json);
                                    if (jobj["pack"] is JObject packObj)
                                    {
                                        metadata.Description = packObj["description"]?.ToString();
                                    }
                                }
                                catch { }
                            }
                        }

                        if (string.IsNullOrEmpty(iconPath))
                        {
                            if (archive.GetEntry("pack.png") != null) iconPath = "pack.png";
                            else if (archive.GetEntry("icon.png") != null) iconPath = "icon.png";
                            else if (archive.GetEntry("logo.png") != null) iconPath = "logo.png";
                        }

                        if (!string.IsNullOrEmpty(iconPath))
                        {
                            if (iconPath.StartsWith("/")) iconPath = iconPath.Substring(1);
                            
                            var iconEntry = archive.GetEntry(iconPath);
                            if (iconEntry != null)
                            {
                                using (var stream = iconEntry.Open())
                                using (var ms = new MemoryStream())
                                {
                                    stream.CopyTo(ms);
                                    metadata.IconBytes = ms.ToArray();
                                }
                            }
                        }

                        return string.IsNullOrEmpty(metadata.Name) && metadata.IconBytes == null ? null : metadata;
                    }
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}
