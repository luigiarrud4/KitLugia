using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KitLugia.GUI.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Colors = System.Windows.Media.Colors;
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class GameBoostPage : Page
    {
        private bool _isInitializing = false;
        private bool _isCleaningUp = false;
        private bool _isLoadingSettings = false;
        private bool _isRestoring = false; // 🔥 NOVO: Flag para evitar SelectionChanged durante restauração
        private List<CustomMotorProfile> _customProfiles = new();
        private CustomMotorProfile? _profileToDelete = null;
        private string? _editingProfileId = null; // null = novo perfil, valor = editando perfil existente
        private CancellationTokenSource? _cts;
        private readonly string _profilesFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "KitLugia", 
            "custom_profiles.json");
        private readonly string _engineConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "KitLugia", 
            "last_engine.json");
        private readonly string _gameBoostSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "KitLugia", 
            "gameboost_settings.json");

        public GameBoostPage()
        {
            InitializeComponent();
            _cts = new CancellationTokenSource();

            // 🔥 IMPORTANTE: Inicialização começa como true (vai ser setado false depois)
            _isInitializing = true;

            // 🔥 NOVO: Carrega perfis customizados salvos primeiro (necessário para RestoreEngineSelection)
            LoadCustomProfiles();

            // 🔥 NOVO: Carrega configurações do GameBoost (toggle e checkboxes)
            LoadGameBoostSettings();

            // 🔥 CORREÇÃO: Marca inicialização como completa
            _isInitializing = false;

            // 🔥 CORREÇÃO: Inicializa timer e carrega lista inicial de processos
            InitializeTimer();

            // 🔥 LIMPEZA: Para timer ao sair da página
            this.Unloaded += GameBoostPage_Unloaded;
        }

        // 🔥 NOVO: Evento Loaded - restaura seleção do motor após renderização completa
        private void GameBoostPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔥 CORREÇÃO: Define SelectedIndex padrão antes de restaurar (evita SelectionChanged)
                if (CmbEngine != null && CmbEngine.Items.Count > 0)
                {
                    _isRestoring = true;
                    CmbEngine.SelectedIndex = 0;
                    _isRestoring = false;
                }

                // 🔥 NOVO: Carrega o último motor escolhido e restaura seleção
                var lastEngineConfig = LoadLastEngine();
                RestoreEngineSelection(lastEngineConfig);
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao restaurar seleção no Loaded: {ex.Message}");
            }
        }
        
        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            // 🔥 Cancela todas as tasks em background
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // 🔥 Limpa todas as listas e bindings
            _customProfiles?.Clear();
            _customProfiles = null!;

            // 🔥 CORREÇÃO: Desinscreve do evento Unloaded para evitar memory leak do WPF
            this.Unloaded -= GameBoostPage_Unloaded;

            // Desinscreve do evento ForegroundChanged
            var trayService = GetTrayService();
            if (trayService != null)
            {
                trayService.ForegroundChanged -= OnForegroundChanged;
            }

            _isCleaningUp = true;
        }

        // 🔥 NOVO: Helper para obter TrayIconService
        private static TrayIconService? GetTrayService()
        {
            if (Application.Current.MainWindow is MainWindow mw)
                return mw.TrayService;
            return null;
        }

        private void GameBoostPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private void InitializeTimer()
        {
            // 🔥 CORREÇÃO: Inscreve no evento ForegroundChanged do TrayIconService
            var trayService = GetTrayService();
            if (trayService != null)
            {
                trayService.ForegroundChanged += OnForegroundChanged;
            }
        }

        // 🔥 CORREÇÃO: Handler para evento ForegroundChanged do TrayIconService
        private void OnForegroundChanged(uint pid, IntPtr hwnd)
        {
            if (_isCleaningUp) return;

            try
            {
                // 🔥 CORREÇÃO: Obtém título da janela usando GetWindowText (sem acessar processo)
                string windowTitle = GetWindowTitle(hwnd);

                // 🔥 CORREÇÃO: Usa Dispatcher.Invoke para atualizar UI na thread principal
                Dispatcher.Invoke(() =>
                {
                    if (TxtActiveForeground != null)
                    {
                        TxtActiveForeground.Text = string.IsNullOrEmpty(windowTitle) ? $"Process {pid}" : windowTitle;
                    }

                    // 🔥 NOVO: Atualiza indicador visual do GameBoost
                    if (StatusIndicator != null && TxtStatus != null)
                    {
                        StatusIndicator.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Verde
                        TxtStatus.Text = "Boost ativo - " + (string.IsNullOrEmpty(windowTitle) ? $"Process {pid}" : windowTitle);
                        TxtStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));

                        // 🔥 NOVO: Reseta a animação para mostrar atividade
                        if (StatusGlow != null)
                        {
                            StatusGlow.Color = System.Windows.Media.Color.FromRgb(76, 175, 80);
                        }
                    }

                    // 🔥 NOVO: Atualiza StatusText na seção de processos
                    if (StatusText != null)
                    {
                        StatusText.Text = "BOOST ATIVO";
                        StatusText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                    }
                });
            }
            catch { }
        }

        // 🔥 NOVO: Obtém título da janela usando Win32 API (sem acessar processo)
        private string GetWindowTitle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";

            try
            {
                int length = Win32Api.GetWindowTextLength(hwnd);
                if (length == 0) return "";

                StringBuilder sb = new StringBuilder(length + 1);
                Win32Api.GetWindowText(hwnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        private double GetCpuUsage(Process proc)
        {
            try
            {
                // Simplified CPU usage estimation
                // In production, you'd use PerformanceCounter
                var startTime = proc.StartTime;
                var totalProcessorTime = proc.TotalProcessorTime;
                var elapsed = DateTime.Now - startTime;
                
                if (elapsed.TotalSeconds > 0)
                {
                    return (totalProcessorTime.TotalSeconds / (Environment.ProcessorCount * elapsed.TotalSeconds)) * 100;
                }
            }
            catch { }
            return 0;
        }

        private void TglGameBoost_Checked(object sender, RoutedEventArgs e)
        {
            // 🔥 PROTEÇÃO: Ignora eventos durante carregamento inicial
            if (_isLoadingSettings) return;
            
            if (TxtStatus != null) TxtStatus.Text = "Ativo e monitorando processos";
            if (StatusIndicator != null) StatusIndicator.Fill = new SolidColorBrush(Colors.Lime);
            KitLugia.Core.Logger.Log("🚀 GameBoost Pro ativado via interface");
            
            // 🔥 CORREÇÃO: Ativa E inicializa o GameBoost no serviço
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw?.TrayService != null)
            {
                mw.TrayService.GamePriorityEnabled = true;
                try
                {
                    mw.TrayService.InitializeGameBoost();
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao inicializar: {ex.Message}");
                }
                mw.TrayService.SaveSettings();
            }
            
            SaveGameBoostSettings();
        }

        private void TglGameBoost_Unchecked(object sender, RoutedEventArgs e)
        {
            // 🔥 PROTEÇÃO: Ignora eventos durante carregamento inicial
            if (_isLoadingSettings) return;
            
            if (TxtStatus != null) TxtStatus.Text = "Desativado";
            if (StatusIndicator != null) StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
            KitLugia.Core.Logger.Log("🚀 GameBoost Pro desativado via interface");
            
            // 🔥 CORREÇÃO: Desativa E encerra o GameBoost no serviço
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw?.TrayService != null)
            {
                mw.TrayService.GamePriorityEnabled = false;
                try
                {
                    mw.TrayService.ShutdownGameBoost();
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao encerrar: {ex.Message}");
                }
                mw.TrayService.SaveSettings();
            }
            
            SaveGameBoostSettings();
        }

        // 🔥 NOVO: Carrega configurações do GameBoost
        private void LoadGameBoostSettings()
        {
            _isLoadingSettings = true; // 🔥 Bloqueia eventos durante load
            try
            {
                // 🔥 SIMPLES: Carrega direto do TrayService
                var mw = Application.Current.MainWindow as MainWindow;
                
                // 🔥 IMPORTANTE: NÃO usar padrões! Só ler do serviço.
                bool gameBoostEnabled = false; // Começa com false
                bool trayEnabled = false;
                bool autoStartEnabled = false;
                
                if (mw?.TrayService != null)
                {
                    // Lê valores atuais do serviço (já carregados do Registry)
                    gameBoostEnabled = mw.TrayService.GamePriorityEnabled;
                    trayEnabled = mw.TrayService.IsTrayEnabled;
                }
                
                // AutoStart no TaskScheduler - usa novo método que verifica o caminho
                try
                {
                    autoStartEnabled = Services.TrayIconService.IsAutoStartEnabled();
                }
                catch { }
                
                // Close to Tray
                bool closeToTray = true; // Padrão
                if (mw?.TrayService != null)
                {
                    closeToTray = mw.TrayService.CloseToTray;
                }
                
                // ProBalance
                bool proBalanceEnabled = true; // Padrão
                if (mw?.TrayService != null)
                {
                    proBalanceEnabled = mw.TrayService.ProBalance;
                }
                
                // Aplica valores na UI
                if (TglGameBoost != null) TglGameBoost.IsChecked = gameBoostEnabled;
                if (ChkTrayIcon != null) ChkTrayIcon.IsChecked = trayEnabled;
                if (ChkStartWithWindows != null) ChkStartWithWindows.IsChecked = autoStartEnabled;
                if (ChkCloseToTray != null) ChkCloseToTray.IsChecked = closeToTray;
                if (ChkProBalance != null) ChkProBalance.IsChecked = proBalanceEnabled;
                
                // Atualiza texto e cor do status ProBalance
                UpdateProBalanceStatusText(proBalanceEnabled);
                
                // Atualiza status visual
                if (gameBoostEnabled)
                {
                    if (TxtStatus != null) TxtStatus.Text = "Ativo e monitorando processos";
                    if (StatusIndicator != null) StatusIndicator.Fill = new SolidColorBrush(Colors.Lime);
                }
                else
                {
                    if (TxtStatus != null) TxtStatus.Text = "Desativado";
                    if (StatusIndicator != null) StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                }
                
                // 🔥 IMPORTANTE: NÃO sobrescrever valores no serviço ao carregar!
                // Só lemos, não escrevemos durante o load.

                // 🔥 CORREÇÃO: Não chamar InitializeGameBoost aqui pois ele é chamado no TrayService.Initialize
                // e pode interferir com o motor restaurado
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao carregar configurações: {ex.Message}");
            }
            finally
            {
                _isLoadingSettings = false; // 🔥 Libera eventos após load
            }
        }

        // 🔥 NOVO: Salva configurações do GameBoost
        private void SaveGameBoostSettings()
        {
            try
            {
                // 🔥 SIMPLES: Salva tudo no TrayService (registry)
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw?.TrayService != null)
                {
                    mw.TrayService.GamePriorityEnabled = TglGameBoost?.IsChecked == true;
                    mw.TrayService.SetTrayEnabled(ChkTrayIcon?.IsChecked == true);
                    mw.TrayService.SaveSettings();
                }
                
                // Salva AutoStart no TaskScheduler
                Services.TrayIconService.SetAutoStart(ChkStartWithWindows?.IsChecked == true);
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao salvar configurações: {ex.Message}");
            }
        }

        // 🔥 NOVO: Handler do toggle TrayIcon (ToggleButton)
        private void ChkTrayIcon_Click(object sender, RoutedEventArgs e)
        {
            // 🔥 PROTEÇÃO: Ignora eventos durante carregamento inicial
            if (_isLoadingSettings) return;
            
            try
            {
                var mw = Application.Current.MainWindow as MainWindow;
                if (mw?.TrayService != null)
                {
                    mw.TrayService.SetTrayEnabled(ChkTrayIcon.IsChecked == true);
                    KitLugia.Core.Logger.Log($"🎮 Tray Icon: {(ChkTrayIcon.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Erro ao configurar TrayIcon: {ex.Message}");
            }
        }

        // 🔥 NOVO: Handler do toggle StartWithWindows (ToggleButton)
        private void ChkStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            // 🔥 PROTEÇÃO: Ignora eventos durante carregamento inicial
            if (_isLoadingSettings) return;
            
            try
            {
                Services.TrayIconService.SetAutoStart(ChkStartWithWindows.IsChecked == true);
                
                // Atualizar estado real
                ChkStartWithWindows.IsChecked = Services.TrayIconService.IsAutoStartEnabled();
                
                KitLugia.Core.Logger.Log($"🎮 AutoStart: {(ChkStartWithWindows.IsChecked == true ? "ativado" : "desativado")}");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Erro ao configurar AutoStart: {ex.Message}");
            }
        }
        
        // 🔥 NOVO: Handler do toggle CloseToTray (ToggleButton)
        private void ChkCloseToTray_Click(object sender, RoutedEventArgs e)
        {
            // 🔥 PROTEÇÃO: Ignora eventos durante carregamento inicial
            if (_isLoadingSettings) return;
            
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    mainWindow.TrayService.CloseToTray = ChkCloseToTray.IsChecked == true;
                    mainWindow.TrayService.SaveSettings();
                    KitLugia.Core.Logger.Log($"🎮 Close to Tray: {(ChkCloseToTray.IsChecked == true ? "ativado" : "desativado")}");
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Erro ao configurar Close to Tray: {ex.Message}");
            }
        }
        
        private void ChkProBalance_Click(object sender, RoutedEventArgs e)
        {
            // 🔥 PROTEÇÃO: Ignora eventos durante carregamento inicial
            if (_isLoadingSettings) return;
            
            try
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mainWindow && mainWindow.TrayService != null)
                {
                    bool newState = ChkProBalance.IsChecked == true;
                    mainWindow.TrayService.ProBalance = newState;
                    mainWindow.TrayService.SaveSettings();
                    
                    // Atualiza texto e cor do status
                    UpdateProBalanceStatusText(newState);
                    
                    // Se desativou ProBalance, restaura todos os processos
                    if (!newState)
                    {
                        mainWindow.TrayService.RestoreAllThrottledProcesses();
                        KitLugia.Core.Logger.Log("⚖️ ProBalance desativado - Todos os processos foram restaurados ao normal");
                    }
                    else
                    {
                        KitLugia.Core.Logger.Log("⚖️ ProBalance ativado - Gerenciamento de processos retomado");
                    }
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Erro ao configurar ProBalance: {ex.Message}");
            }
        }
        
        // 🔥 Atualiza texto e cor do status ProBalance
        private void UpdateProBalanceStatusText(bool isEnabled)
        {
            if (TxtProBalanceStatus == null) return;
            
            if (isEnabled)
            {
                TxtProBalanceStatus.Text = "ATIVADO";
                TxtProBalanceStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Verde #4CAF50
            }
            else
            {
                TxtProBalanceStatus.Text = "DESATIVADO";
                TxtProBalanceStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54)); // Vermelho #F44336
            }
        }

        // 🔥 NOVO: Handler para troca de motor do GameBoost
        private void CmbEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 🔥 CORREÇÃO: Ignora eventos durante inicialização
                if (_isInitializing)
                {
                    return;
                }

                // 🔥 CORREÇÃO: Ignora eventos durante restauração
                if (_isRestoring)
                {
                    return;
                }

                // Ignora eventos quando ComboBox não está pronto
                if (CmbEngine == null || CmbEngine.SelectedItem == null)
                {
                    return;
                }

                if (CmbEngine.SelectedItem is ComboBoxItem selected)
                {
                    // Pega a tag do item selecionado
                    if (selected.Tag == null)
                    {
                        return;
                    }

                    var tagValue = selected.Tag.ToString();
                    
                    // 🔥 NOVO: Verifica se é a opção "Personalizado"
                    if (tagValue == "custom")
                    {
                        OpenCustomMotorOverlay();
                        return;
                    }
                    
                    // 🔥 NOVO: Verifica se é um perfil customizado
                    var customProfile = _customProfiles.FirstOrDefault(p => p.Id == tagValue);
                    if (customProfile != null)
                    {
                        ApplyCustomMotorProfile(customProfile);

                        // 🔥 CORREÇÃO: Salva o perfil customizado com EngineNumber=0 para indicar custom
                        SaveLastEngine(0, customProfile.Id);
                        return;
                    }

                    if (!int.TryParse(tagValue, out int engineNumber))
                    {
                        return;
                    }

                    // ⚠️ AVISO: Mostra alerta para V2 e V3 sobre possíveis travamentos
                    if (engineNumber != 1)
                    {
                        string engineName = engineNumber switch
                        {
                            2 => "V2 - FPS Estável",
                            3 => "V3 - Extremo",
                            _ => "Desconhecido"
                        };

                        var result = System.Windows.MessageBox.Show(
                            $"⚠️ ATENÇÃO - Motor {engineName}\n\n" +
                            "Este motor pode causar travamentos inesperados no sistema.\n\n" +
                            "Recomendações:\n" +
                            "• Feche aplicativos desnecessários antes de usar\n" +
                            "• V2: Pode causar micro-travamentos em alguns jogos\n" +
                            "• V3: Pode causar travamentos mais frequentes\n\n" +
                            "Use por sua conta e risco!\n\n" +
                            "Deseja continuar?",
                            "⚠️ Aviso de Performance",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                        {
                            // Volta para V1
                            CmbEngine.SelectedIndex = 0;
                            return;
                        }
                    }

                    // Chama o serviço para trocar o motor
                    Services.TrayIconService.SetEngine(engineNumber);

                    // Atualiza descrição na UI
                    if (TxtEngineDescription != null)
                    {
                        TxtEngineDescription.Text = engineNumber switch
                        {
                            1 => "🟢 V1 - ORIGINAL: Mesmo comportamento da versão antiga (PADRÃO) - Seguro e estável",
                            2 => "🟡 V2 - FPS Estável: EcoQoS OFF + ProBalance médio (>8% CPU) - ⚠️ Pode travar",
                            3 => "🔴 V3 - Extremo: Timer 0.5ms + Network OFF + ProBalance agressivo (>3%) - ⚠️ Pode travar muito",
                            _ => "Desconhecido"
                        };
                    }

                    // Mostra mensagem de confirmação
                    string engineNameConfirm = engineNumber switch
                    {
                        1 => "V1 - ORIGINAL",
                        2 => "V2 - FPS Estável",
                        3 => "V3 - Extremo",
                        _ => "Desconhecido"
                    };

                    // 🔥 NOVO: Salva o motor escolhido (sem perfil customizado)
                    SaveLastEngine(engineNumber, null);

                    // 🔥 RE-APLICA boost ao processo em foreground para mudança imediata
                    ReapplyBoostToCurrentForeground();
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ GameBoost: ERRO em SelectionChanged: {ex.Message}");
                System.Windows.MessageBox.Show($"Erro ao trocar motor: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 🔥 NOVO: Re-aplica boost ao processo em foreground atual
        private async void ReapplyBoostToCurrentForeground()
        {
            try
            {
                // Obtém o processo em foreground atual
                var foregroundWindow = Services.TrayIconService.Win32Api.GetForegroundWindow();
                Services.TrayIconService.Win32Api.GetWindowThreadProcessId(foregroundWindow, out uint foregroundPid);

                if (foregroundPid > 0)
                {
                    // Força re-aplicação do boost com o novo motor (em background)
                    await Task.Run(() =>
                    {
                        if (_cts?.IsCancellationRequested == true) return;
                        Services.TrayIconService.ForceReapplyBoost(foregroundPid);
                    });

                    // 🔥 CORREÇÃO: UI atualizada automaticamente via kernel hook
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Erro ao re-aplicar boost: {ex.Message}");
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar lista de exceções e configurações
            var exceptions = Services.TrayIconService.GetUserExceptions();
            string exceptionList = exceptions.Count > 0 
                ? string.Join(", ", exceptions) 
                : "Nenhum (padrão: Discord, Opera GX, Spotify, etc.)";
            
            string currentEngine = Services.TrayIconService.GetEngineDescription(Services.TrayIconService.CurrentEngine);
            
            MessageBox.Show(
                $"Configurações do GameBoost Pro:\n\n" +
                $"Motor Atual: {currentEngine}\n\n" +
                "🟢 V1 - ORIGINAL (Mesmo da versão antiga):\n" +
                "  • CPU: High Priority\n" +
                "  • I/O: High (3)\n" +
                "  • Page: Maximum (5)\n" +
                "  • Timer: Não boosta\n" +
                "  • EcoQoS: Não aplica\n" +
                "  • ProBalance: Não aplica\n\n" +
                "🟡 V2 - FPS Estável (Baseado no V1):\n" +
                "  • CPU: High Priority\n" +
                "  • I/O: High (3)\n" +
                "  • Page: Maximum (5)\n" +
                "  • Timer: Não boosta\n" +
                "  • EcoQoS: DESATIVADO\n" +
                "  • ProBalance: >8% CPU\n\n" +
                "🔴 V3 - Extremo (Tudo no máximo):\n" +
                "  • CPU: High Priority\n" +
                "  • I/O: High (3)\n" +
                "  • Page: Maximum (5)\n" +
                "  • Timer: 0.5ms (máxima precisão)\n" +
                "  • EcoQoS: DESATIVADO\n" +
                "  • Network: Throttling OFF\n" +
                "  • ProBalance: >3% CPU (agressivo)\n\n" +
                "Exceções do Usuário:\n" + exceptionList + "\n\n" +
                "Windows 11 25H2 Optimized",
                "GameBoost Pro Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        #region 🔥 CUSTOM MOTOR PROFILES

        // 🔥 NOVO: Abre o overlay de configuração de motor personalizado (NOVO)
        private void OpenCustomMotorOverlay()
        {
            // Limpa o ID do perfil em edição (novo perfil)
            _editingProfileId = null;
            
            // Inicializa controles com valores padrão
            TxtCustomProfileName.Text = "Meu Motor Personalizado";
            CmbCpuPriority.SelectedIndex = 1; // High
            CmbIoPriority.SelectedIndex = 1; // High
            CmbPagePriority.SelectedIndex = 1; // Maximum
            CmbThreadMemory.SelectedIndex = 0; // Normal
            
            // Inicializa toggles com valores padrão
            TglTimerResolution.IsChecked = false;
            TglEcoQoS.IsChecked = false;
            TglNetworkBoost.IsChecked = false;
            TglProBalance.IsChecked = true;
            
            // Atualiza textos de status dos toggles
            UpdateToggleStatus(TglTimerResolution, false);
            UpdateToggleStatus(TglEcoQoS, false);
            UpdateToggleStatus(TglNetworkBoost, false);
            UpdateToggleStatus(TglProBalance, true);
            
            SliderProBalance.Value = 5;
            TxtProBalanceValue.Text = "5%";

            // Mostra overlay
            OverlayCustomMotor.Visibility = Visibility.Visible;
        }

        // 🔥 NOVO: Abre o overlay para EDITAR um perfil existente
        private void OpenEditCustomMotorOverlay(CustomMotorProfile profile)
        {
            // Guarda o ID do perfil em edição
            _editingProfileId = profile.Id;
            
            // Preenche controles com valores do perfil existente
            TxtCustomProfileName.Text = profile.Name;
            
            // CPU Priority
            CmbCpuPriority.SelectedIndex = profile.CpuPriority.ToLower() switch
            {
                "normal" => 0,
                "high" => 1,
                "realtime" => 2,
                _ => 1
            };
            
            // I/O Priority
            CmbIoPriority.SelectedIndex = profile.IoPriority;
            
            // Page Priority
            CmbPagePriority.SelectedIndex = profile.PagePriority;
            
            // Thread Memory Priority
            CmbThreadMemory.SelectedIndex = profile.ThreadMemoryPriority;
            
            // Toggles
            TglTimerResolution.IsChecked = profile.TimerResolution;
            TglEcoQoS.IsChecked = profile.EcoQoS;
            TglNetworkBoost.IsChecked = profile.NetworkBoost;
            TglProBalance.IsChecked = profile.ProBalanceEnabled;
            
            // Atualiza textos de status dos toggles
            UpdateToggleStatus(TglTimerResolution, profile.TimerResolution);
            UpdateToggleStatus(TglEcoQoS, profile.EcoQoS);
            UpdateToggleStatus(TglNetworkBoost, profile.NetworkBoost);
            UpdateToggleStatus(TglProBalance, profile.ProBalanceEnabled);
            
            // ProBalance Threshold
            SliderProBalance.Value = profile.ProBalanceThreshold;
            TxtProBalanceValue.Text = $"{profile.ProBalanceThreshold}%";

            // Mostra overlay
            OverlayCustomMotor.Visibility = Visibility.Visible;
        }

        // 🔥 NOVO: Atualiza valor do slider ProBalance
        private void SliderProBalance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtProBalanceValue != null)
            {
                TxtProBalanceValue.Text = $"{(int)SliderProBalance.Value}%";
            }
        }

        // 🔥 NOVO: Handlers para ToggleButtons - atualiza status visual
        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
            UpdateToggleStatus(sender as ToggleButton, true);
        }

        private void ToggleButton_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateToggleStatus(sender as ToggleButton, false);
        }

        private void UpdateToggleStatus(ToggleButton? toggle, bool isChecked)
        {
            if (toggle is null) return;

            var redBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 107, 107));
            var greenBrush = new SolidColorBrush(Colors.LimeGreen);
            var turquoiseBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 78, 205, 196));

            // Atualiza o texto e cor do status baseado no toggle
            if (toggle == TglTimerResolution && TxtTimerResolutionStatus != null)
            {
                TxtTimerResolutionStatus.Text = isChecked ? "⏱️ Timer Resolution - ATIVADO" : "⏱️ Timer Resolution - DESATIVADO";
                TxtTimerResolutionStatus.Foreground = isChecked ? greenBrush : redBrush;
            }
            else if (toggle == TglEcoQoS && TxtEcoQoSStatus != null)
            {
                TxtEcoQoSStatus.Text = isChecked ? "🌱 EcoQoS - ATIVADO (Performance Max!)" : "🌱 EcoQoS - DESATIVADO";
                TxtEcoQoSStatus.Foreground = isChecked ? greenBrush : redBrush;
            }
            else if (toggle == TglNetworkBoost && TxtNetworkBoostStatus != null)
            {
                TxtNetworkBoostStatus.Text = isChecked ? "🌐 Network Boost - ATIVADO" : "🌐 Network Boost - DESATIVADO";
                TxtNetworkBoostStatus.Foreground = isChecked ? greenBrush : redBrush;
            }
            else if (toggle == TglProBalance && TxtProBalanceStatusCustom != null)
            {
                TxtProBalanceStatusCustom.Text = isChecked ? "⚖️ ProBalance - ATIVADO" : "⚖️ ProBalance - DESATIVADO";
                TxtProBalanceStatusCustom.Foreground = isChecked ? turquoiseBrush : redBrush;
            }
        }

        // 🔥 NOVO: Fecha o overlay sem salvar
        private void BtnCancelCustomMotor_Click(object sender, RoutedEventArgs e)
        {
            OverlayCustomMotor.Visibility = Visibility.Collapsed;
            _editingProfileId = null; // Limpa estado de edição
            _isRestoring = true; // 🔥 CORREÇÃO: Evita SelectionChanged
            CmbEngine.SelectedIndex = 0; // Volta para V1
            _isRestoring = false;
        }

        // 🔥 NOVO: Salva o perfil customizado (novo ou edição)
        private void BtnSaveCustomMotor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Valida nome
                var profileName = TxtCustomProfileName.Text?.Trim();
                if (string.IsNullOrWhiteSpace(profileName))
                {
                    MessageBox.Show("Por favor, digite um nome para o perfil.", "Nome Obrigatório", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Coleta valores dos controles
                var profile = new CustomMotorProfile
                {
                    Id = _editingProfileId ?? ("custom_" + Guid.NewGuid().ToString("N")[..8]),
                    Name = profileName,
                    CpuPriority = CmbCpuPriority.SelectedIndex switch
                    {
                        0 => "Normal",
                        1 => "High",
                        2 => "RealTime",
                        _ => "High"
                    },
                    IoPriority = CmbIoPriority.SelectedIndex,
                    PagePriority = CmbPagePriority.SelectedIndex,
                    ThreadMemoryPriority = CmbThreadMemory.SelectedIndex,
                    TimerResolution = TglTimerResolution.IsChecked == true,
                    EcoQoS = TglEcoQoS.IsChecked == true,
                    NetworkBoost = TglNetworkBoost.IsChecked == true,
                    ProBalanceEnabled = TglProBalance.IsChecked == true,
                    ProBalanceThreshold = (int)SliderProBalance.Value
                };

                bool isEditing = !string.IsNullOrEmpty(_editingProfileId);

                if (isEditing)
                {
                    // 🔥 EDIÇÃO: Atualiza perfil existente
                    var existingProfile = _customProfiles.FirstOrDefault(p => p.Id == _editingProfileId);
                    if (existingProfile != null)
                    {
                        // Atualiza na lista
                        _customProfiles.Remove(existingProfile);
                        _customProfiles.Add(profile);
                        
                        // Atualiza no ComboBox (remove e readiciona)
                        for (int i = 0; i < CmbEngine.Items.Count; i++)
                        {
                            if (CmbEngine.Items[i] is ComboBoxItem item && item.Tag?.ToString() == _editingProfileId)
                            {
                                CmbEngine.Items.RemoveAt(i);
                                break;
                            }
                        }
                        AddCustomProfileToComboBox(profile);
                        
                        MessageBox.Show($"Perfil '{profile.Name}' atualizado com sucesso!", "Perfil Atualizado", 
                                      MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // 🔥 NOVO: Adiciona perfil novo
                    _customProfiles.Add(profile);
                    AddCustomProfileToComboBox(profile);
                    
                    MessageBox.Show($"Perfil '{profile.Name}' criado com sucesso!", "Perfil Salvo", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Fecha overlay
                OverlayCustomMotor.Visibility = Visibility.Collapsed;
                
                // Limpa estado de edição
                _editingProfileId = null;

                // 🔥 NOVO: Salva perfis no arquivo
                SaveCustomProfiles();

                // Seleciona o perfil no ComboBox (isso dispara SelectionChanged que aplica o perfil)
                SelectCustomProfile(profile.Id);
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ GameBoost: Erro ao salvar perfil: {ex.Message}");
                MessageBox.Show($"Erro ao salvar perfil: {ex.Message}", "Erro", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🔥 NOVO: Adiciona perfil customizado ao ComboBox (com botões de editar e excluir)
        private void AddCustomProfileToComboBox(CustomMotorProfile profile)
        {
            // Cria o item principal
            var item = new ComboBoxItem
            {
                Content = $"🔧 {profile.Name}",
                Tag = profile.Id,
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Container com botões de editar e excluir
            var container = new Grid();
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            container.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textBlock = new TextBlock 
            { 
                Text = $"🔧 {profile.Name}", 
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(textBlock, 0);

            // Botão Editar (ícone branco/amarelo claro)
            var editBtn = new System.Windows.Controls.Button
            {
                Content = "⚙️",
                Width = 24,
                Height = 24,
                FontSize = 12,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Editar perfil",
                Padding = new Thickness(0),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 215, 0)) // Amarelo dourado claro
            };
            editBtn.Click += (s, e) => 
            { 
                e.Handled = true;
                OpenEditCustomMotorOverlay(profile); 
            };
            Grid.SetColumn(editBtn, 1);

            // Botão Excluir (ícone branco/vermelho claro)
            var deleteBtn = new System.Windows.Controls.Button
            {
                Content = "🗑️",
                Width = 24,
                Height = 24,
                FontSize = 12,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Excluir perfil",
                Padding = new Thickness(0),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 255, 100, 100)) // Vermelho claro
            };
            deleteBtn.Click += (s, e) => 
            { 
                e.Handled = true;
                ShowDeleteConfirmation(profile); 
            };
            Grid.SetColumn(deleteBtn, 2);

            container.Children.Add(textBlock);
            container.Children.Add(editBtn);
            container.Children.Add(deleteBtn);

            item.Content = container;
            item.Tag = profile.Id;

            // Insere antes do item "Personalizado" (último item)
            CmbEngine.Items.Insert(CmbEngine.Items.Count - 1, item);
        }

        // 🔥 NOVO: Seleciona um perfil customizado no ComboBox
        private void SelectCustomProfile(string profileId)
        {
            _isRestoring = true; // 🔥 CORREÇÃO: Evita SelectionChanged
            for (int i = 0; i < CmbEngine.Items.Count; i++)
            {
                if (CmbEngine.Items[i] is ComboBoxItem item && item.Tag?.ToString() == profileId)
                {
                    CmbEngine.SelectedIndex = i;
                    break;
                }
            }
            _isRestoring = false;
        }

        // 🔥 NOVO: Aplica um perfil customizado
        private void ApplyCustomMotorProfile(CustomMotorProfile profile)
        {
            try
            {
                
                // Converte para configurações do TrayIconService
                var config = new Services.CustomEngineConfig
                {
                    CpuPriority = profile.CpuPriority,
                    IoPriorityLevel = profile.IoPriority,
                    PagePriorityLevel = profile.PagePriority,
                    TimerBoost = profile.TimerResolution,
                    EcoQoSEnabled = profile.EcoQoS,
                    ProBalance = profile.ProBalanceEnabled,
                    ProBalanceCpuThreshold = profile.ProBalanceThreshold,
                    NetworkBoost = profile.NetworkBoost,
                    ThreadMemoryPriority = profile.ThreadMemoryPriority
                };

                // Configura o motor personalizado no serviço
                Services.TrayIconService.SetCustomEngine(config);

                // Atualiza descrição na UI
                if (TxtEngineDescription != null)
                {
                    TxtEngineDescription.Text = $"🔧 {profile.Name}: Perfil personalizado | CPU: {profile.CpuPriority} | ProBalance: {(profile.ProBalanceEnabled ? $"ON (> {profile.ProBalanceThreshold}%)" : "OFF")}";
                }

                // Re-aplica ao foreground (async para não travar UI)
                ReapplyBoostToCurrentForeground();
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ GameBoost: Erro ao aplicar perfil: {ex.Message}");
                MessageBox.Show($"Erro ao aplicar perfil: {ex.Message}", "Erro", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🔥 NOVO: Mostra confirmação de exclusão usando overlay
        private void ShowDeleteConfirmation(CustomMotorProfile profile)
        {
            _profileToDelete = profile;
            TxtDeleteProfileName.Text = profile.Name;
            OverlayConfirmDelete.Visibility = Visibility.Visible;
        }

        // 🔥 NOVO: Cancela exclusão
        private void BtnCancelDelete_Click(object sender, RoutedEventArgs e)
        {
            OverlayConfirmDelete.Visibility = Visibility.Collapsed;
            _profileToDelete = null;
        }

        // 🔥 NOVO: Confirma exclusão
        private void BtnConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            ConfirmDeleteProfile();
            OverlayConfirmDelete.Visibility = Visibility.Collapsed;
        }

        // 🔥 NOVO: Lógica de exclusão do perfil
        private void ConfirmDeleteProfile()
        {
            if (_profileToDelete == null) return;

            try
            {
                // Remove da lista
                _customProfiles.Remove(_profileToDelete);

                // Remove do ComboBox
                for (int i = CmbEngine.Items.Count - 1; i >= 0; i--)
                {
                    if (CmbEngine.Items[i] is ComboBoxItem item && item.Tag?.ToString() == _profileToDelete.Id)
                    {
                        CmbEngine.Items.RemoveAt(i);
                        break;
                    }
                }

                // 🔥 NOVO: Salva perfis após exclusão
                SaveCustomProfiles();

                // Volta para Padrão Windows
                _isRestoring = true; // 🔥 CORREÇÃO: Evita SelectionChanged
                CmbEngine.SelectedIndex = 0;
                _isRestoring = false;

                _profileToDelete = null;
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ GameBoost: Erro ao excluir perfil: {ex.Message}");
            }
        }

        // 🔥 NOVO: Carrega perfis customizados do arquivo JSON
        private void LoadCustomProfiles()
        {
            try
            {
                if (!File.Exists(_profilesFilePath))
                {
                }

                var json = File.ReadAllText(_profilesFilePath);
                var profiles = JsonSerializer.Deserialize<List<CustomMotorProfile>>(json);
                
                if (profiles != null && profiles.Count > 0)
                {
                    _customProfiles = profiles;
                    
                    // Adiciona cada perfil ao ComboBox
                    foreach (var profile in _customProfiles)
                    {
                        AddCustomProfileToComboBox(profile);
                    }
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao carregar perfis: {ex.Message}");
            }
        }

        // 🔥 NOVO: Salva perfis customizados em arquivo JSON
        private void SaveCustomProfiles()
        {
            try
            {
                // Garante que o diretório existe
                var directory = Path.GetDirectoryName(_profilesFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                var json = JsonSerializer.Serialize(_customProfiles, options);
                File.WriteAllText(_profilesFilePath, json);
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao salvar perfis: {ex.Message}");
            }
        }

        // 🔥 NOVO: Classe para configuração de motor (suporta fixo e customizado)
        private class EngineConfig
        {
            public string EngineType { get; set; } = "fixed"; // "fixed" ou "custom"
            public int EngineNumber { get; set; } = 1; // 1, 2, 3 (se fixed)
            public string? CustomProfileId { get; set; } = null; // ID do perfil (se custom)
        }

        // 🔥 NOVO: Carrega o último motor escolhido do arquivo JSON
        private EngineConfig LoadLastEngine()
        {
            try
            {
                if (!File.Exists(_engineConfigPath))
                {
                    return new EngineConfig { EngineType = "fixed", EngineNumber = 1 };
                }

                var json = File.ReadAllText(_engineConfigPath);
                var config = JsonSerializer.Deserialize<EngineConfig>(json);

                if (config != null)
                {
                    // 🔥 CORREÇÃO: Validação corrigida para aceitar EngineNumber=0 como custom
                    if (config.EngineType == "custom" || config.EngineNumber == 0)
                    {
                        if (!string.IsNullOrEmpty(config.CustomProfileId))
                        {
                            return config;
                        }
                    }
                    if (config.EngineType == "fixed" && config.EngineNumber >= 1 && config.EngineNumber <= 3)
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao carregar motor: {ex.Message}");
            }

            return new EngineConfig { EngineType = "fixed", EngineNumber = 1 };
        }

        // 🔥 NOVO: Salva o último motor escolhido no arquivo JSON
        private void SaveLastEngine(int engineNumber, string? customProfileId = null)
        {
            try
            {
                // Garante que o diretório existe
                var directory = Path.GetDirectoryName(_engineConfigPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var config = new EngineConfig
                {
                    EngineType = string.IsNullOrEmpty(customProfileId) ? "fixed" : "custom",
                    EngineNumber = engineNumber,
                    CustomProfileId = customProfileId
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(_engineConfigPath, json);
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao salvar motor: {ex.Message}");
            }
        }

        // 🔥 NOVO: Restaura a seleção do motor no ComboBox
        private void RestoreEngineSelection(EngineConfig config)
        {
            try
            {
                // 🔥 CORREÇÃO: Define flag para evitar SelectionChanged durante restauração
                _isRestoring = true;

                // 🔥 CORREÇÃO: Se é um perfil customizado (EngineType="custom" ou EngineNumber=0)
                if (config.EngineType == "custom" || config.EngineNumber == 0)
                {
                    if (!string.IsNullOrEmpty(config.CustomProfileId))
                    {
                        // Procura o perfil no ComboBox
                        for (int i = 0; i < CmbEngine.Items.Count; i++)
                        {
                            if (CmbEngine.Items[i] is ComboBoxItem item)
                            {
                                var tagValue = item.Tag?.ToString();
                                if (tagValue == config.CustomProfileId)
                                {
                                    CmbEngine.SelectedIndex = i;

                                    // Aplica o perfil customizado
                                    var profile = _customProfiles?.FirstOrDefault(p => p.Id == config.CustomProfileId);
                                    if (profile != null)
                                    {
                                        ApplyCustomMotorProfile(profile);
                                    }

                                    _isRestoring = false;
                                    return;
                                }
                            }
                        }
                    }
                    // Se não encontrou o perfil customizado, volta para V1 sem aplicar
                    CmbEngine.SelectedIndex = 0;
                    _isRestoring = false;
                    return;
                }

                // Motor fixo (V1, V2, V3)
                for (int i = 0; i < CmbEngine.Items.Count; i++)
                {
                    if (CmbEngine.Items[i] is ComboBoxItem item)
                    {
                        var tagValue = item.Tag?.ToString();
                        if (tagValue == config.EngineNumber.ToString())
                        {
                            CmbEngine.SelectedIndex = i;

                            // Atualiza a descrição também
                            if (TxtEngineDescription != null)
                            {
                                TxtEngineDescription.Text = config.EngineNumber switch
                                {
                                    1 => "🟢 V1 - ORIGINAL: Mesmo comportamento da versão antiga (PADRÃO) - Seguro e estável",
                                    2 => "🟡 V2 - FPS Estável: EcoQoS OFF + ProBalance médio (>8% CPU) - ⚠️ Pode travar",
                                    3 => "🔴 V3 - Extremo: Timer 0.5ms + Network OFF + ProBalance agressivo (>3%) - ⚠️ Pode travar muito",
                                    _ => "Desconhecido"
                                };
                            }

                            // Aplica o motor
                            Services.TrayIconService.SetEngine(config.EngineNumber);

                            _isRestoring = false;
                            return;
                        }
                    }
                }

                _isRestoring = false;
            }
            catch (Exception ex)
            {
                _isRestoring = false;
                KitLugia.Core.Logger.Log($"⚠️ GameBoost: Erro ao restaurar seleção: {ex.Message}");
            }
        }

        #endregion
    }

    // 🔥 NOVO: Classe para perfis de motor personalizados
    public class CustomMotorProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string CpuPriority { get; set; } = "High"; // Normal, High, RealTime
        public int IoPriority { get; set; } = 1; // 0=Normal(2), 1=High(3), 2=Critical
        public int PagePriority { get; set; } = 1; // 0=Normal(5), 1=Max(5)
        public bool TimerResolution { get; set; } = false;
        public bool EcoQoS { get; set; } = false; // true=ON (economia), false=OFF (performance)
        public bool ProBalanceEnabled { get; set; } = true;
        public int ProBalanceThreshold { get; set; } = 5; // % CPU
        public bool NetworkBoost { get; set; } = false;
        public int ThreadMemoryPriority { get; set; } = 0; // 0=Normal, 1=Maximum
    }

    public class ProcessInfo
    {
        public string Name { get; set; } = "";
        public string Pid { get; set; } = "";
        public string CpuUsage { get; set; } = "";
        public System.Windows.Media.Brush CpuColor { get; set; } = Brushes.White;
        public string Priority { get; set; } = "";
        public System.Windows.Media.Brush PriorityColor { get; set; } = Brushes.Gray;
        public bool IsForeground { get; set; }
    }
}
