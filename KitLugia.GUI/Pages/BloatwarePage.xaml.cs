using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using KitLugia.Core;
using KitLugia.GUI.Controls;

// --- CORREÇÃO DOS CONFLITOS DE AMBIGUIDADE ---
using Button = System.Windows.Controls.Button;

using Application = System.Windows.Application;

#pragma warning disable CS4014 // Chamadas async não aguardadas são intencionais para operações em background

namespace KitLugia.GUI.Pages
{
    public partial class BloatwarePage : Page
    {
        // Correção de aviso de Nulo
        private ObservableCollection<BloatwareApp>? AppsCollection;

        public BloatwarePage()
        {
            InitializeComponent();
            LoadApps();
        }

        private async Task LoadApps()
        {
            if (LoadingPanel != null) LoadingPanel.Visibility = Visibility.Visible;
            if (AppsList != null) AppsList.ItemsSource = null;

            AppsCollection = new ObservableCollection<BloatwareApp>();

            var apps = await Task.Run(() => SystemTweaks.GetBloatwareAppsStatus());

            foreach (var app in apps)
            {
                AppsCollection.Add(app);
            }

            if (AppsList != null) AppsList.ItemsSource = AppsCollection;
            if (LoadingPanel != null) LoadingPanel.Visibility = Visibility.Collapsed;
        }

        private async void BtnAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is BloatwareApp app)
            {
                if (app.IsInstalled)
                {
                    if (MessageBox.Show($"Remover {app.DisplayName}?", "Bloatware", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        btn.Content = "⏳";
                        btn.IsEnabled = false;
                        await Task.Run(() => SystemTweaks.RemoveBloatwareApp(app.PackageName));

                        // Atualização visual simples
                        btn.Content = "REMOVIDO";
                        // LoadApps(); // Descomente para recarregar lista total
                    }
                }
                else
                {
                    // Lógica de Reinstalar
                    if (string.IsNullOrEmpty(app.StoreId))
                    {
                        MessageBox.Show("Este app não tem ID de loja vinculado.", "Indisponível");
                        return;
                    }
                    SystemTweaks.ReinstallBloatwareApp(app.StoreId);
                }
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadApps();
        }
    }
}