using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using KitLugia.GUI.Controls;
using System.Windows.Forms; // Para OpenFileDialog
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;
using System.IO;
using System.Collections.Generic;

namespace KitLugia.GUI.Pages
{
    public partial class ServicesPage : Page
    {
        // Cache da lista para filtros rápidos
        private List<ServiceInfo> _allServices = new();

        public ServicesPage()
        {
            InitializeComponent();
            Loaded += ServicesPage_Loaded;
        }

        private void ServicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStartupApps();
            LoadServices();
        }

        #region --- ABA 1: INICIALIZAÇÃO (STARTUP) ---

        private async void LoadStartupApps()
        {
            var apps = await Task.Run(() => StartupManager.GetStartupAppsWithDetails(false));

            var sortedApps = apps
                .OrderByDescending(a => a.Status.ToString() == "Elevated")
                .ThenByDescending(a => a.Status.ToString() == "Enabled")
                .ThenBy(a => a.Name)
                .ToList();

            GridStartup.ItemsSource = sortedApps;
        }

        private void BtnRefreshStartup_Click(object sender, RoutedEventArgs e) => LoadStartupApps();

        private async void BtnToggleStartup_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp)
            {
                bool willEnable = selectedApp.Status == StartupStatus.Disabled;

                if (selectedApp.Status == StartupStatus.Elevated || (selectedApp.Location.Contains("Agendador") && !willEnable))
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        if (!await mw.ShowConfirmationDialog($"O item '{selectedApp.Name}' é gerenciado pelo Agendador de Tarefas (Admin).\nDeseja alterar seu estado?")) return;
                    }
                }

                var result = await Task.Run(() => StartupManager.SetStartupItemState(selectedApp.Name, willEnable));

                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    if (result.Success)
                    {
                        string action = willEnable ? "Habilitado" : "Desabilitado";
                        mainWin.ShowSuccess("STARTUP", $"{selectedApp.Name} foi {action} com sucesso.");
                        LoadStartupApps();
                    }
                    else mainWin.ShowError("ERRO", result.Message);
                }
            }
            else
            {
                if (Application.Current.MainWindow is MainWindow mw) mw.ShowInfo("SELEÇÃO", "Selecione um item na lista.");
            }
        }

        private async void BtnRemoveStartup_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp)
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    if (!await mw.ShowConfirmationDialog($"TEM CERTEZA? Isso excluirá permanentemente '{selectedApp.Name}' da inicialização.")) return;

                    var result = await Task.Run(() => StartupManager.RemoveStartupItem(selectedApp.Name));

                    if (result.Success)
                    {
                        mw.ShowSuccess("REMOVIDO", result.Message);
                        LoadStartupApps();
                    }
                    else mw.ShowError("ERRO", result.Message);
                }
            }
            else
            {
                if (Application.Current.MainWindow is MainWindow mw) mw.ShowInfo("SELEÇÃO", "Selecione um item para remover.");
            }
        }

        // Lógica do Menu "Adicionar"
        private void BtnAddStartup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private string? PickFile()
        {
            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK) return openFileDialog.FileName;
            }
            return null;
        }

        private void MenuAddNormal_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            CreateStandardShortcut(System.IO.Path.GetFileNameWithoutExtension(p), p);
            if (Application.Current.MainWindow is MainWindow mw) mw.ShowSuccess("PADRÃO", "Adicionado com sucesso.");
            LoadStartupApps();
        }

        private void MenuAddAdmin_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            var res = StartupManager.CreateElevatedStartupTask(System.IO.Path.GetFileNameWithoutExtension(p), p, null);
            if (Application.Current.MainWindow is MainWindow mw) { if (res.Success) mw.ShowSuccess("ADMIN", res.Message); else mw.ShowError("ERRO", res.Message); }
            LoadStartupApps();
        }

        private void MenuAddDelayed_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            var res = StartupManager.CreateDelayedStartupTask(System.IO.Path.GetFileNameWithoutExtension(p), p, null);
            if (Application.Current.MainWindow is MainWindow mw) { if (res.Success) mw.ShowSuccess("ATRASO", res.Message); else mw.ShowError("ERRO", res.Message); }
            LoadStartupApps();
        }

        private void MenuAddAdminDelayed_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            var res = StartupManager.CreateElevatedDelayedStartupTask(System.IO.Path.GetFileNameWithoutExtension(p), p, null);
            if (Application.Current.MainWindow is MainWindow mw) { if (res.Success) mw.ShowSuccess("ADMIN + ATRASO", res.Message); else mw.ShowError("ERRO", res.Message); }
            LoadStartupApps();
        }

        private void CreateStandardShortcut(string name, string targetPath)
        {
            string folder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Startup);
            string link = System.IO.Path.Combine(folder, $"{name}.lnk");
            string script = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{link}');$s.TargetPath='{targetPath}';$s.Save()";
            SystemUtils.RunExternalProcess("powershell", $"-Command \"{script}\"", hidden: true);
        }

        #endregion

        #region --- ABA 2: OTIMIZAÇÃO DE SERVIÇOS ---

        private async void LoadServices()
        {
            // 1. SALVA O ITEM SELECIONADO ATUALMENTE
            string? selectedServiceId = (GridServices.SelectedItem as ServiceInfo)?.Name;

            // 2. Carrega em background
            var services = await Task.Run(() => BackgroundProcessManager.GetAllServices());
            _allServices = services;

            // 3. Aplica filtro
            ApplyServiceFilter();

            // 4. RESTAURA A SELEÇÃO E O SCROLL (FIX PARA A LISTA NÃO PULAR)
            if (selectedServiceId != null)
            {
                var itemToSelect = GridServices.Items.Cast<ServiceInfo>()
                    .FirstOrDefault(s => s.Name == selectedServiceId);

                if (itemToSelect != null)
                {
                    GridServices.SelectedItem = itemToSelect;
                    GridServices.ScrollIntoView(itemToSelect);
                }
            }
        }

        private void ApplyServiceFilter()
        {
            string filter = TxtSearchService.Text.Trim().ToLower();
            bool showDangerous = ChkShowDangerous.IsChecked == true;

            var filtered = _allServices.Where(s =>
            {
                bool matchesText = string.IsNullOrEmpty(filter) ||
                                   s.DisplayName.ToLower().Contains(filter) ||
                                   s.Name.ToLower().Contains(filter);

                bool matchesSafety = showDangerous || s.Safety != ServiceSafetyLevel.Dangerous;

                return matchesText && matchesSafety;
            }).ToList();

            GridServices.ItemsSource = filtered;

            if (TxtServiceCount != null)
                TxtServiceCount.Text = $"{filtered.Count} Serviços";
        }

        private void TxtSearchService_TextChanged(object sender, TextChangedEventArgs e) => ApplyServiceFilter();
        private void ChkShowDangerous_Click(object sender, RoutedEventArgs e) => ApplyServiceFilter();
        private void BtnRefreshServices_Click(object sender, RoutedEventArgs e) => LoadServices();

        // --- AÇÕES DE PRESET ---
        private async void RunServicePreset(string presetName, string friendlyName)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                bool confirmed = await mw.ShowConfirmationDialog(
                    $"Aplicar otimização '{friendlyName}'?\nIsso irá parar/desativar vários serviços.");

                if (!confirmed) return;

                mw.ShowInfo("AGUARDE", $"Aplicando perfil de serviços: {friendlyName}...");

                var result = await Task.Run(() => BackgroundProcessManager.ApplyServicePreset(presetName));

                if (result.Success) mw.ShowSuccess("SERVIÇOS", result.Message);
                else mw.ShowError("ERRO", result.Message);

                LoadServices();
            }
        }
        private void BtnSafeOpt_Click(object sender, RoutedEventArgs e) => RunServicePreset("Safe", "Seguro");
        private void BtnGamerOpt_Click(object sender, RoutedEventArgs e) => RunServicePreset("Gamer", "Gamer");
        private void BtnRestoreServices_Click(object sender, RoutedEventArgs e) => RunServicePreset("Restore", "Padrão");

        // --- AÇÕES INDIVIDUAIS ---
        private async void ChangeServiceState(string mode)
        {
            if (GridServices.SelectedItem is ServiceInfo svc && Application.Current.MainWindow is MainWindow mw)
            {
                // Proteção para Serviços Críticos
                if (svc.Safety == ServiceSafetyLevel.Dangerous)
                {
                    // Lista de serviços intocáveis (Kernel/Security)
                    var hardlockedServices = new HashSet<string> { "Schedule", "MpsSvc", "Audiosrv", "RpcSs", "ProfSvc", "EventLog", "BFE", "Dhcp", "Dnscache" };
                    if (hardlockedServices.Contains(svc.Name))
                    {
                        mw.ShowError("BLOQUEADO", $"O Windows protege o serviço '{svc.DisplayName}'.\nEle não pode ser modificado.");
                        return;
                    }

                    if (mode == "disabled")
                    {
                        if (!await mw.ShowConfirmationDialog($"PERIGO: '{svc.DisplayName}' é um serviço CRÍTICO.\nDesativá-lo pode causar falhas graves. Tem certeza?")) return;
                    }
                }

                if (mode == "default")
                {
                    mw.ShowInfo("AGUARDE", $"Restaurando '{svc.DisplayName}'...");
                    var result = await Task.Run(() => BackgroundProcessManager.ResetServiceToDefault(svc.Name));
                    if (result.Success) mw.ShowSuccess("RESTAURADO", result.Message); else mw.ShowError("ERRO", result.Message);
                }
                else
                {
                    string modeUi = mode == "auto" ? "Automático" : (mode == "demand" ? "Manual" : "Desativado");
                    mw.ShowInfo("AGUARDE", $"Configurando '{svc.DisplayName}' para {modeUi}...");

                    var result = await Task.Run(() => BackgroundProcessManager.ToggleServiceState(svc.Name, mode));

                    if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                    else
                    {
                        // Mensagem de erro mais bonita
                        string msg = result.Message;
                        if (msg.Contains("5") || msg.Contains("Acesso negado")) msg = "Acesso Negado pelo Windows (Serviço Protegido).";
                        mw.ShowError("ERRO", msg);
                    }
                }

                LoadServices();
            }
        }

        private void MenuSvcAuto_Click(object sender, RoutedEventArgs e) => ChangeServiceState("auto");
        private void MenuSvcManual_Click(object sender, RoutedEventArgs e) => ChangeServiceState("demand");
        private void MenuSvcDisabled_Click(object sender, RoutedEventArgs e) => ChangeServiceState("disabled");
        private void MenuSvcDefault_Click(object sender, RoutedEventArgs e) => ChangeServiceState("default");

        #endregion
    }
}