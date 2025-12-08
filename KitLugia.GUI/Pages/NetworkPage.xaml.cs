using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using KitLugia.Core;
using KitLugia.GUI.Controls;

// !!! IMPORTANTE: ESSES ALIAS RESOLVEM O ERRO CS0104 DE AMBIGUIDADE NOS PRINTS !!!
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class NetworkPage : Page
    {
        private bool _isLoading = true;
        private readonly SolidColorBrush _colorActive = new SolidColorBrush(Color.FromRgb(108, 203, 95)); // Verde
        private readonly SolidColorBrush _colorDefault = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Cinza
        private readonly SolidColorBrush _colorWarning = new SolidColorBrush(Color.FromRgb(244, 129, 32)); // Laranja

        public NetworkPage()
        {
            InitializeComponent();
            LoadStatus();
        }

        private async void LoadStatus()
        {
            await Task.Run(() =>
            {
                bool tcpOptimized = SystemTweaks.IsTcpIpLatencyTweakApplied();
                var adapters = SystemTweaks.GetActiveNetworkAdapters();
                bool driverOptimized = false;
                if (adapters.Any())
                {
                    string? guid = adapters.FirstOrDefault()?["GUID"]?.ToString();
                    if (!string.IsNullOrEmpty(guid))
                    {
                        string? path = SystemTweaks.FindNetworkAdapterRegistryPath(guid);
                        if (!string.IsNullOrEmpty(path))
                            driverOptimized = SystemTweaks.AreNetworkDriverOptimizationsApplied(path);
                    }
                }
                var dnsInfo = Toolbox.GetActiveDnsInfo();
                Dispatcher.Invoke(() =>
                {
                    _isLoading = true;
                    ChkTcp.IsChecked = tcpOptimized;
                    UpdateLabel(StatusTcp, tcpOptimized);
                    ChkDriver.IsChecked = driverOptimized;
                    UpdateLabel(StatusDriver, driverOptimized);
                    UpdateDnsUi(dnsInfo.Provider, dnsInfo.DnsIp);
                    _isLoading = false;
                });
            });
        }

        private void UpdateDnsUi(string provider, string ip)
        {
            BtnCloudflare.Tag = null;
            BtnGoogle.Tag = null;
            BtnDhcp.Tag = null;
            TxtCurrentDnsIp.Text = string.IsNullOrEmpty(ip) || ip == "N/A" ? "Automático / DHCP" : ip;
            TxtCurrentDnsIp.Foreground = _colorDefault;
            if (provider.Equals("Cloudflare", System.StringComparison.OrdinalIgnoreCase)) { BtnCloudflare.Tag = "Selected"; TxtCurrentDnsIp.Foreground = _colorActive; }
            else if (provider.Equals("Google", System.StringComparison.OrdinalIgnoreCase)) { BtnGoogle.Tag = "Selected"; TxtCurrentDnsIp.Foreground = _colorActive; }
            else if (provider.Equals("Automático (DHCP)", System.StringComparison.OrdinalIgnoreCase)) { BtnDhcp.Tag = "Selected"; TxtCurrentDnsIp.Foreground = _colorActive; }
            else { TxtCurrentDnsIp.Text = $"{ip} (Personalizado)"; TxtCurrentDnsIp.Foreground = _colorWarning; }
        }

        private void UpdateLabel(TextBlock label, bool isActive)
        {
            label.Text = isActive ? "Otimizado" : "Padrão";
            label.Foreground = isActive ? _colorActive : _colorDefault;
        }

        // --- BOTÕES DE DNS ---

        private async void ApplyDns(string provider)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            string providerForUi = provider.Equals("DHCP", System.StringComparison.OrdinalIgnoreCase) ? "Automático (DHCP)" : provider;
            UpdateDnsUi(providerForUi, "Aplicando...");
            mw.ShowInfo("AGUARDE", $"Configurando DNS {provider}...\nA internet pode reconectar.");

            var result = await Task.Run(() => Toolbox.SetDns(provider));

            if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
            else mw.ShowError("ERRO", result.Message);

            LoadStatus();
        }

        private void BtnDnsCloudflare_Click(object sender, RoutedEventArgs e) => ApplyDns("Cloudflare");
        private void BtnDnsGoogle_Click(object sender, RoutedEventArgs e) => ApplyDns("Google");
        private void BtnDnsReset_Click(object sender, RoutedEventArgs e) => ApplyDns("DHCP");

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;
            var result = await Task.Run(() => Toolbox.FlushDnsCache());
            mw.ShowSuccess("LIMPEZA DE CACHE", result.Message);
        }

        // --- SWITCHES (TCP/DRIVER) ---

        private void ChkTcp_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            SystemTweaks.ToggleTcpIpLatencyTweak();

            bool isActive = ChkTcp.IsChecked == true;
            UpdateLabel(StatusTcp, isActive);
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", "Configurações TCP/IP alteradas. Reinicie o computador para aplicar.");
        }

        private void ChkDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading || !(Application.Current.MainWindow is MainWindow mw)) return;

            var adapters = SystemTweaks.GetActiveNetworkAdapters();
            if (!adapters.Any())
            {
                mw.ShowError("ERRO", "Nenhum adaptador de rede ativo encontrado.");
                ChkDriver.IsChecked = false;
                return;
            }

            string? guid = adapters.FirstOrDefault()?["GUID"]?.ToString();
            if (string.IsNullOrEmpty(guid)) { mw.ShowError("ERRO", "Não foi possível identificar o adaptador de rede."); return; }

            string? path = SystemTweaks.FindNetworkAdapterRegistryPath(guid);
            if (string.IsNullOrEmpty(path)) { mw.ShowError("ERRO", "Registro do driver de rede não encontrado."); return; }

            SystemTweaks.ToggleNetworkDriverOptimizations(path);

            bool isActive = ChkDriver.IsChecked == true;
            UpdateLabel(StatusDriver, isActive);
            mw.ShowSuccess("DRIVER DE REDE", $"Otimizações do driver foram {(isActive ? "aplicadas" : "restauradas")}.\nA conexão pode cair brevemente.");
        }
    }
}