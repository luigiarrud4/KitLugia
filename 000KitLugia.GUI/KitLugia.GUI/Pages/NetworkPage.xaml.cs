using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using KitLugia.Core;
using KitLugia.GUI.Controls; // Para usar LugiaMsgBox

using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class NetworkPage : Page
    {
        private bool _isLoading = true;
        private readonly SolidColorBrush _colorActive = new SolidColorBrush(Color.FromRgb(108, 203, 95)); // Verde
        private readonly SolidColorBrush _colorDefault = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Cinza

        public NetworkPage()
        {
            InitializeComponent();
            LoadStatus();
        }

        private async void LoadStatus()
        {
            await Task.Run(() =>
            {
                // Verifica o estado atual dos tweaks de rede
                bool tcpOptimized = SystemTweaks.IsTcpIpLatencyTweakApplied();

                // Nota: Verificamos o PRIMEIRO adaptador ativo encontrado
                var adapters = SystemTweaks.GetActiveNetworkAdapters();
                bool driverOptimized = false;

                if (adapters.Count > 0)
                {
                    string path = SystemTweaks.FindNetworkAdapterRegistryPath(adapters[0]);
                    if (path != null)
                        driverOptimized = SystemTweaks.AreNetworkDriverOptimizationsApplied(path);
                }

                Dispatcher.Invoke(() =>
                {
                    _isLoading = true;

                    ChkTcp.IsChecked = tcpOptimized;
                    UpdateLabel(StatusTcp, tcpOptimized);

                    ChkDriver.IsChecked = driverOptimized;
                    UpdateLabel(StatusDriver, driverOptimized);

                    _isLoading = false;
                });
            });
        }

        private void UpdateLabel(TextBlock label, bool isActive)
        {
            label.Text = isActive ? "Otimizado" : "Padrão";
            label.Foreground = isActive ? _colorActive : _colorDefault;
        }

        private void ShowAlert(string message, string title = "REDE")
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowNotification(message, title);
        }

        // --- BOTÕES DE DNS ---

        private async void ApplyDns(string provider)
        {
            // Efeito visual simples: desabilitar temporariamente
            ShowAlert($"Aplicando DNS {provider}...\nAguarde um momento.", "Configurando Rede");

            var result = await Task.Run(() => Toolbox.SetDns(provider));

            ShowAlert(result.Message, result.Success ? "Sucesso" : "Erro");
        }

        private void BtnDnsCloudflare_Click(object sender, RoutedEventArgs e) => ApplyDns("Cloudflare");
        private void BtnDnsGoogle_Click(object sender, RoutedEventArgs e) => ApplyDns("Google");
        private void BtnDnsReset_Click(object sender, RoutedEventArgs e) => ApplyDns("DHCP");

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            var result = await Task.Run(() => Toolbox.FlushDnsCache());
            ShowAlert(result.Message, "Limpeza de Cache");
        }

        // --- SWITCHES (TCP/DRIVER) ---

        private void ChkTcp_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            SystemTweaks.ToggleTcpIpLatencyTweak();

            bool isActive = ChkTcp.IsChecked == true;
            UpdateLabel(StatusTcp, isActive);
            ShowAlert("Configurações TCP/IP alteradas. Reinicie o computador para aplicar a latência.", "Reinício Necessário");
        }

        private void ChkDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            var adapters = SystemTweaks.GetActiveNetworkAdapters();
            if (adapters.Count == 0)
            {
                ShowAlert("Nenhum adaptador de rede ativo encontrado.", "Erro");
                ChkDriver.IsChecked = false;
                return;
            }

            // Aplica no primeiro adaptador principal encontrado (simplificação para GUI)
            // Idealmente em versão futura listaríamos adaptadores em um combobox.
            string path = SystemTweaks.FindNetworkAdapterRegistryPath(adapters[0]);

            if (string.IsNullOrEmpty(path))
            {
                ShowAlert("Registro do driver não encontrado.", "Erro");
                return;
            }

            SystemTweaks.ToggleNetworkDriverOptimizations(path);

            bool isActive = ChkDriver.IsChecked == true;
            UpdateLabel(StatusDriver, isActive);
            ShowAlert($"Driver de Rede {(isActive ? "Otimizado" : "Restaurado")}.\nA conexão pode cair brevemente.", "Driver de Rede");
        }
    }
}