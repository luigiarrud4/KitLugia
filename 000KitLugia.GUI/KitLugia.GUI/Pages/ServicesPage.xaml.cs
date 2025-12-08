using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using System.Windows.Forms; // Para OpenFileDialog
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace KitLugia.GUI.Pages
{
    public partial class ServicesPage : Page
    {
        public ServicesPage()
        {
            InitializeComponent();
            Loaded += ServicesPage_Loaded;
        }

        private void ServicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStartupApps();
        }

        // --- ABA STARTUP ---

        private async void LoadStartupApps()
        {
            var apps = await Task.Run(() => StartupManager.GetStartupAppsWithDetails());
            GridStartup.ItemsSource = apps;
        }

        private void BtnRefreshStartup_Click(object sender, RoutedEventArgs e)
        {
            LoadStartupApps();
        }

        private async void BtnToggleStartup_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp)
            {
                bool enable = selectedApp.Status == StartupStatus.Disabled;

                var result = await Task.Run(() =>
                    StartupManager.SetStartupItemState(selectedApp.Name, enable));

                if (result.Success)
                {
                    MessageBox.Show(result.Message, "Sucesso");
                    LoadStartupApps();
                }
                else
                {
                    MessageBox.Show(result.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Selecione um item na lista.");
            }
        }

        private void BtnAddStartup_Click(object sender, RoutedEventArgs e)
        {
            // Usa OpenFileDialog do WinForms
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string path = openFileDialog.FileName;
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);

                    // Cria como tarefa elevada por padrão no seu kit
                    var result = StartupManager.CreateElevatedStartupTask(name, path, null);
                    MessageBox.Show(result.Message);
                    LoadStartupApps();
                }
            }
        }

        // --- ABA SERVIÇOS ---
        // Aqui chamamos métodos que precisamos garantir que existam no Core

        private async void RunServicePreset(string presetName)
        {
            if (MessageBox.Show($"Aplicar otimização '{presetName}' nos serviços?\nIsso pode parar funcionalidades do Windows.", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                // ATENÇÃO: Você precisa ter um método público no Core para isso.
                // Exemplo: Toolbox.ApplyServiceOptimization(presetName);

                // Como placeholder, vou mostrar uma mensagem.
                // Se você tiver o método, substitua aqui.
                await Task.Run(() =>
                {
                    // Toolbox.ApplyServicePreset(presetName); 
                });

                MessageBox.Show($"Preset '{presetName}' aplicado com sucesso! (Simulação)");
            }
        }

        private void BtnSafeOpt_Click(object sender, RoutedEventArgs e) => RunServicePreset("Safe");
        private void BtnGamerOpt_Click(object sender, RoutedEventArgs e) => RunServicePreset("Gamer");
        private void BtnRestoreServices_Click(object sender, RoutedEventArgs e) => RunServicePreset("Restore");
    }
}