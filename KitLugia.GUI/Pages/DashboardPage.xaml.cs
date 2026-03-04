using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
// Resolve ambiguidade
using Application = System.Windows.Application;
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
    }
}
