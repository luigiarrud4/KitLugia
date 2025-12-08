using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            LoadSystemInfo();
        }

        private void LoadSystemInfo()
        {
            try
            {
                TxtPCName.Text = System.Environment.MachineName;
                double ram = SystemUtils.GetTotalSystemRamGB();
                TxtSpecs.Text = $"{ram:F0} GB de RAM • {System.Environment.OSVersion.VersionString}";
            }
            catch { /* Ignora erro se falhar leitura */ }
        }

        // Método auxiliar para chamar a navegação da MainWindow
        private void RequestNavigation(string tag)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.NavigateToPage(tag);
            }
        }

        // Eventos dos Botões Grandes do Dashboard
        private void BtnGoToTweaks_Click(object sender, RoutedEventArgs e) => RequestNavigation("⚡");
        private void BtnGoToCleanup_Click(object sender, RoutedEventArgs e) => RequestNavigation("💿");
        private void BtnGoToNetwork_Click(object sender, RoutedEventArgs e) => RequestNavigation("🌐");
        private void BtnGoToPrivacy_Click(object sender, RoutedEventArgs e) => RequestNavigation("🛡️");
    }
}