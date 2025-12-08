using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public class PowerPlanItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Guid { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public bool CanDelete { get; set; } = false;

        private bool _isConfirmingDelete = false;
        public bool IsConfirmingDelete
        {
            get => _isConfirmingDelete;
            set { if (_isConfirmingDelete != value) { _isConfirmingDelete = value; OnPropertyChanged(nameof(IsConfirmingDelete)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class ToolsPage : Page
    {
        private readonly HashSet<string> _defaultGuids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            { "381b4222-f694-41f0-9685-ff5bb260df2e", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", "a1841308-3541-4fab-bc81-f71556f20b4a", "e9a42b02-d5df-448d-aa00-03f14749eb61" };

        public ToolsPage()
        {
            InitializeComponent();
            Loaded += ToolsPage_Loaded;
        }

        private void ToolsPage_Loaded(object sender, RoutedEventArgs e) => RefreshPowerPlans();

        private void RefreshPowerPlans()
        {
            if (CmbPowerPlans.ItemsSource is IEnumerable<PowerPlanItem> oldItems)
            {
                foreach (var item in oldItems) item.IsConfirmingDelete = false;
            }

            var plans = Toolbox.GetAllPowerPlans();
            var activePlan = Toolbox.GetActivePowerPlan();
            TxtCurrentPlan.Text = activePlan.Name;

            var powerPlanItems = new List<PowerPlanItem>();
            foreach (var p in plans)
            {
                bool isActive = p.Guid.Equals(activePlan.Guid, System.StringComparison.OrdinalIgnoreCase);
                powerPlanItems.Add(new PowerPlanItem
                {
                    Name = p.Name,
                    Guid = p.Guid,
                    IsActive = isActive,
                    CanDelete = !_defaultGuids.Contains(p.Guid) && !isActive
                });
            }
            CmbPowerPlans.ItemsSource = powerPlanItems;
            CmbPowerPlans.SelectedValue = activePlan.Guid;
        }

        private void BtnActivatePlan_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPowerPlans.SelectedValue is string guid && Application.Current.MainWindow is MainWindow mw)
            {
                var result = Toolbox.SetActivePowerPlan(guid);
                if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                else mw.ShowError("ERRO", result.Message);

                if (result.Success) RefreshPowerPlans();
            }
        }

        private async void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is PowerPlanItem planToDelete && Application.Current.MainWindow is MainWindow mw)
            {
                if (planToDelete.IsConfirmingDelete)
                {
                    var result = await Task.Run(() => Toolbox.DeletePowerPlan(planToDelete.Guid));
                    if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                    else mw.ShowError("ERRO", result.Message);

                    RefreshPowerPlans();
                }
                else
                {
                    if (CmbPowerPlans.ItemsSource is IEnumerable<PowerPlanItem> items)
                    {
                        foreach (var item in items) item.IsConfirmingDelete = false;
                    }

                    planToDelete.IsConfirmingDelete = true;

                    // TIMER REDUZIDO PARA 1.5 SEGUNDOS
                    await Task.Delay(1500);

                    if (planToDelete.IsConfirmingDelete)
                    {
                        planToDelete.IsConfirmingDelete = false;
                    }
                }
            }
        }

        private async void BtnUltimate_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = await Task.Run(() => Toolbox.UnlockAndActivateUltimatePerformance());
                if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                else mw.ShowInfo("AVISO", result.Message);
                RefreshPowerPlans();
            }
        }

        private async void BtnBitsum_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = await Task.Run(() => Toolbox.ImportAndActivateBitsumPlan());
                if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                else mw.ShowError("ERRO", result.Message);
                RefreshPowerPlans();
            }
        }

        private async void ApplyDns(string provider)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.ShowInfo("CONFIGURANDO DNS", $"Aplicando DNS {provider}. A rede pode reconectar.");
                var result = await Task.Run(() => Toolbox.SetDns(provider));
                if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                else mw.ShowError("ERRO", result.Message);
            }
        }

        private void BtnDnsCloudflare_Click(object sender, RoutedEventArgs e) => ApplyDns("Cloudflare");
        private void BtnDnsGoogle_Click(object sender, RoutedEventArgs e) => ApplyDns("Google");
        private void BtnDnsDhcp_Click(object sender, RoutedEventArgs e) => ApplyDns("DHCP");

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = await Task.Run(() => Toolbox.FlushDnsCache());
                mw.ShowSuccess("CACHE DE DNS", result.Message);
            }
        }

        private async void BtnNetReset_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.MessageBox.Show("Isso irá resetar suas configurações de rede e requer reinicialização.\nContinuar?", "Aviso Crítico", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    var result = await Task.Run(() => Toolbox.ResetNetworkStack());
                    mw.ShowInfo("REPARO DE REDE", result.Message);
                }
            }
        }

        private void BtnStoreReset_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = Toolbox.ResetStoreCache();
                mw.ShowSuccess("MICROSOFT STORE", result.Message);
            }
        }

        private void BtnGamingServices_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = Toolbox.RepairGamingServices();
                mw.ShowSuccess("GAMING SERVICES", result.Message);
            }
        }

        private async void BtnGpedit_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("GPEDIT", "O processo de instalação será iniciado em uma janela separada e pode levar alguns minutos.");

            await Task.Run(() => Toolbox.EnableGroupPolicyEditor());
        }

        private void BtnGodMode_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = Toolbox.ToggleGodMode();
                mw.ShowInfo("GOD MODE", result.Message);
            }
        }

        private void BtnDriverBackup_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecione onde salvar o backup dos drivers";
                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        var backupResult = Toolbox.BackupThirdPartyDrivers(dialog.SelectedPath);
                        mw.ShowSuccess("BACKUP DE DRIVERS", backupResult.Message);
                    }
                }
            }
        }
    }
}