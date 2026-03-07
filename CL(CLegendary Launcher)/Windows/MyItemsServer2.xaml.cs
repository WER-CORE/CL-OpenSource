using CL_CLegendary_Launcher_.Class;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_
{
    public partial class MyItemsServer : UserControl
    {
        private Image ImageBg;
        private Image Image;
        private string _title;
        private string _description;

        public MyItemsServer()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            OpenInfoServerPanel.ToolTip = LocalizationManager.GetString("Servers.ServerItemInfoTooltip", "Інформація про сервер");
            OpenInfoServerTXT.Text = LocalizationManager.GetString("Servers.ServerPageBtn", "Перейти на сторінку");

            OnlinePlayerPanel.ToolTip = LocalizationManager.GetString("Servers.ServerOnlineTooltip", "Онлайн");
            IPServerPanel.ToolTip = LocalizationManager.GetString("Servers.ServerCopyIpTooltip", "Натисніть щоб скопіювати");
            PlayServerPanel1.ToolTip = LocalizationManager.GetString("Servers.ServerPlayBtn", "Натисніть щоб почати грати");
            PlayServerTXT1.Text = LocalizationManager.GetString("Servers.ServerPlayBtn", "Грати");
        }

        [Category("Custom Props")]
        public string _Title
        {
            get => _title;
            set { _title = value; TitleMain1.Text = value; }
        }

        [Category("Custom Props")]
        public string Description_
        {
            get => _description;
            set { _description = value; DescriptionMain2.Text = value; }
        }

        [Category("Custom Props")]
        public Image ImageMain_
        {
            get => Image;
            set
            {
                Image = value;
                if (value?.Source is BitmapSource source)
                {
                    MainIcon3.Source = source;
                }
            }
        }

        private void IPServerTXT_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { Clipboard.SetText(IPServerTXT.Text); } catch { }
        }
    }
}