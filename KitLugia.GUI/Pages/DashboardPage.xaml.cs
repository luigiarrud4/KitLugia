using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
// Resolve ambiguidade
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
namespace KitLugia.GUI.Pages
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            _ = LoadSystemInfo();
        }
       
        private async Task LoadSystemInfo()
        {
            try
            {
                TxtPCName.Text = System.Environment.MachineName;
                TxtSpecs.Text = "Lendo hardware...";

                double ram = SystemUtils.GetTotalSystemRamGB();

                using var dashManager = new DashboardManager();
                var snapshot = await dashManager.GetSystemSnapshotAsync();

                // Formata a string com os dados reais e rápidos
                TxtSpecs.Text = $"{ram:F0} GB de RAM • {snapshot.OsName} • {snapshot.CpuName} • {snapshot.GpuName}";
            }
            catch 
            {
                TxtSpecs.Text = "Falha ao ler hardware.";
            }
        }

        // --- NAVEGAÇÃO DOS CARDS ---
        private void RequestNavigation(string tag)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.NavigateToPage(tag);
            }
        }

        private void BtnGoToTweaks_Click(object sender, RoutedEventArgs e) => RequestNavigation("⚡");
        private void BtnGoToCleanup_Click(object sender, RoutedEventArgs e) => RequestNavigation("💿");
        private void BtnGoToNetwork_Click(object sender, RoutedEventArgs e) => RequestNavigation("🌐");
        private void BtnGoToPrivacy_Click(object sender, RoutedEventArgs e) => RequestNavigation("🛡️");
        private void BtnGoToOOShutUp_Click(object sender, RoutedEventArgs e) => RequestNavigation("🔒");
        private void BtnGoToOptimize_Click(object sender, RoutedEventArgs e) => RequestNavigation("⚙️");
        private void BtnGoToAdvanced_Click(object sender, RoutedEventArgs e) => RequestNavigation("🚀");
        private void BtnGoToActivation_Click(object sender, RoutedEventArgs e) => RequestNavigation("🔑");

        // --- AÇÕES DOS BOTÕES GRANDES (1-CLICK) ---

        // --- AÇÕES DOS BOTÕES EXTRAS (1-CLIQUE MODIFICADO) ---

        private void BtnOpenQuickMenu_Click(object sender, RoutedEventArgs e)
        {
            OverlayQuickMenu.Visibility = Visibility.Visible;
            PopulateGpuList();
            UpdateVramRecommendations();
            LoadTraySettingsToQuickMenu();
        }

        private void LoadTraySettingsToQuickMenu()
        {
            try
            {
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw?.TrayService != null)
                {
                    ChkTrayIcon.IsChecked = mw.TrayService.IsTrayEnabled;
                    ChkGameBoost.IsChecked = mw.TrayService.GamePriorityEnabled;
                }

                using (var ts = new Microsoft.Win32.TaskScheduler.TaskService())
                {
                    var task = ts.GetTask("KitLugia");
                    ChkStartWithWindows.IsChecked = task?.Enabled == true;
                }
            }
            catch { }
        }

        private void ChkGameBoost_Click(object sender, RoutedEventArgs e)
        {
            // Removido auto-start forçado para evitar impacto na performance
            // GameBoost agora funciona independente de auto-start
        }

        private void UpdateVramRecommendations()
        {
            try
            {
                double ram = SystemUtils.GetTotalSystemRamGB();
                int recommendedMb = SystemTweaks.GetRecommendedVramMb(ram);

                // Mapeamento: 
                // Item 0: Padrão
                // Item 1: 256
                // Item 2: 512
                // Item 3: 1024
                // Item 4: 2048
                // Item 5: 4096

                foreach (ComboBoxItem item in CmbVram.Items)
                {
                    string content = item.Content.ToString() ?? "";
                    // Limpa recomendações anteriores se houver
                    content = content.Replace(" (Recomendado)", "");

                    bool isRecommended = false;
                    if (content.Contains("256 MB") && recommendedMb == 256) isRecommended = true;
                    else if (content.Contains("512 MB") && recommendedMb == 512) isRecommended = true;
                    else if (content.Contains("1024 MB") && recommendedMb == 1024) isRecommended = true;
                    else if (content.Contains("2048 MB") && recommendedMb == 2048) isRecommended = true;

                    if (isRecommended)
                    {
                        item.Content = content + " (Recomendado)";
                        item.IsSelected = true; // Auto-seleciona o recomendado
                    }
                    else
                    {
                        item.Content = content;
                    }
                }
            }
            catch { }
        }

        private void BtnCancelQuickMenu_Click(object sender, RoutedEventArgs e)
        {
            OverlayQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void PopulateGpuList()
        {
            try
            {
                CmbGpu.Items.Clear();
                var gpus = SystemTweaks.GetAllGpus();
                
                foreach (var gpu in gpus)
                {
                    string name = gpu["Name"]?.ToString() ?? "GPU Desconhecida";
                    CmbGpu.Items.Add(name);
                }

                if (CmbGpu.Items.Count > 0) CmbGpu.SelectedIndex = 0;
            }
            catch { }
        }

        private async void BtnApplyCustomOptimization_Click(object sender, RoutedEventArgs e)
        {
            OverlayQuickMenu.Visibility = Visibility.Collapsed;

            var settings = new OptimizationSettings
            {
                ApplyRegistryTweaks = ChkSystemTweaks.IsChecked == true,
                ApplyPowerPlan = ChkPowerPlan.IsChecked == true,
                ApplyGamingOptimizations = true, // Sempre ativado no 1-clique
                ApplyVerboseBoot = true,
                ApplyVramTweak = true,
                UseExtremeProfile = ChkExtremeVisuals.IsChecked == true
            };

            if (ChkTurboShutdown.IsChecked == true)
            {
                SystemTweaks.ToggleFastShutdown();
            }

            ApplyTraySettingsFromQuickMenu();

            // Detectar RegPath da GPU selecionada
            int index = CmbGpu.SelectedIndex;
            var allGpus = SystemTweaks.GetAllGpus();
            if (index >= 0 && index < allGpus.Count)
            {
                settings.TargetGpuRegPath = SystemTweaks.FindGpuRegistryPath(allGpus[index]);
            }

            // Mapear VRAM
            int vramIndex = CmbVram.SelectedIndex;
            settings.VramSizeMb = vramIndex switch
            {
                1 => 256,
                2 => 512,
                3 => 1024,
                4 => 2048,
                5 => 4096,
                _ => 0 // Automatic
            };

            await RunOptimizationFlow(settings);
        }

        private async void BtnRevertNormal_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            bool confirm = await mw.ShowConfirmationDialog(
                "Isso removerá todas as otimizações de VRAM, planos de energia e modificações de registro do Kit Lugia.\n\n" +
                "Deseja restaurar o padrão do sistema?");

            if (!confirm) return;

            mw.ShowInfo("REVERTENDO", "Restaurando configurações padrão do sistema...");
            
            var progress = new Progress<string>(s => { });
            try
            {
                await OptimizationOrchestrator.RevertAllOptimizationsAsync(progress);
                mw.ShowSuccess("SUCESSO", "Sistema restaurado! Reinicie para concluir a reversão.");
            }
            catch (Exception ex)
            {
                mw.ShowError("ERRO", $"Falha ao reverter: {ex.Message}");
            }
        }

        private void ApplyTraySettingsFromQuickMenu()
        {
            try
            {
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw?.TrayService != null)
                {
                    mw.TrayService.SetTrayEnabled(ChkTrayIcon.IsChecked == true);
                    mw.TrayService.GamePriorityEnabled = ChkGameBoost.IsChecked == true;
                    mw.TrayService.SaveSettings();

                    KitLugia.GUI.Services.TrayIconService.SetAutoStart(ChkStartWithWindows.IsChecked == true);
                }
            }
            catch { }
        }

        // Lógica compartilhada de execução
        private async Task RunOptimizationFlow(OptimizationSettings settings)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            mw.ShowInfo("PROCESSANDO", "Aplicando otimizações selecionadas...");

            var progress = new Progress<string>(s => { });

            try
            {
                await OptimizationOrchestrator.RunOptimizationAsync(settings, progress);
                mw.ShowSuccess("SUCESSO", "Otimização concluída com sucesso! Reinicie o computador.");
            }
            catch (Exception ex)
            {
                mw.ShowError("ERRO", $"Falha na otimização: {ex.Message}");
            }
        }

        // Métodos antigos removidos para evitar duplicidade ou confusão
        [Obsolete("Use BtnOpenQuickMenu_Click")]
        private void BtnOptimizeStandard_Click(object sender, RoutedEventArgs e) => BtnOpenQuickMenu_Click(sender, e);

        [Obsolete("Use Selection Overlay instead")]
        private void BtnOptimizeExtreme_Click(object sender, RoutedEventArgs e) => BtnOpenQuickMenu_Click(sender, e);

        // --- GAMING LATENCY PROFILE EVENT HANDLERS ---

        private void BtnOpenLatencyMenu_Click(object sender, RoutedEventArgs e)
        {
            OverlayLatencyMenu.Visibility = Visibility.Visible;
            RefreshLatencyStatus();
        }

        private void BtnCancelLatencyMenu_Click(object sender, RoutedEventArgs e)
        {
            OverlayLatencyMenu.Visibility = Visibility.Collapsed;
        }

        private void BtnRevertLatency_Click(object sender, RoutedEventArgs e)
        {
            var result = SystemTweaks.RevertGamingLatencyProfile();
            MessageBox.Show(result.Message, result.Success ? "Sucesso" : "Erro", 
                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        private void BtnRevertLatencyMenu_Click(object sender, RoutedEventArgs e)
        {
            var result = SystemTweaks.RevertGamingLatencyProfile();
            if (result.Success)
            {
                MessageBox.Show(result.Message, "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshLatencyStatus();
            }
            else
            {
                MessageBox.Show(result.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnApplyAllLatency_Click(object sender, RoutedEventArgs e)
        {
            int win32Value = 0x26;
            switch (CmbWin32Priority.SelectedIndex)
            {
                case 0: win32Value = 0x18; break;
                case 1: win32Value = 0x26; break;
                case 2: win32Value = 0x2A; break;
            }

            var result = SystemTweaks.ApplyFullGamingLatencyProfile(win32Value);
            
            if (result.Success)
            {
                string tweaksAplicados = string.Join("\n• ", result.Applied);
                MessageBox.Show($"{result.Message}\n\nTweaks aplicados:\n• {tweaksAplicados}", 
                    "Gaming Latency Profile", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshLatencyStatus();
            }
            else
            {
                MessageBox.Show(result.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- TOGGLE METHODS COM CORES VISUAIS ---

        private void BtnToggleCoreParking_Click(object sender, RoutedEventArgs e)
        {
            var status = SystemTweaks.CheckGamingLatencyStatus();
            bool isCurrentlyOn = status["CoreParking"];
            
            if (!isCurrentlyOn)
            {
                var result = SystemTweaks.DisableCoreParking();
                if (result.Success) UpdateToggleVisual(BtnToggleCoreParking, IndicatorCoreParking, BorderCoreParking, true);
                MessageBox.Show(result.Message, "Core Parking", MessageBoxButton.OK, 
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            else
            {
                // Revert individual - restaura valor padrão (64)
                try
                {
                    Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c8-3b32988b1dd4\0cc5b647-c1df-4637-891a-dec35c318583", "ValueMax", 64, Microsoft.Win32.RegistryValueKind.DWord);
                    UpdateToggleVisual(BtnToggleCoreParking, IndicatorCoreParking, BorderCoreParking, false);
                    MessageBox.Show("Core Parking restaurado para padrão.", "Core Parking", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao restaurar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshLatencyStatus();
        }

        private void BtnToggleTimerCoalescing_Click(object sender, RoutedEventArgs e)
        {
            var status = SystemTweaks.CheckGamingLatencyStatus();
            bool isCurrentlyOn = status["TimerCoalescing"];
            
            if (!isCurrentlyOn)
            {
                var result = SystemTweaks.DisableTimerCoalescing();
                if (result.Success) UpdateToggleVisual(BtnToggleTimerCoalescing, IndicatorTimerCoalescing, BorderTimerCoalescing, true);
                MessageBox.Show(result.Message, "Timer Coalescing", MessageBoxButton.OK, 
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            else
            {
                // Revert individual - deleta a chave para voltar ao padrão
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", true);
                    if (key != null)
                    {
                        key.DeleteValue("CoalescingTimerInterval", false);
                    }
                    UpdateToggleVisual(BtnToggleTimerCoalescing, IndicatorTimerCoalescing, BorderTimerCoalescing, false);
                    MessageBox.Show("Timer Coalescing restaurado para padrão.", "Timer Coalescing", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao restaurar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshLatencyStatus();
        }

        private void BtnToggleInputQueue_Click(object sender, RoutedEventArgs e)
        {
            var status = SystemTweaks.CheckGamingLatencyStatus();
            bool isCurrentlyOn = status["InputQueue"];
            
            if (!isCurrentlyOn)
            {
                var result = SystemTweaks.OptimizeInputQueue();
                if (result.Success) UpdateToggleVisual(BtnToggleInputQueue, IndicatorInputQueue, BorderInputQueue, true);
                MessageBox.Show(result.Message, "Input Queue", MessageBoxButton.OK, 
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            else
            {
                // Revert individual - restaura para 100 (padrão)
                try
                {
                    Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", "KeyboardDataQueueSize", 100, Microsoft.Win32.RegistryValueKind.DWord);
                    Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", 100, Microsoft.Win32.RegistryValueKind.DWord);
                    UpdateToggleVisual(BtnToggleInputQueue, IndicatorInputQueue, BorderInputQueue, false);
                    MessageBox.Show("Input Queue restaurado para padrão (100).", "Input Queue", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao restaurar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshLatencyStatus();
        }

        private void BtnToggleGlobalTimer_Click(object sender, RoutedEventArgs e)
        {
            var status = SystemTweaks.CheckGamingLatencyStatus();
            bool isCurrentlyOn = status["GlobalTimerResolution"];
            
            if (!isCurrentlyOn)
            {
                var result = SystemTweaks.EnableGlobalTimerResolution();
                if (result.Success) UpdateToggleVisual(BtnToggleGlobalTimer, IndicatorGlobalTimer, BorderGlobalTimer, true);
                MessageBox.Show(result.Message, "Global Timer Resolution", MessageBoxButton.OK, 
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            else
            {
                // Revert individual
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true);
                    key?.DeleteValue("GlobalTimerResolutionRequests", false);
                    UpdateToggleVisual(BtnToggleGlobalTimer, IndicatorGlobalTimer, BorderGlobalTimer, false);
                    MessageBox.Show("Global Timer Resolution restaurado para padrão.", "Global Timer", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao restaurar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshLatencyStatus();
        }

        private void BtnToggleSysResp_Click(object sender, RoutedEventArgs e)
        {
            var status = SystemTweaks.CheckGamingLatencyStatus();
            bool isCurrentlyOn = status["SystemResponsiveness"];
            
            if (!isCurrentlyOn)
            {
                var result = SystemTweaks.SetSystemResponsivenessGaming();
                if (result.Success) UpdateToggleVisual(BtnToggleSysResp, IndicatorSysResp, BorderSysResp, true);
                MessageBox.Show(result.Message, "System Responsiveness", MessageBoxButton.OK, 
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            else
            {
                // Revert individual - restaura para 20 (padrão)
                try
                {
                    Microsoft.Win32.Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 20, Microsoft.Win32.RegistryValueKind.DWord);
                    UpdateToggleVisual(BtnToggleSysResp, IndicatorSysResp, BorderSysResp, false);
                    MessageBox.Show("System Responsiveness restaurado para padrão (20).", "System Responsiveness", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Erro ao restaurar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            RefreshLatencyStatus();
        }

        private void UpdateToggleVisual(Button btn, System.Windows.Shapes.Ellipse indicator, Border container, bool isOn)
        {
            if (isOn)
            {
                btn.Content = "ON";
                btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // #4CAF50
                btn.Foreground = System.Windows.Media.Brushes.White;
                btn.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                indicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Verde
                container.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                container.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 55, 37)); // Verde escuro
            }
            else
            {
                btn.Content = "OFF";
                btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)); // #333
                btn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(153, 153, 153)); // #999
                btn.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)); // #555
                indicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)); // #666
                container.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)); // #333
                container.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 37, 37)); // #252525
            }
        }

        #region Latency Analyzer Event Handlers

        private CancellationTokenSource? _latencyScanCts;

        private async void BtnScanLatency_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mostra loading overlay
                LoadingOverlayBenchmark.Visibility = Visibility.Visible;
                ProgressBarBenchmark.Value = 0;
                TxtBenchmarkStatus.Text = "Iniciando benchmark...";
                TxtBenchmarkLog.Text = "";
                BtnCancelBenchmark.IsEnabled = true;

                _latencyScanCts = new CancellationTokenSource();
                
                // Progress reporter para atualizar a UI
                var progress = new Progress<string>(msg =>
                {
                    TxtBenchmarkStatus.Text = msg;
                    TxtBenchmarkLog.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}\n";
                    ScrollViewerBenchmarkLog.ScrollToBottom();
                    
                    // Atualiza progresso baseado na mensagem
                    if (msg.Contains("PADRÃO")) ProgressBarBenchmark.Value = 25;
                    else if (msg.Contains("CONSERVADOR")) ProgressBarBenchmark.Value = 50;
                    else if (msg.Contains("EQUILIBRADO")) ProgressBarBenchmark.Value = 75;
                    else if (msg.Contains("AGRESSIVO")) ProgressBarBenchmark.Value = 90;
                    else if (msg.Contains("Aplicando")) ProgressBarBenchmark.Value = 100;
                });

                // Executa benchmark inteligente completo
                var benchmark = await LatencyAnalyzer.RunIntelligentBenchmarkAsync(progress, _latencyScanCts.Token);

                // Esconde loading
                LoadingOverlayBenchmark.Visibility = Visibility.Collapsed;

                if (benchmark.Success)
                {
                    // Mostra resultados do melhor perfil
                    TxtCurrentLatency.Text = benchmark.Best.Measurement.CurrentLatencyUs.ToString("F1");
                    TxtAvgLatency.Text = benchmark.Best.Measurement.AvgLatencyUs.ToString("F1");
                    TxtMaxLatency.Text = benchmark.Best.Measurement.MaxLatencyUs.ToString("F1");
                    
                    // Mostra relatório completo
                    TxtLatencyRecommendation.Text = $"🏆 Melhor: {benchmark.Best.Profile.Name}\n" +
                        $"Latência: {benchmark.Best.Measurement.AvgLatencyUs:F1}µs | Score: {benchmark.Best.Score:F0}\n" +
                        $"Estável: {(benchmark.Best.IsStable ? "Sim" : "Não")}\n\n" +
                        benchmark.Report;
                    
                    PanelLatencyResults.Visibility = Visibility.Visible;
                    
                    // Atualiza toggles para refletir o perfil aplicado
                    RefreshLatencyStatus();
                    
                    Logger.Log($"Benchmark completo. Melhor perfil: {benchmark.Best.Profile.Name}");
                    
                    // Mostra mensagem de sucesso
                    MessageBox.Show($"Benchmark concluído!\n\nMelhor perfil: {benchmark.Best.Profile.Name}\n" +
                        $"Latência: {benchmark.Best.Measurement.AvgLatencyUs:F1}µs\n" +
                        $"As configurações ótimas já foram aplicadas automaticamente.", 
                        "Benchmark Concluído", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TxtLatencyRecommendation.Text = $"Erro no benchmark: {benchmark.Report}";
                    PanelLatencyResults.Visibility = Visibility.Visible;
                    MessageBox.Show($"Erro no benchmark: {benchmark.Report}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (OperationCanceledException)
            {
                LoadingOverlayBenchmark.Visibility = Visibility.Collapsed;
                TxtLatencyRecommendation.Text = "Benchmark cancelado pelo usuário.";
                PanelLatencyResults.Visibility = Visibility.Visible;
                Logger.Log("Benchmark cancelado pelo usuário");
            }
            catch (Exception ex)
            {
                LoadingOverlayBenchmark.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Erro no benchmark: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnScanLatency.IsEnabled = true;
                BtnScanLatency.Content = "ANALISAR";
            }
        }

        private void BtnCancelBenchmark_Click(object sender, RoutedEventArgs e)
        {
            _latencyScanCts?.Cancel();
            BtnCancelBenchmark.IsEnabled = false;
            TxtBenchmarkStatus.Text = "Cancelando...";
            Logger.Log("Solicitação de cancelamento do benchmark");
        }

        private Dictionary<string, string> _currentRecommendations = new();

        private async void BtnApplyRecommended_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnApplyRecommended.IsEnabled = false;
                BtnApplyRecommended.Content = "APLICANDO...";

                var result = await LatencyAnalyzer.AutoOptimizeAsync();
                
                if (result.Success)
                {
                    MessageBox.Show(result.Message, "Otimização Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshLatencyStatus();
                    
                    // Atualiza os valores na UI
                    TxtCurrentLatency.Text = result.After.CurrentLatencyUs.ToString("F1");
                    TxtAvgLatency.Text = result.After.AvgLatencyUs.ToString("F1");
                    TxtMaxLatency.Text = result.After.MaxLatencyUs.ToString("F1");
                }
                else
                {
                    MessageBox.Show(result.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao aplicar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnApplyRecommended.IsEnabled = true;
                BtnApplyRecommended.Content = "APLICAR RECOMENDAÇÕES";
            }
        }

        private async Task AnimateProgressBarAsync(CancellationToken cancellationToken)
        {
            try
            {
                double progress = 0;
                while (progress < 100 && !cancellationToken.IsCancellationRequested)
                {
                    progress += 2;
                    ProgressLatencyScan.Value = progress;
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal quando cancela
            }
        }

        #endregion

        private void RefreshLatencyStatus()
        {
            try
            {
                var status = SystemTweaks.CheckGamingLatencyStatus();
                var sb = new System.Text.StringBuilder();
                
                // Atualiza visuais dos toggles
                UpdateToggleVisual(BtnToggleCoreParking, IndicatorCoreParking, BorderCoreParking, status["CoreParking"]);
                UpdateToggleVisual(BtnToggleTimerCoalescing, IndicatorTimerCoalescing, BorderTimerCoalescing, status["TimerCoalescing"]);
                UpdateToggleVisual(BtnToggleInputQueue, IndicatorInputQueue, BorderInputQueue, status["InputQueue"]);
                UpdateToggleVisual(BtnToggleGlobalTimer, IndicatorGlobalTimer, BorderGlobalTimer, status["GlobalTimerResolution"]);
                UpdateToggleVisual(BtnToggleSysResp, IndicatorSysResp, BorderSysResp, status["SystemResponsiveness"]);
                
                foreach (var item in status)
                {
                    string statusText = item.Value ? "✓ Ativado" : "✗ Padrão";
                    sb.AppendLine($"{item.Key}: {statusText}");
                }
                
                TxtLatencyStatus.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                TxtLatencyStatus.Text = "Erro ao verificar status: " + ex.Message;
            }
        }
    }
}
