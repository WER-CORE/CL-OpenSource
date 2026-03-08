using CL_CLegendary_Launcher_.Class;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class PartherItem : UserControl
    {
        private Image ImageBg;
        private Image Image;
        private string _title;
        public string _description;
        private string port;

        public PartherItem()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            OpenInfoServerPanel.ToolTip = LocalizationManager.GetString("Servers.PartnerInfoTooltip", "Інформація про сервер");
            OpenInfoServerTXT.Text = LocalizationManager.GetString("Servers.PartnerVisitPage", "Перейти на сторінку");

            OnlinePlayerPanel.ToolTip = LocalizationManager.GetString("Servers.PartnerOnlineTooltip", "Онлайн");
            IPServerPanel.ToolTip = LocalizationManager.GetString("Servers.PartnerIpTooltip", "Натисніть щоб скопіювати");
            PlayServerPanel1.ToolTip = LocalizationManager.GetString("Servers.PartnerPlayTooltip", "Натисніть щоб почати грати");
            PlayServerTXT1.Text = LocalizationManager.GetString("Servers.PartnerPlayBtn", "Грати");
        }

        [Category("Custom Props")]
        public string _Title
        {
            get { return _title; }
            set { _title = value; TitleMain1.Text = value; }
        }

        [Category("Custom Props")]
        public Image ImageMain_
        {
            get { return Image; }
            set
            {
                Image = value;
                if (value?.Source is BitmapSource source)
                {
                    MainIcon3.Source = source;
                }
            }
        }

        [Category("Custom Props")]
        public Image ImageMainBg_
        {
            get { return ImageBg; }
            set
            {
                ImageBg = value;
            }
        }

        private void IPServerTXT_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try { Clipboard.SetText(IPServerTXT.Text); } catch { }
        }
    }
}