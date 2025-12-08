using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core; // Importante
using System.Windows.Forms; // Para FolderBrowserDialog (Driver Backup)
using MessageBox = System.Windows.MessageBox; // Resolve ambiguidade com Windows.Forms

namespace KitLugia.GUI.Pages
{
    // Classe simples para preencher o ComboBox
    public class PowerPlanItem
    {
        public string Name { get; set; } = "";
        public string Guid { get; set; } = "";
    }

    public partial class ToolsPage : Page
    {
        public ToolsPage()
        {
            InitializeComponent();
            Loaded += ToolsPage_Loaded;
        }

        private void ToolsPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshPowerPlans();
        }

        // --- ABA ENERGIA ---

        private void RefreshPowerPlans()
        {
            var plans = Toolbox.GetAllPowerPlans(); // Chama o Core
            var activePlan = Toolbox.GetActivePowerPlan();

            TxtCurrentPlan.Text = $"{activePlan.Name}";

            CmbPowerPlans.Items.Clear();
            foreach (var p in plans)
            {
                CmbPowerPlans.Items.Add(new PowerPlanItem { Name = p.Name, Guid = p.Guid });
            }

            // Seleciona o atual no combo
            CmbPowerPlans.SelectedValue = activePlan.Guid;
        }

        private void BtnActivatePlan_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPowerPlans.SelectedValue is string guid)
            {
                var result = Toolbox.SetActivePowerPlan(guid);
                if (result.Success)
                {
                    MessageBox.Show(result.Message, "Sucesso");
                    RefreshPowerPlans();
                }
                else
                {
                    MessageBox.Show(result.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnUltimate_Click(object sender, RoutedEventArgs e)
        {
            var result = await Task.Run(() => Toolbox.UnlockAndActivateUltimatePerformance());
            MessageBox.Show(result.Message);
            RefreshPowerPlans();
        }

        private async void BtnBitsum_Click(object sender, RoutedEventArgs e)
        {
            var result = await Task.Run(() => Toolbox.ImportAndActivateBitsumPlan());
            MessageBox.Show(result.Message);
            RefreshPowerPlans();
        }

        // --- ABA REDE ---

        private async void ApplyDns(string provider)
        {
            var result = await Task.Run(() => Toolbox.SetDns(provider));
            MessageBox.Show(result.Message, "Configuração de DNS");
        }

        private void BtnDnsCloudflare_Click(object sender, RoutedEventArgs e) => ApplyDns("Cloudflare");
        private void BtnDnsGoogle_Click(object sender, RoutedEventArgs e) => ApplyDns("Google");
        private void BtnDnsDhcp_Click(object sender, RoutedEventArgs e) => ApplyDns("DHCP");

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            var result = await Task.Run(() => Toolbox.FlushDnsCache());
            MessageBox.Show(result.Message);
        }

        private async void BtnNetReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Isso irá resetar suas configurações de rede e requer reinicialização.\nContinuar?", "Aviso", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                var result = await Task.Run(() => Toolbox.ResetNetworkStack());
                MessageBox.Show(result.Message);
            }
        }

        // --- ABA SISTEMA E EXTRAS ---

        private void BtnStoreReset_Click(object sender, RoutedEventArgs e)
        {
            Toolbox.ResetStoreCache();
            MessageBox.Show("Comando de reset da loja iniciado.\nAguarde a janela da loja abrir automaticamente.");
        }

        private void BtnGamingServices_Click(object sender, RoutedEventArgs e)
        {
            Toolbox.RepairGamingServices();
        }

        private async void BtnGpedit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("O processo de instalação será iniciado em uma janela separada.\nIsso pode levar alguns minutos.");
            await Task.Run(() => Toolbox.EnableGroupPolicyEditor());
        }

        private void BtnGodMode_Click(object sender, RoutedEventArgs e)
        {
            var result = Toolbox.ToggleGodMode();
            MessageBox.Show(result.Message);
        }

        private void BtnDriverBackup_Click(object sender, RoutedEventArgs e)
        {
            // Precisamos do FolderBrowserDialog (WinForms) ou pode-se usar uma lib externa.
            // Como System.Windows.Forms já é comum em WPF para isso:
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecione onde salvar o backup dos drivers";
                DialogResult result = dialog.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    Toolbox.BackupThirdPartyDrivers(dialog.SelectedPath);
                    MessageBox.Show("Backup iniciado em segundo plano (janela CMD).");
                }
            }
        }
    }
}