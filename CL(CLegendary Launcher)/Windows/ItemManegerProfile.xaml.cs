using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CL_CLegendary_Launcher_.Windows
{
    public partial class ItemManegerProfile : UserControl
    {
        public event EventHandler SkinEditClicked;

        public enum AccountType
        {
            Microsoft,
            LittleSkin,
            Offline
        }

        public string NameAccount2 { get; set; }
        public string UUID { get; set; }
        public string AccessToken { get; set; }
        public string ImageUrl { get; set; }
        public int index { get; set; }

        private AccountType _typeAccount;

        public AccountType TypeAccount
        {
            get { return _typeAccount; }
            set
            {
                _typeAccount = value;
            }
        }

        public ItemManegerProfile()
        {
            InitializeComponent();
        }
    }
}