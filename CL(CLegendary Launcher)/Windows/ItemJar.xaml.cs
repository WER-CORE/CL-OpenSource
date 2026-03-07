using CL_CLegendary_Launcher_.Class;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class ItemJar : UserControl
    {
        public string UrlMods { get; set; }
        public string TypeSite { get; set; }
        public string ProjectId { get; set; }
        public int ModId { get; set; }
        public int FileId { get; set; }

        public Grid VersionSelectionGrid { get; set; }
        public ComboBox LoaderComboBox { get; set; }
        public ComboBox VersionComboBox { get; set; }

        public ItemJar()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            ToolTipDownloads.ToolTip = LocalizationManager.GetString("Mods.ItemJarDownloadsTooltip", "Кількість завантажень");
            ToolTipUpdate.ToolTip = LocalizationManager.GetString("Mods.ItemJarUpdateTooltip", "Останнє оновлення");
            ToolTipCreate.ToolTip = LocalizationManager.GetString("Mods.ItemJarCreateTooltip", "Дата створення");
            ToolTipAuthor.ToolTip = LocalizationManager.GetString("Mods.ItemJarAuthorTooltip", "Автор");
            DowloadTXT.Text = LocalizationManager.GetString("Mods.ItemJarInstallBtn", "Встановити");
        }

        [Category("Custom Props")]
        public string ModTitle
        {
            get => Title.Text;
            set => Title.Text = value;
        }

        [Category("Custom Props")]
        public string ModDescription
        {
            get => Description.Text;
            set => Description.Text = value;
        }

        [Category("Custom Props")]
        public string Author
        {
            get => User_AuthorTXT.Text;
            set => User_AuthorTXT.Text = value;
        }

        [Category("Custom Props")]
        public string CreateDate
        {
            get => DataCreateModTXT.Text;
            set => DataCreateModTXT.Text = value?.ToString();
        }

        [Category("Custom Props")]
        public string LastUpdateDate
        {
            get => DataUpdateLastTXT.Text;
            set => DataUpdateLastTXT.Text = value?.ToString();
        }

        [Category("Custom Props")]
        public string DownloadCount
        {
            get => DowloadCountTXT.Text;
            set => DowloadCountTXT.Text = value;
        }

        [Category("Custom Props")]
        public BitmapImage ModImage
        {
            get => (BitmapImage)ModIcon.Source;
            set => ModIcon.Source = value;
        }
    }
}