using CL_CLegendary_Launcher_.Models;
using Newtonsoft.Json;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace CL_CLegendary_Launcher_.Windows
{
    public static class ModJsonManager
    {
public class ModrinthIndex
{
    public int formatVersion { get; set; }
    public string game { get; set; }
    public string versionId { get; set; }
    public string name { get; set; }
    public List<ModrinthFile> files { get; set; }
}

public class ModrinthFile
{
    public string path { get; set; }
    public Hashes hashes { get; set; }
    public Env env { get; set; }
    public List<string> downloads { get; set; }
    public long fileSize { get; set; }
}

public class Hashes
{
    public string sha1 { get; set; }
    public string sha512 { get; set; }
}

public class Env
{
    public string client { get; set; }
    public string server { get; set; }
}

        public class CurseForgeManifest
        {
            public MinecraftData minecraft { get; set; }
            public string manifestType { get; set; }
            public int manifestVersion { get; set; }
            public string name { get; set; }
            public string version { get; set; }
            public string author { get; set; }
            public List<CurseForgeFile> files { get; set; }
        }

        public class MinecraftData
        {
            public string version { get; set; }
            public List<ModLoader> modLoaders { get; set; }
        }

        public class ModLoader
        {
            public string id { get; set; }
            public bool primary { get; set; }
        }

        public class CurseForgeFile
        {
            public string projectID { get; set; }
            public string fileID { get; set; } 
            public bool required { get; set; }
        }


        public class CurseFileLink
        {
            public int fileID { get; set; }
            public string fileName { get; set; }
        }

        public class CustomModEntry
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string ProjectId { get; set; }
            public string Loader { get; set; }
            public string LoaderType { get; set; }
            public string Version { get; set; }
            public string Url { get; set; }
        }


        public static void RemoveModFromJson(string modFileName, ModpackInfo modpack)
        {
            try
            {
                string jsonPath = modpack.PathJson;
                if (!File.Exists(jsonPath)) return;

                string json = File.ReadAllText(jsonPath);

                if (modpack.TypeSite == "Custom")
                {
                    var modsList = JsonConvert.DeserializeObject<List<CustomModEntry>>(json);

                    modsList = modsList
                        .Where(m => !m.Url.Contains(Path.GetFileNameWithoutExtension(modFileName), StringComparison.OrdinalIgnoreCase))
                        .ToList();


                    string newJson = JsonConvert.SerializeObject(modsList, Formatting.Indented);
                    File.WriteAllText(jsonPath, newJson);
                }
                else if (modpack.TypeSite == "Modrinth")
                {
                    var index = JsonConvert.DeserializeObject<ModrinthIndex>(json);

                    index.files = index.files
                        .Where(f => !Path.GetFileName(f.path).Equals(modFileName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    File.WriteAllText(jsonPath, JsonConvert.SerializeObject(index, Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка при видаленні мода з JSON: {ex.Message}", "Помилка");
            }
        }
        public static void RemoveModFromCurseForgeManifest(string jsonPath, int selectedIndex)
        {
            if (!File.Exists(jsonPath)) return;

            var json = File.ReadAllText(jsonPath);
            var curseData = JsonConvert.DeserializeObject<CurseForgeManifest>(json);

            if (selectedIndex >= 0 && selectedIndex < curseData.files.Count)
            {
                curseData.files.RemoveAt(selectedIndex);
                File.WriteAllText(jsonPath, JsonConvert.SerializeObject(curseData, Formatting.Indented));
            }
        }

    }

    public partial class ItemJarManeger : UserControl
    {
        public ModpackInfo CurrentModpack { get; set; }
        public ImageSource ModIconValue { get; set; } 
        public string pathmods; 
        public bool Off_OnMod = true; 
        public bool IsModPack = false;
        public int Index { get; set; } 
        private ImageBrush toggleOnBrush;
        private ImageBrush toggleOffBrush;

        public ItemJarManeger()
        {
            InitializeComponent();
            InitializeToggleImages();
        }

        private void InitializeToggleImages()
        {
        }

        private void Off_OnMods_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(pathmods) || !File.Exists(pathmods))
            {
                MessageBox.Show("Файл мода не знайдено. Перевірте шлях.", "Помилка");
                return;
            }

            string newFilePath;
            try
            {
                if (Off_OnMod == true)
                {
                    Off_OnMod = false;
                    Off_OnMods.Background = toggleOffBrush;

                    newFilePath = pathmods + ".disabled";
                }
                else
                {
                    Off_OnMod = true;
                    Off_OnMods.Background = toggleOnBrush;

                    newFilePath = pathmods.Replace(".disabled", "");
                }

                File.Move(pathmods, newFilePath);
                pathmods = newFilePath; 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося змінити статус мода.\nПомилка: {ex.Message}", "Помилка");
            }
        }

        private void DowloadTXT_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(pathmods) || !File.Exists(pathmods))
            {
                MessageBox.Show("Файл мода не знайдено. Перевірте шлях.", "Помилка");
                return;
            }

            try
            {
                File.Delete(pathmods);
                if (IsModPack && CurrentModpack != null)
                {
                    if(CurrentModpack.TypeSite == "CurseForge")
                    {
                        ModJsonManager.RemoveModFromCurseForgeManifest(CurrentModpack.PathJson, Index);
                    }
                    else if (CurrentModpack.TypeSite == "Custom")
                    {
                        string modFileName = System.IO.Path.GetFileName(pathmods);
                        ModJsonManager.RemoveModFromJson(pathmods, CurrentModpack);
                    }
                }
                var parentItemsControl = this.Parent as ItemsControl;
                if (parentItemsControl != null)
                {
                    parentItemsControl.Items.Remove(this);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося видалити мод.\nПомилка: {ex.Message}", "Помилка");
            }
        }
    }
}
