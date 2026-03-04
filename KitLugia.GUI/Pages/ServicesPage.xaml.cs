using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using Microsoft.Win32; // Para OpenFileDialog

// Resolve conflito de nomes
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class ServicesPage : Page
    {
        private List<ServiceInfo> _allServices = new();
        private int _initialTabIndex = 0;

        public ServicesPage(int tabIndex = 0)
        {
            InitializeComponent();
            _initialTabIndex = tabIndex;
            Loaded += ServicesPage_Loaded;
        }

        private async void ServicesPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (MainTabs != null) MainTabs.SelectedIndex = _initialTabIndex;

            // Carrega os dados iniciais das abas principais
            await LoadStartupApps();
            await LoadServices();
            await LoadScheduledTasks();
        }

        // =========================================================
        // ABA 1: INICIALIZAÇÃO (STARTUP)
        // =========================================================
        #region Startup Logic

        private async Task LoadStartupApps()
        {
            var apps = await Task.Run(() => StartupManager.GetStartupAppsWithDetails(false));
            var sortedApps = apps
                .OrderByDescending(a => a.Status.ToString() == "Elevated")
                .ThenByDescending(a => a.Status.ToString() == "Enabled")
                .ThenBy(a => a.Name)
                .ToList();
            GridStartup.ItemsSource = sortedApps;
        }

        private async void BtnRefreshStartup_Click(object sender, RoutedEventArgs e) => await LoadStartupApps();

        private async void BtnToggleStartup_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp)
            {
                bool willEnable = selectedApp.Status == StartupStatus.Disabled;
                var result = await Task.Run(() => StartupManager.SetStartupItemState(selectedApp.Name, willEnable));

                if (Application.Current.MainWindow is MainWindow mw)
                {
                    if (result.Success)
                    {
                        mw.ShowSuccess("STARTUP", $"{selectedApp.Name} foi {(willEnable ? "Habilitado" : "Desabilitado")}.");
                        LoadStartupApps();
                    }
                    else mw.ShowError("ERRO", result.Message);
                }
            }
        }

        private async void BtnRemoveStartup_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp)
            {
                if (Application.Current.MainWindow is MainWindow mw)
                {
                    if (!await mw.ShowConfirmationDialog($"Excluir '{selectedApp.Name}' permanentemente?")) return;
                    var result = await Task.Run(() => StartupManager.RemoveStartupItem(selectedApp.Name));
                    if (result.Success) { mw.ShowSuccess("REMOVIDO", result.Message); LoadStartupApps(); }
                    else mw.ShowError("ERRO", result.Message);
                }
            }
        }

        // --- Adicionar Novo ---
        private void BtnAddStartup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private string? PickFile()
        {
            // Adicione "Microsoft.Win32." antes de OpenFileDialog
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executáveis (*.exe)|*.exe|Todos (*.*)|*.*"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        private void MenuAddNormal_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            // Cria atalho padrão via Powershell (Lógica simples)
            string name = System.IO.Path.GetFileNameWithoutExtension(p);
            string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string script = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{startupDir}\\{name}.lnk');$s.TargetPath='{p}';$s.Save()";
            SystemUtils.RunExternalProcess("powershell", $"-Command \"{script}\"", hidden: true);
            LoadStartupApps();
        }

        private void MenuAddAdmin_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            StartupManager.CreateElevatedStartupTask(System.IO.Path.GetFileNameWithoutExtension(p), p, null);
            LoadStartupApps();
        }

        private void MenuAddDelayed_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            StartupManager.CreateDelayedStartupTask(System.IO.Path.GetFileNameWithoutExtension(p), p, null);
            LoadStartupApps();
        }

        private void MenuAddAdminDelayed_Click(object sender, RoutedEventArgs e)
        {
            string? p = PickFile(); if (p == null) return;
            StartupManager.CreateElevatedDelayedStartupTask(System.IO.Path.GetFileNameWithoutExtension(p), p, null);
            LoadStartupApps();
        }

        private async void MenuMoveToTurbo_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp && Application.Current.MainWindow is MainWindow mw)
            {
                if (selectedApp.Location.Contains("Turbo Boot"))
                {
                    mw.ShowInfo("TURBO BOOT", "Este aplicativo já está no KitLugia Turbo Boot.");
                    return;
                }

                if (!await mw.ShowConfirmationDialog($"Mover '{selectedApp.Name}' para o Turbo Boot (KitLugia)?\n\nIsso utilizará uma inicialização paralela de alta prioridade.")) return;

                var resultAdd = await Task.Run(() => StartupManager.DelegateToKitLugia(selectedApp.Name));
                if (resultAdd.Success) { mw.ShowSuccess("BEM VINDO AO TURBO BOOT", resultAdd.Message); LoadStartupApps(); }
                else mw.ShowError("ERRO", resultAdd.Message);
            }
        }

        private async void MenuConvertToAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp && Application.Current.MainWindow is MainWindow mw)
            {
                if (selectedApp.Status.ToString() == "Elevated")
                {
                    mw.ShowInfo("JÁ ELEVADO", "Este aplicativo já está rodando como Administrador.");
                    return;
                }
                
                StartupManager.ExtractCommandParts(selectedApp.FullCommand, out string? path, out string? args);
                if (string.IsNullOrEmpty(path)) { mw.ShowError("ERRO", "Caminho inválido ou não pode ser convertido."); return; }

                await Task.Run(() => StartupManager.RemoveStartupItem(selectedApp.Name));
                var result = await Task.Run(() => StartupManager.CreateElevatedStartupTask(selectedApp.Name, path, args));
                
                if (result.Success) { mw.ShowSuccess("ELEVADO COM SUCESSO", result.Message); LoadStartupApps(); }
                else mw.ShowError("ERRO", result.Message);
            }
        }

        private async void MenuConvertToAdminDelayed_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp && Application.Current.MainWindow is MainWindow mw)
            {
                StartupManager.ExtractCommandParts(selectedApp.FullCommand, out string? path, out string? args);
                if (string.IsNullOrEmpty(path)) { mw.ShowError("ERRO", "Caminho inválido ou não pode ser convertido."); return; }

                await Task.Run(() => StartupManager.RemoveStartupItem(selectedApp.Name));
                var result = await Task.Run(() => StartupManager.CreateElevatedDelayedStartupTask(selectedApp.Name, path, args));
                
                if (result.Success) { mw.ShowSuccess("ELEVADO (ATRASO) COM SUCESSO", result.Message); LoadStartupApps(); }
                else mw.ShowError("ERRO", result.Message);
            }
        }

        private async void MenuRestoreNormal_Click(object sender, RoutedEventArgs e)
        {
            if (GridStartup.SelectedItem is StartupAppDetails selectedApp && Application.Current.MainWindow is MainWindow mw)
            {
                if (selectedApp.Status == StartupStatus.Enabled)
                {
                    mw.ShowInfo("RESTAURAR", "Este aplicativo já está na inicialização padrão.");
                    return;
                }

                if (!await mw.ShowConfirmationDialog($"Restaurar '{selectedApp.Name}' para a inicialização padrão do Windows?")) return;

                var result = await Task.Run(() => StartupManager.RestoreToNormal(selectedApp.Name));
                if (result.Success)
                {
                    mw.ShowSuccess("SUCESSO", result.Message);
                    LoadStartupApps();
                }
                else mw.ShowError("ERRO", result.Message);
            }
        }
        #endregion

        // =========================================================
        // ABA 2: OTIMIZAÇÃO DE SERVIÇOS
        // =========================================================
        #region Services Logic

        private async Task LoadServices()
        {
            var services = await Task.Run(() => BackgroundProcessManager.GetAllServices());
            _allServices = services;
            ApplyServiceFilter();
        }

        private void ApplyServiceFilter()
        {
            string filter = TxtSearchService.Text.Trim().ToLower();
            bool showDangerous = ChkShowDangerous.IsChecked == true;

            var filtered = _allServices.Where(s =>
            {
                bool matchesText = string.IsNullOrEmpty(filter) || s.DisplayName.ToLower().Contains(filter) || s.Name.ToLower().Contains(filter);
                bool matchesSafety = showDangerous || s.Safety != ServiceSafetyLevel.Dangerous;
                return matchesText && matchesSafety;
            }).ToList();

            GridServices.ItemsSource = filtered;
            if (TxtServiceCount != null) TxtServiceCount.Text = $"{filtered.Count} Serviços";
        }

        private void TxtSearchService_TextChanged(object sender, TextChangedEventArgs e) => ApplyServiceFilter();
        private void ChkShowDangerous_Click(object sender, RoutedEventArgs e) => ApplyServiceFilter();
        private async void BtnRefreshServices_Click(object sender, RoutedEventArgs e) => await LoadServices();

        private async Task RunServicePreset(string presetName, string friendlyName)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (!await mw.ShowConfirmationDialog($"Aplicar perfil '{friendlyName}'?")) return;
                mw.ShowInfo("AGUARDE", "Aplicando configurações...");
                var result = await Task.Run(() => BackgroundProcessManager.ApplyServicePreset(presetName));
                mw.ShowSuccess("SERVIÇOS", result.Message);
                LoadServices();
            }
        }

        private void BtnSafeOpt_Click(object sender, RoutedEventArgs e) => RunServicePreset("Safe", "Seguro");
        private void BtnGamerOpt_Click(object sender, RoutedEventArgs e) => RunServicePreset("Gamer", "Gamer");
        private void BtnRestoreServices_Click(object sender, RoutedEventArgs e) => RunServicePreset("Restore", "Padrão");

        // Menu de Contexto
        private async Task ChangeServiceState(string mode)
        {
            if (GridServices.SelectedItem is ServiceInfo svc && Application.Current.MainWindow is MainWindow mw)
            {
                if (svc.Safety == ServiceSafetyLevel.Dangerous && mode == "disabled")
                {
                    if (!await mw.ShowConfirmationDialog($"PERIGO: '{svc.DisplayName}' é crítico. Desativar?")) return;
                }

                mw.ShowInfo("AGUARDE", $"Configurando '{svc.DisplayName}'...");

                var result = mode == "default"
                    ? await Task.Run(() => BackgroundProcessManager.ResetServiceToDefault(svc.Name))
                    : await Task.Run(() => BackgroundProcessManager.ToggleServiceState(svc.Name, mode));

                if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                else mw.ShowError("ERRO", result.Message);

                LoadServices();
            }
        }

        private void MenuSvcAuto_Click(object sender, RoutedEventArgs e) => ChangeServiceState("auto");
        private void MenuSvcManual_Click(object sender, RoutedEventArgs e) => ChangeServiceState("demand");
        private void MenuSvcDisabled_Click(object sender, RoutedEventArgs e) => ChangeServiceState("disabled");
        private void MenuSvcDefault_Click(object sender, RoutedEventArgs e) => ChangeServiceState("default");
        #endregion

        // =========================================================
        // ABA 3: TAREFAS AGENDADAS (NOVO)
        // =========================================================
        #region Scheduled Tasks Logic

        private async Task LoadScheduledTasks()
        {
            var tasks = await Task.Run(() => BackgroundProcessManager.GetScheduledTasksStatus());
            GridTasks.ItemsSource = tasks;
        }

        private async void BtnToggleTask_Click(object sender, RoutedEventArgs e)
        {
            if (GridTasks.SelectedItem is ScheduledTaskInfo task && Application.Current.MainWindow is MainWindow mw)
            {
                bool newState = !task.IsEnabled;
                var result = await Task.Run(() => BackgroundProcessManager.ToggleTaskState(task.Path, newState));

                if (result.Success)
                {
                    mw.ShowSuccess("TAREFA", result.Message);
                    LoadScheduledTasks();
                }
                else mw.ShowError("ERRO", result.Message);
            }
        }

        private async void BtnDisableAllTasks_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (!await mw.ShowConfirmationDialog("Isso desativará TODAS as tarefas de telemetria listadas.\nDeseja continuar?")) return;

                var result = await Task.Run(() => BackgroundProcessManager.DisableTelemetryTasks());
                mw.ShowSuccess("TELEMETRIA", result.Message);
                LoadScheduledTasks();
            }
        }
        #endregion

        // =========================================================
        // ABA 4: ANÁLISE DE BOOT (NOVO)
        // =========================================================
        #region Boot Analysis Logic

        private async void BtnAnalyzeBoot_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                TxtBootTime.Text = "Analisando...";
                mw.ShowInfo("AGUARDE", "Lendo logs de eventos do sistema...");

                try
                {
                    var result = await Task.Run(() => BootOptimizerManager.AnalyzeBootPerformance());

                    if (!string.IsNullOrEmpty(result.ServiceStatusMessage))
                    {
                        mw.ShowError("AVISO", result.ServiceStatusMessage);
                        TxtBootTime.Text = "N/A";
                        return;
                    }

                    if (result.TotalTimeEvent != null)
                    {
                        double seconds = result.TotalTimeEvent.TimeTaken / 1000.0;
                        TxtBootTime.Text = $"{seconds:F2} segundos";
                        TxtBootDate.Text = $"Data: {result.TotalTimeEvent.TimeOfEvent}";
                    }
                    else
                    {
                        TxtBootTime.Text = "Sem dados recentes";
                    }

                    // Junta as duas listas para exibir na tabela
                    var combinedList = new List<PerformanceEvent>();
                    combinedList.AddRange(result.SlowStartupItems);
                    combinedList.AddRange(result.HighImpactApps);

                    GridBootItems.ItemsSource = combinedList;

                    if (combinedList.Count == 0)
                        mw.ShowSuccess("ÓTIMO", "Nenhum atraso significativo (>1s) encontrado no último boot.");
                    else
                        mw.ShowInfo("ANÁLISE", $"Encontrados {combinedList.Count} itens que impactaram o boot.");

                }
                catch (Exception ex)
                {
                    mw.ShowError("ERRO", ex.Message);
                    TxtBootTime.Text = "Erro";
                }
            }
        }
        #endregion
    }
}
