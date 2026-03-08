using System;

namespace CL_CLegendary_Launcher_.Models
{
    public class AppSettings
    {
        public int width { get; set; } = 800;
        public int height { get; set; } = 600;
        public bool EnableLog { get; set; } = false;
        public bool FullScreen { get; set; } = false;
        public int OP { get; set; } = 2048;
        public string bgImage { get; set; } = "";
        public bool MicrosoftAccount { get; set; } = false;
        public string Them { get; set; } = "Dark";
        public string Section_colour { get; set; } = "";
        public string Background_colour { get; set; } = "";
        public string Additional_colour { get; set; } = "";
        public string Text_colour { get; set; } = "";
        public string Button_colour { get; set; } = "";
        public string PathLacunher { get; set; } = "";
        public bool CloseLaucnher { get; set; } = false;
        public bool ModDep { get; set; } = false;
        public string EncryptKey { get; set; } = "";
        public bool TutorialComplete { get; set; } = false;
        public double StatsGameVanila { get; set; } = 0;
        public double StatsGameMod { get; set; } = 0;
        public double StatsGameServer { get; set; } = 0;
        public bool OfflineModLauncher { get; set; } = false;
        public bool DisableGlassEffect { get; set; } = true;
        public string LanguageCode { get; set; } = null;
        public int LastSelectedType { get; set; } = -1;
        public string LastSelectedVersion { get; set; } = "";
        public string LastSelectedModVersion { get; set; } = "";
        public DateTime EulaAcceptedDate { get; set; } = DateTime.MinValue;
        public bool IsDocsTutorialShown { get; set; } = false;
        public string LoadScreenBackground { get; set; } = "";
        public string LoadScreenBarColor { get; set; } = "";
        public int SelectIndexAccount { get; set; } = -1;
        public byte MaxAutoBackups { get; set; } = 0;
        public bool EnableAutoBackup { get; set; } = false;
        public bool EnableMemeSound { get; set; } = false;
        public string CustomGeminiKey { get; set; } = "";
        public object this[string propertyName]
        {
            get
            {
                var property = GetType().GetProperty(propertyName);
                if (property != null)
                {
                    return property.GetValue(this, null);
                }
                throw new ArgumentException($"Властивість з іменем '{propertyName}' не знайдена в AppSettings.");
            }
            set
            {
                var property = GetType().GetProperty(propertyName);
                if (property != null)
                {
                    property.SetValue(this, Convert.ChangeType(value, property.PropertyType), null);
                }
                else
                {
                    throw new ArgumentException($"Властивість з іменем '{propertyName}' не знайдена в AppSettings.");
                }
            }
        }
    }
}