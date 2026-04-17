using System;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class AdvancedRamCleanSettingsPage : Page
    {
        public AdvancedRamCleanSettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            CheckWindowsVersion();
            
            // Event handlers para sliders
            SliderAutoReductLimit.ValueChanged += (s, e) => 
            {
                TxtAutoReductLimit.Text = $"{(int)e.NewValue}%";
            };
            SliderAutoReductInterval.ValueChanged += (s, e) => 
            {
                TxtAutoReductInterval.Text = $"{(int)e.NewValue}s";
            };
            SliderWarningLevel.ValueChanged += (s, e) => 
            {
                TxtWarningLevel.Text = $"{(int)e.NewValue}%";
            };
            SliderDangerLevel.ValueChanged += (s, e) => 
            {
                TxtDangerLevel.Text = $"{(int)e.NewValue}%";
            };
        }
        
        private void CheckWindowsVersion()
        {
            var windowsVersion = SystemInfo.GetWindowsVersion();
            var versionString = SystemInfo.GetWindowsVersionString();
            
            Logger.Log($"🖥️ Sistema detectado: {versionString}");
            
            // Desabilitar opções não suportadas
            if (!SystemInfo.IsFeatureSupported("RegistryCache"))
            {
                ChkRegistryCache.IsChecked = false;
                ChkRegistryCache.IsEnabled = false;
                Logger.Log("⚠️ Registry Cache desabilitado - não suportado nesta versão do Windows");
            }
            
            if (!SystemInfo.IsFeatureSupported("CombineMemoryLists"))
            {
                ChkCombineLists.IsChecked = false;
                ChkCombineLists.IsEnabled = false;
                Logger.Log("⚠️ Combine Memory Lists desabilitado - não suportado nesta versão do Windows");
            }
        }
        
        private void LoadSettings()
        {
            // Carregar configurações do Registry
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\KitLugia\AdvancedRamClean");
                if (key != null)
                {
                    ChkWorkingSet.IsChecked = (int)key.GetValue("WorkingSet", 1) == 1;
                    ChkSystemFileCache.IsChecked = (int)key.GetValue("SystemFileCache", 1) == 1;
                    ChkStandbyPriority0.IsChecked = (int)key.GetValue("StandbyPriority0", 1) == 1;
                    ChkStandbyList.IsChecked = (int)key.GetValue("StandbyList", 0) == 1;
                    ChkModifiedList.IsChecked = (int)key.GetValue("ModifiedList", 0) == 1;
                    ChkCombineLists.IsChecked = (int)key.GetValue("CombineLists", 1) == 1;
                    ChkRegistryCache.IsChecked = (int)key.GetValue("RegistryCache", 1) == 1;
                    ChkModifiedFileCache.IsChecked = (int)key.GetValue("ModifiedFileCache", 1) == 1;
                    
                    SliderAutoReductLimit.Value = (int)key.GetValue("AutoReductLimit", 90);
                    SliderAutoReductInterval.Value = (int)key.GetValue("AutoReductInterval", 30);
                    SliderWarningLevel.Value = (int)key.GetValue("WarningLevel", 70);
                    SliderDangerLevel.Value = (int)key.GetValue("DangerLevel", 90);
                    
                    TxtAutoReductLimit.Text = $"{(int)SliderAutoReductLimit.Value}%";
                    TxtAutoReductInterval.Text = $"{(int)SliderAutoReductInterval.Value}s";
                    TxtWarningLevel.Text = $"{(int)SliderWarningLevel.Value}%";
                    TxtDangerLevel.Text = $"{(int)SliderDangerLevel.Value}%";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("AdvancedRamCleanSettingsPage.LoadSettings", $"Erro: {ex.Message}");
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\KitLugia\AdvancedRamClean");
                key.SetValue("WorkingSet", ChkWorkingSet.IsChecked == true ? 1 : 0);
                key.SetValue("SystemFileCache", ChkSystemFileCache.IsChecked == true ? 1 : 0);
                key.SetValue("StandbyPriority0", ChkStandbyPriority0.IsChecked == true ? 1 : 0);
                key.SetValue("StandbyList", ChkStandbyList.IsChecked == true ? 1 : 0);
                key.SetValue("ModifiedList", ChkModifiedList.IsChecked == true ? 1 : 0);
                key.SetValue("CombineLists", ChkCombineLists.IsChecked == true ? 1 : 0);
                key.SetValue("RegistryCache", ChkRegistryCache.IsChecked == true ? 1 : 0);
                key.SetValue("ModifiedFileCache", ChkModifiedFileCache.IsChecked == true ? 1 : 0);
                
                key.SetValue("AutoReductLimit", (int)SliderAutoReductLimit.Value);
                key.SetValue("AutoReductInterval", (int)SliderAutoReductInterval.Value);
                key.SetValue("WarningLevel", (int)SliderWarningLevel.Value);
                key.SetValue("DangerLevel", (int)SliderDangerLevel.Value);
                
                Logger.Log("✅ AdvancedRamCleanSettings: Configurações salvas");
            }
            catch (Exception ex)
            {
                Logger.LogError("AdvancedRamCleanSettingsPage.SaveSettings", $"Erro: {ex.Message}");
            }
        }
        
        private void BtnRestoreDefault_Click(object sender, RoutedEventArgs e)
        {
            // Restaurar configurações padrão do MemReduct
            ChkWorkingSet.IsChecked = true;
            ChkSystemFileCache.IsChecked = true;
            ChkStandbyPriority0.IsChecked = true;
            ChkStandbyList.IsChecked = false;
            ChkModifiedList.IsChecked = false;
            ChkCombineLists.IsChecked = true;
            ChkRegistryCache.IsChecked = true;
            ChkModifiedFileCache.IsChecked = true;
            
            SliderAutoReductLimit.Value = 90;
            SliderAutoReductInterval.Value = 30;
            SliderWarningLevel.Value = 70;
            SliderDangerLevel.Value = 90;
            
            Logger.Log("🔄 AdvancedRamCleanSettings: Restaurado para padrão MemReduct");
        }
        
        private async void BtnAggressiveMode_Click(object sender, RoutedEventArgs e)
        {
            // Modo agressivo - limpa tudo incluindo Modified List
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = await mw.ShowConfirmationDialog("Este modo habilita a Modified Page List, que pode causar travamentos. Deseja continuar?");
                if (!result) return;
            }
            
            ChkWorkingSet.IsChecked = true;
            ChkSystemFileCache.IsChecked = true;
            ChkStandbyPriority0.IsChecked = true;
            ChkStandbyList.IsChecked = true;
            ChkModifiedList.IsChecked = true; // ⚠️ Perigoso
            ChkCombineLists.IsChecked = true;
            ChkRegistryCache.IsChecked = true;
            ChkModifiedFileCache.IsChecked = true;
            
            Logger.Log("⚠️ AdvancedRamCleanSettings: Modo agressivo ativado");
        }
        
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.ShowSuccess("CONFIGURAÇÕES", "Configurações avançadas de limpeza de RAM salvas com sucesso!");
                // Voltar para a página anterior
                if (mw.MainFrame.CanGoBack)
                    mw.MainFrame.GoBack();
            }
        }
        
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Fechar modal e voltar para página anterior
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (mw.MainFrame.CanGoBack)
                    mw.MainFrame.GoBack();
            }
        }
    }
}
