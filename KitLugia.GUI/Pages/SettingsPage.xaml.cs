using System;
using System.IO;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;

namespace KitLugia.GUI.Pages
{
    public partial class SettingsPage : Page
    {
        // Arquivo de configuração
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "KitLugia", 
            "settings.json");
        
        private const string AutoStartRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartValueName = "KitLugia";

        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();

            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += SettingsPage_Unloaded;

            // Adicionar eventos para os novos toggles
            AddToggleEvents();
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            SaveSettings();
            this.Unloaded -= SettingsPage_Unloaded;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }
        
        private void AddToggleEvents()
        {
            // Adicionar eventos para salvar automaticamente ao mudar
            ToggleStartup.Click += (s, e) => ToggleStartup_Click(s, e);
            ToggleCloseToTray.Click += (s, e) => ToggleCloseToTray_Click(s, e);
            ToggleTray.Click += (s, e) => ToggleTray_Click();
            ToggleNotifications.Click += (s, e) => SaveSettings();
            ToggleVerboseLogging.Click += (s, e) => SaveSettings();
            
            ToggleRAMMonitor.Click += (s, e) => SaveSettings();
            ToggleGameBoost.Click += (s, e) => ToggleGameBoost_Click();
            ToggleTurboBoot.Click += (s, e) => ToggleTurboBoot_Click();
            ToggleTurboShutdown.Click += (s, e) => ToggleTurboShutdown_Click();
            ToggleStandbyClean.Click += (s, e) => ToggleStandbyClean_Click();
        }

        private void LoadSettings()
        {
            try
            {
                // 🔥 VERIFICAR AUTO-START REAL DO SISTEMA - usa novo método que verifica o caminho
                ToggleStartup.IsChecked = KitLugia.GUI.Services.TrayIconService.IsAutoStartEnabled();
                
                // 🔥 CARREGAR ESTADO DOS MÓDULOS DO TRAYSERVICE
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
                {
                    // Close to Tray
                    if (mainWindow.TrayService != null)
                    {
                        ToggleCloseToTray.IsChecked = mainWindow.TrayService.CloseToTray;
                    }
                    else
                    {
                        // Fallback se não conseguir acessar
                        ToggleCloseToTray.IsChecked = true;
                    }
                    
                    if (mainWindow.TrayService != null)
                    {
                        var tray = mainWindow.TrayService;
                        
                        // Carregar todos os módulos do TrayService
                        ToggleGameBoost.IsChecked = tray.GamePriorityEnabled;
                        ToggleTray.IsChecked = tray.IsTrayEnabled;
                        ToggleTurboBoot.IsChecked = tray.TurboBootEnabled;
                        ToggleTurboShutdown.IsChecked = tray.TurboShutdownEnabled;
                        ToggleStandbyClean.IsChecked = tray.StandbyCleanEnabled;
                    }
                }
                
                // Carregar configurações salvas para os demais (não controlados pelo TrayService)
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (settings != null)
                    {
                        // Não sobrescrever módulos do TrayService (já carregados acima)
                        ToggleNotifications.IsChecked = settings.ShowNotifications;
                        ToggleVerboseLogging.IsChecked = settings.VerboseLogging;
                        ToggleRAMMonitor.IsChecked = settings.RAMMonitorEnabled;
                        ToggleDeveloperMode.IsChecked = settings.DeveloperMode;
                    }
                }
                else
                {
                    // Configurações padrão
                    ToggleNotifications.IsChecked = true;
                    ToggleVerboseLogging.IsChecked = false;
                    ToggleRAMMonitor.IsChecked = true;
                    ToggleDeveloperMode.IsChecked = false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsPage.LoadSettings", $"Erro ao carregar: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    // Não salvar módulos do TrayService (gerenciados pelo próprio TrayService)
                    StartWithWindows = ToggleStartup.IsChecked ?? false,
                    MinimizeToTray = ToggleTray.IsChecked ?? true,
                    ShowNotifications = ToggleNotifications.IsChecked ?? true,
                    VerboseLogging = ToggleVerboseLogging.IsChecked ?? false,
                    
                    RAMMonitorEnabled = ToggleRAMMonitor.IsChecked ?? true,
                    DeveloperMode = ToggleDeveloperMode.IsChecked ?? false
                };

                // Garantir que o diretório existe
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                
                string json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigPath, json);

                // Aplicar configurações em tempo real
                ApplySettings(settings);

                // Logger.Log("⚙️ Configurações salvas com sucesso");
            }
            catch (Exception ex)
            {
                Logger.LogError("SettingsPage.SaveSettings", $"Erro ao salvar: {ex.Message}");
            }
        }

        private void ApplySettings(AppSettings settings)
        {
            // Aplicar modo desenvolvedor
            DeveloperModeManager.IsDeveloperMode = settings.DeveloperMode;
            
            // TODO: Aplicar configuração de log detalhado quando implementado no Logger
            
            // Notificar a MainWindow para atualizar visibilidade do menu de debug
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.UpdateDebugMenuVisibility(settings.DeveloperMode);
            }
        }

        #region Event Handlers

        private void ToggleStartup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔥 Usar TrayIconService.SetAutoStart como nas outras páginas
                KitLugia.GUI.Services.TrayIconService.SetAutoStart(ToggleStartup.IsChecked == true);
                
                // Atualizar estado real
                ToggleStartup.IsChecked = KitLugia.GUI.Services.TrayIconService.IsAutoStartEnabled();
                
                Logger.Log($"⚙️ Auto-Start: {(ToggleStartup.IsChecked == true ? "ativado" : "desativado")}");
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleStartup_Click", $"Erro: {ex.Message}");
            }
        }
        
        private void ToggleCloseToTray_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.CloseToTray = ToggleCloseToTray.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    Logger.Log($"⚙️ Close to Tray: {(ToggleCloseToTray.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleCloseToTray_Click", $"Erro: {ex.Message}");
            }
        }
        
        private void ToggleTray_Click()
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.IsTrayEnabled = ToggleTray.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    mainWindow.TrayService.LoadSettings(); // Recarregar para aplicar
                    Logger.Log($"⚙️ Tray Icon: {(ToggleTray.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleTray_Click", $"Erro: {ex.Message}");
            }
        }
        
        private void ToggleGameBoost_Click()
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.GamePriorityEnabled = ToggleGameBoost.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    mainWindow.TrayService.LoadSettings(); // Recarregar para aplicar
                    Logger.Log($"⚙️ GameBoost: {(ToggleGameBoost.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleGameBoost_Click", $"Erro: {ex.Message}");
            }
        }
        
        private void ToggleTurboBoot_Click()
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.TurboBootEnabled = ToggleTurboBoot.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    mainWindow.TrayService.LoadSettings(); // Recarregar para aplicar
                    Logger.Log($"⚙️ Turbo Boot: {(ToggleTurboBoot.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleTurboBoot_Click", $"Erro: {ex.Message}");
            }
        }
        
        private void ToggleTurboShutdown_Click()
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.TurboShutdownEnabled = ToggleTurboShutdown.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    mainWindow.TrayService.LoadSettings(); // Recarregar para aplicar
                    Logger.Log($"⚙️ Turbo Shutdown: {(ToggleTurboShutdown.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleTurboShutdown_Click", $"Erro: {ex.Message}");
            }
        }
        
        private void ToggleStandbyClean_Click()
        {
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.StandbyCleanEnabled = ToggleStandbyClean.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    mainWindow.TrayService.LoadSettings(); // Recarregar para aplicar
                    Logger.Log($"⚙️ Standby Clean: {(ToggleStandbyClean.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ToggleStandbyClean_Click", $"Erro: {ex.Message}");
            }
        }

        private void ToggleDeveloperMode_Checked(object sender, RoutedEventArgs e)
        {
            Logger.Log("🐛 Modo Desenvolvedor ATIVADO");
            SaveSettings();
            
            // Mostrar mensagem
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowSuccess("🐛 Modo Desenvolvedor", "Menu de debug agora visível. Reinicie para aplicar todas as mudanças.");
            }
        }

        private void ToggleDeveloperMode_Unchecked(object sender, RoutedEventArgs e)
        {
            Logger.Log("🔒 Modo Desenvolvedor DESATIVADO");
            SaveSettings();
            
            if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowInfo("🔒 Modo Normal", "Menu de debug oculto.");
            }
        }

        #endregion
    }

    #region Classes de Suporte

    public class AppSettings
    {
        // Inicialização
        public bool StartWithWindows { get; set; }
        public bool MinimizeToTray { get; set; } = true;
        
        // Sistema
        public bool ShowNotifications { get; set; } = true;
        public bool VerboseLogging { get; set; }
        
        // Módulos do Kit (apenas RAMMonitor, os demais são gerenciados pelo TrayService)
        public bool RAMMonitorEnabled { get; set; } = true;
        
        // Desenvolvedor
        public bool DeveloperMode { get; set; }
    }

    /// <summary>
    /// Gerenciador do Modo Desenvolvedor
    /// </summary>
    public static class DeveloperModeManager
    {
        public static bool IsDeveloperMode { get; set; }
        
        public static event EventHandler? DeveloperModeChanged;
        
        public static void Toggle()
        {
            IsDeveloperMode = !IsDeveloperMode;
            DeveloperModeChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    #endregion
}
