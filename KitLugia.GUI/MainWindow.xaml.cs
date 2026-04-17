using System;
using System.Collections.Generic;
using System.Diagnostics; // 🔥 Adicionado para corrigir Process
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media; // 🔥 Adicionado para animações
using System.Windows.Media.Animation; // 🔥 Adicionado para Storyboard e DoubleAnimation
using System.Windows.Threading;
using KitLugia.Core;
using KitLugia.GUI.Controls;
using KitLugia.GUI.Pages;
using KitLugia.GUI.Services;
using MessageBox = System.Windows.MessageBox; // 🔥 Corrige ambiguidade
using Button = System.Windows.Controls.Button; // 🔥 Corrige ambiguidade

// --- CORREÇÃO DOS ERROS DE AMBIGUIDADE ---
// Estas linhas forçam o código a usar os componentes do WPF
using RadioButton = System.Windows.Controls.RadioButton;
using Application = System.Windows.Application;
using Logger = KitLugia.Core.Logger;

namespace KitLugia.GUI
{
    public partial class MainWindow : Window
    {
        private const int MaxVisibleToasts = 6;
        private Dictionary<string, LugiaToast> _activeToasts = new Dictionary<string, LugiaToast>();
        private TaskCompletionSource<bool>? _confirmCompletionSource;

        // 🔥 CORREÇÃO: Armazenar handlers para poder removê-los no cleanup
        private Action<string>? _logHandler;
        private Action? _notificationCountHandler;

        // Tray Icon RAM Monitor
        private TrayIconService? _trayService;
        public TrayIconService? TrayService => _trayService;

        // Timer para o Debounce da pesquisa
        private DispatcherTimer _searchDebounceTimer;

        // 🔥 NOVO: healthCheckTimer precisa ser parado no Cleanup
        private DispatcherTimer? _healthCheckTimer;

        // Single-instance show window signaling
        private System.Threading.EventWaitHandle? _showWindowEvent;
        private System.Threading.Thread? _showWindowMonitor;

        // 🔥 NOVO: CancellationTokenSource para cancelar tasks de background
        private CancellationTokenSource? _backgroundTasksCts;

        public MainWindow()
        {
            InitializeComponent();

            // 🔥 NOVO: Inicializa CancellationTokenSource para tasks de background
            _backgroundTasksCts = new CancellationTokenSource();

            // Inicializa a Engine de Busca
            SearchEngine.Initialize();

            // Configura o timer de pesquisa (300ms)
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += SearchDebounce_Tick;

            // Inicia na Dashboard (navegação segura)
            MainFrame.Navigate(new DashboardPage());
            if (BtnDashboard != null) BtnDashboard.IsChecked = true;
            
            // Limpa histórico inicial para evitar acúmulo desde o início
            while (MainFrame.NavigationService.CanGoBack)
            {
                MainFrame.NavigationService.RemoveBackEntry();
            }

            // Conecta o Logger do Core ao Console da GUI
            _logHandler = (msg) => ConsoleManager.WriteLine(msg);
            KitLugia.Core.Logger.OnLogReceived += _logHandler;

            // Conecta o contador de notificações
            _notificationCountHandler = UpdateNotificationBadge;
            NotificationHistoryManager.OnCountChanged += _notificationCountHandler;

            // Configura o fechamento do painel de console (Rodapé)
            if (GlobalConsolePanel != null)
                GlobalConsolePanel.RequestClose += (s, e) => { GlobalConsolePanel.Visibility = Visibility.Collapsed; };

            // --- CORREÇÃO: Configura o fechamento do Terminal Legacy (Tela Cheia) ---
            if (LegacyTerminalPanel != null)
                LegacyTerminalPanel.RequestClose += (s, e) => { LegacyTerminalPanel.Visibility = Visibility.Collapsed; };

            // --- TRAY ICON: Inicializa o Monitor de RAM ---
            _trayService = new TrayIconService();
            _trayService.OnOpenMainWindow += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    Focus();
                });
            };
            _trayService.OnOpenSettings += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    Focus();
                    NavigateToPage("🔔");
                });
            };
            _trayService.Initialize();

            // 🔥 REMOVIDO: Monitoramento contínuo - não pausa quando janela perde foco
            // O GameBoost continua funcionando mesmo com a janela minimizada
            // Logs rotativos de foreground foram removidos para evitar acumulo

            // --- AUTO-START: Garante que o app inicie com o Windows se o Tray estiver ativo ---
            if (TrayIconService.IsTrayEnabledStatic())
            {
                TrayIconService.SetAutoStart(true);
                
                // 🔥 Verificação cirúrgica de instância única
                var currentPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                Logger.Log($"KitLugia iniciado: {currentPath}");
                Logger.Log($"Tray ativo: {TrayIconService.IsTrayEnabledStatic()}");
                
                // 🔥 CHECK 5: Verificação de saúde do Tray Icon após 3 segundos
                _healthCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                _healthCheckTimer.Tick += (s, e) =>
                {
                    _healthCheckTimer?.Stop();
                    if (_trayService != null && !_trayService.IsTrayIconHealthy())
                    {
                        Logger.Log("❌ Tray Icon não está saudável, tentando recuperar...");
                        if (_trayService.RecoverTrayIcon())
                        {
                            Logger.Log("✅ Tray Icon recuperado com sucesso");
                        }
                        else
                        {
                            Logger.Log("❌ Falha na recuperação do Tray Icon");
                        }
                    }
                    else
                    {
                        Logger.Log("✅ Tray Icon está saudável");
                    }
                };
                _healthCheckTimer.Start();
            }

            // 🔥 AUTO-UPDATER: Inicia verificação automática em background com CancellationToken
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), _backgroundTasksCts.Token); // Espera 10s para iniciar
                    if (!_backgroundTasksCts.Token.IsCancellationRequested)
                    {
                        await GitHubUpdater.StartAutoUpdateCheck();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Task foi cancelada, não fazer nada
                }
            }, _backgroundTasksCts.Token);

            // --- INTELLIGENT MEMORY CLEANER: Limpeza baseada em limite de memória ---
            AggressiveMemoryCleaner.StartIntelligentMonitoring(5, 80); // Verifica a cada 5s, limpa em 80MB
            Logger.Log("🧹 MemoryCleaner inteligente iniciado - Limite: 80MB, Verificação: 5s");

            // --- MODO DESENVOLVEDOR: Inicializa com menu de debug OCULTO por padrão ---
            UpdateDebugMenuVisibility(false);

            // --- NAMED EVENT: Permite que uma segunda instância sinalize para mostrar a janela ---
            _showWindowEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "KitLugia_ShowWindow");
            _showWindowMonitor = new System.Threading.Thread(() =>
            {
                while (!_backgroundTasksCts.Token.IsCancellationRequested)
                {
                    if (_showWindowEvent.WaitOne(1000)) // Timeout de 1s para verificar cancelamento
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Show();
                            WindowState = WindowState.Normal;
                            Activate();
                            Focus();
                        });
                    }
                }
            }) { IsBackground = true, Name = "ShowWindowMonitor" };
            _showWindowMonitor.Start();
        }

        // =========================================================
        // MÉTODO PÚBLICO PARA ABRIR O TERMINAL LEGACY
        // (Chamado pela Dashboard e pela página Sobre)
        // =========================================================
        public void OpenLegacyTerminal()
        {
            if (LegacyTerminalPanel != null)
            {
                LegacyTerminalPanel.Visibility = Visibility.Visible;
                // Opcional: Focar no input se o controle suportar
            }
        }

        #region LIVE SEARCH (PESQUISA GLOBAL)

        private void TxtGlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchDebounce_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            PerformLiveSearch(TxtGlobalSearch.Text);
        }

        private void PerformLiveSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                if (SearchPopup != null) SearchPopup.IsOpen = false;
                return;
            }

            if (MainFrame.Content is GlobalSearchPage searchPage)
            {
                searchPage.UpdateSearch(query);
            }
            else
            {
                UncheckAllNavButtons();
                CleanupAndNavigate(new GlobalSearchPage(query));
            }
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSearchResults.SelectedItem is GlobalSearchResult result)
            {
                TxtGlobalSearch.Text = "";
                if (SearchPopup != null) SearchPopup.IsOpen = false;
            }
        }
        #endregion

        #region SISTEMA DE NAVEGAÇÃO

        public bool IsNavigationLocked { get; set; } = false;

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsNavigationLocked)
            {
                if (sender is RadioButton rb)
                {
                    rb.IsChecked = false; // Desmarca o botão que o usuário tentou clicar
                    // Tenta remarcar o botão da página atual
                    UpdateNavButtonsSelection();
                }
                ShowError("NAVEGAÇÃO BLOQUEADA", "Uma operação crítica de disco está em curso. Aguarde a conclusão para trocar de página.");
                return;
            }

            if (sender is RadioButton btn)
            {
                if (TxtGlobalSearch != null) TxtGlobalSearch.Text = "";
                NavigateToPage(btn.Tag?.ToString(), btn);
            }
        }

        private void UpdateNavButtonsSelection()
        {
            if (MainFrame.Content is DashboardPage) BtnDashboard.IsChecked = true;
            else if (MainFrame.Content is TweaksPage) BtnTweaks.IsChecked = true;
                        else if (MainFrame.Content is BloatwarePage) BtnApps.IsChecked = true;
            else if (MainFrame.Content is CleanupPage) BtnStorage.IsChecked = true;
            else if (MainFrame.Content is NetworkPage) BtnNetwork.IsChecked = true;
            else if (MainFrame.Content is GamesPage) BtnGames.IsChecked = true;
            else if (MainFrame.Content is ServicesPage) BtnServices.IsChecked = true;
            else if (MainFrame.Content is RepairsPage) BtnRepairs.IsChecked = true;
            else if (MainFrame.Content is DriversPage) BtnDrivers.IsChecked = true;
            else if (MainFrame.Content is PartitionsPage) BtnPartitions.IsChecked = true;
            else if (MainFrame.Content is TraySettingsPage) { if (BtnTray != null) BtnTray.IsChecked = true; }
            else if (MainFrame.Content is IntegrityPage) { if (BtnIntegrity != null) BtnIntegrity.IsChecked = true; }
            else if (MainFrame.Content is GameBoostPage) { if (BtnGameBoost != null) BtnGameBoost.IsChecked = true; }
            else if (MainFrame.Content is ToolsPage) { }
            else if (MainFrame.Content is WinbootPage) { }
            else if (MainFrame.Content is AdvancedToolsPage) { }
            else if (MainFrame.Content is SecurityPage) { }
            else if (MainFrame.Content is PrivacyPage) { }
            else if (MainFrame.Content is ActivationPage) { }
            else if (MainFrame.Content is UpdatePage) { }
            else if (MainFrame.Content is DiagnosticPage) { if (BtnDiagnostic != null) BtnDiagnostic.IsChecked = true; }
        }

        private void UncheckAllNavButtons()
        {
            if (BtnDashboard != null) BtnDashboard.IsChecked = false;
            if (BtnTweaks != null) BtnTweaks.IsChecked = false;
                        if (BtnApps != null) BtnApps.IsChecked = false;
            if (BtnStorage != null) BtnStorage.IsChecked = false;
            if (BtnNetwork != null) BtnNetwork.IsChecked = false;
            if (BtnGames != null) BtnGames.IsChecked = false;
            if (BtnServices != null) BtnServices.IsChecked = false;
            if (BtnRepairs != null) BtnRepairs.IsChecked = false;
            if (BtnDrivers != null) BtnDrivers.IsChecked = false;
            if (BtnPartitions != null) BtnPartitions.IsChecked = false;

            // CORREÇÃO: Garante que o botão de integridade no topo também seja desmarcado
            if (BtnIntegrity != null) BtnIntegrity.IsChecked = false;
            if (BtnTray != null) BtnTray.IsChecked = false;
            if (BtnDiagnostic != null) BtnDiagnostic.IsChecked = false;
        }

        public void NavigateToPage(string? pageTag, object? senderButton = null)
        {
            if (pageTag == null) return;

            string baseTag = pageTag;
            int tabIndex = 0;

            if (pageTag.Contains(":"))
            {
                var parts = pageTag.Split(':');
                baseTag = parts[0];
                if (parts.Length > 1) int.TryParse(parts[1], out tabIndex);
            }

            // SEGURANÇA GERAL: Bloqueia navegação se houver operação crítica
            if (IsNavigationLocked)
            {
                ShowError("BLOQUEADO", "Aguarde a operação de disco finalizar.");
                return;
            }

            // SEGURANÇA WINBOOT: Pergunta se deseja sair se estiver na página de Winboot
            if (MainFrame.Content is WinbootPage winPage)
            {
                var result = System.Windows.MessageBox.Show("Tem certeza que deseja sair do Winboot? Suas configurações de partição em andamento podem ser perdidas.", 
                                           "KitLugia - Confirmação", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    // Restaura o botão de navegação correto (se possível) ou apenas cancela
                    if (senderButton is RadioButton rb) rb.IsChecked = false;
                    return;
                }
            }



            if (senderButton == null)
            {
                UncheckAllNavButtons();
                switch (baseTag)
                {
                    case "🏠": if (BtnDashboard != null) BtnDashboard.IsChecked = true; break;
                    case "⚡": if (BtnTweaks != null) BtnTweaks.IsChecked = true; break;
                                        case "📱": if (BtnApps != null) BtnApps.IsChecked = true; break;
                    case "💿": if (BtnStorage != null) BtnStorage.IsChecked = true; break;
                    case "🌐": if (BtnNetwork != null) BtnNetwork.IsChecked = true; break;
                    case "🎮": if (BtnGames != null) BtnGames.IsChecked = true; break;
                    case "🛡️": if (BtnServices != null) BtnServices.IsChecked = true; break;
                    case "🔧": if (BtnRepairs != null) BtnRepairs.IsChecked = true; break;
                    case "💾": if (BtnDrivers != null) BtnDrivers.IsChecked = true; break;
                    case "💽": if (BtnPartitions != null) BtnPartitions.IsChecked = true; break;
                    case "🧰": if (BtnIntegrity != null) BtnIntegrity.IsChecked = true; break;
                    case "�": if (BtnTray != null) BtnTray.IsChecked = true; break;
                    case "🚀": if (BtnGameBoost != null) BtnGameBoost.IsChecked = true; break;
                    case "🔬": if (BtnDiagnostic != null) BtnDiagnostic.IsChecked = true; break;
                }
            }

            Page? newPage = baseTag switch
            {
                "🏠" => new DashboardPage(),
                "⚡" => new TweaksPage(),
                "🖥️" => new ScreenPage(),
                "📱" => new BloatwarePage(),
                "💿" => new CleanupPage(),
                "🌐" => new NetworkPage(),
                "🎮" => new GamesPage(),
                "🛠️" => new ToolsPage(tabIndex),
                "🚀" => new GameBoostPage(), // 🔥 NOVO: GameBoost Pro
                "🛡️" => new ServicesPage(tabIndex),
                "🔧" => new RepairsPage(),
                "💾" => new DriversPage(),
                "💽" => new PartitionsPage(),
                "💻" => new WinbootPage(), // 🔥 NOVO: WinBoot (Instalação Windows)
                "🔨" => new AdvancedToolsPage(), // 🔥 NOVO: Winhance + WinBoot + Partições
                "🧰" => new IntegrityPage(), // 🔥 NOVO: Integridade do Sistema (Scan)
                "🛡️ShutUp" => new ServicesPage(1), // 🔥 NOVO: O&O ShutUp10 (tab 1)
                "🔐" => new SecurityPage(),
                "⚙️" => new TweaksPage(), // Otimização integrada em Tweaks
                "🔒" => new PrivacyPage(),
                "🔑" => new ActivationPage(),
                "🔔" => new TraySettingsPage(),
                "🔄" => new UpdatePage(), // 🔥 Página de atualizações
                "🔬" => new DiagnosticPage(), // 🔥 NOVO: Diagnóstico Interno
                _ => null
            };

            if (newPage != null)
            {
                // 🔥 LIMPEZA CRÍTICA: Libera página anterior e limpa histórico
                CleanupAndNavigate(newPage);
            }
            else
            {
                ShowInfo("EM BREVE", "Página em desenvolvimento.");
            }
        }

        /// <summary>
        /// Navegação simples - apenas navega para a nova página.
        /// </summary>
        private void CleanupAndNavigate(Page newPage)
        {
            try
            {
                // 🔥 CORREÇÃO: Chamar Cleanup() da página anterior para liberar recursos
                if (MainFrame.Content is Page previousPage)
                {
                    // Usa reflection para chamar o método Cleanup() se existir
                    var cleanupMethod = previousPage.GetType().GetMethod("Cleanup", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (cleanupMethod != null)
                    {
                        try
                        {
                            cleanupMethod.Invoke(previousPage, null);
                        }
                        catch { }
                    }
                }

                // 🔥 CORREÇÃO: Cria nova instância sempre (já é o padrão do Navigate)
                // O WPF não reutiliza instâncias de Page por padrão
                MainFrame.Navigate(newPage);

                // 🔥 CORREÇÃO: Força GC para liberar memória imediatamente
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Remove todas as entradas do histórico
                        while (MainFrame.NavigationService.CanGoBack)
                            MainFrame.NavigationService.RemoveBackEntry();

                        // 🔥 Força GC para liberar memória das páginas anteriores
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                Logger.Log($"⚠️ Erro na navegação: {ex.Message}");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Minimiza para a bandeja em vez de fechar
            Hide();
            _trayService?.ShowMinimizedNotification();
        }
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        #endregion

        #region EVENTOS DO HEADER (Console e Notificações)

        private void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            if (NotifPanel != null) NotifPanel.Toggle();
        }

        private void BtnConsole_Click(object sender, RoutedEventArgs e)
        {
            // Alterna a visibilidade do console
            if (GlobalConsolePanel != null)
            {
                GlobalConsolePanel.Visibility = GlobalConsolePanel.Visibility == Visibility.Collapsed 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
        }

        // ⚙️ CONFIGURAÇÕES: Abre página de configurações
        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Logger.Log("⚙️ Abrindo configurações...");
                CleanupAndNavigate(new SettingsPage());
            }
            catch (Exception ex)
            {
                Logger.LogError("BtnSettings_Click", $"Erro: {ex.Message}");
                ShowError("❌ Erro", "Não foi possível abrir configurações");
            }
        }

        // � MODO DESENVOLVEDOR: Atualiza visibilidade do menu de debug
        public void UpdateDebugMenuVisibility(bool isDeveloperMode)
        {
            try
            {
                // Mostrar/esconder botão de diagnóstico
                if (BtnDiagnostic != null)
                {
                    BtnDiagnostic.Visibility = isDeveloperMode ? Visibility.Visible : Visibility.Collapsed;
                }

                // Mostrar/esconder console global
                if (GlobalConsolePanel != null)
                {
                    GlobalConsolePanel.Visibility = isDeveloperMode ? Visibility.Visible : Visibility.Collapsed;
                }

                // Logger.Log($"🐛 Modo desenvolvedor: {(isDeveloperMode ? "ATIVADO" : "DESATIVADO")}");
            }
            catch (Exception ex)
            {
                Logger.LogError("UpdateDebugMenuVisibility", $"Erro: {ex.Message}");
            }
        }

        // �🔥 LIMPEZA MANUAL: Botão para forçar limpeza de memory leaks
        private async void BtnCleanMemory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Log("🧹 Iniciando limpeza manual de memory leaks...");
                
                var result = await AggressiveMemoryCleaner.PerformAggressiveCleanup();
                
                var message = $"""
                    ✅ Limpeza Concluída!
                    
                    Memória antes: {result.MemoryBefore / 1024 / 1024:F1} MB
                    Memória depois: {result.MemoryAfter / 1024 / 1024:F1} MB
                    Liberado: {result.Freed / 1024 / 1024:F1} MB
                    
                    Total acumulado liberado: {AggressiveMemoryCleaner.TotalMemoryFreed / 1024 / 1024:F1} MB
                    Limpezas realizadas: {AggressiveMemoryCleaner.CleanupCount}
                    """;
                
                Logger.Log(message);
                
                // Mostra notificação de confirmação
                ShowSuccess("🧹 Limpeza Concluída", $"Liberados {result.Freed / 1024 / 1024:F1} MB");
            }
            catch (Exception ex)
            {
                Logger.LogError("BtnCleanMemory", $"Erro na limpeza: {ex.Message}");
                ShowError("❌ Erro", "Falha na limpeza de memória");
            }
        }

        // --- ATUALIZAÇÃO: Botão de Atualizações ---
        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔥 ANIMAÇÃO: Gira o ícone de refresh (simplificado)
                if (sender is Button btn)
                {
                    // Animação simples sem complexidade
                    var storyboard = new Storyboard();
                    var animation = new DoubleAnimation
                    {
                        From = 0,
                        To = 360,
                        Duration = TimeSpan.FromSeconds(1)
                    };
                    
                    Storyboard.SetTarget(animation, btn);
                    Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.(RotateTransform.Angle)"));
                    
                    btn.RenderTransform = new RotateTransform(18);
                    storyboard.Children.Add(animation);
                    storyboard.Begin();
                }

                // 🔥 ABRE DIRETO A PÁGINA DE ATUALIZAÇÕES
                KitLugia.Core.Logger.Log("🔄 Abrindo página de atualizações...");
                NavigateToPage("🔄");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ Erro ao abrir atualizações: {ex.Message}");
                MessageBox.Show(
                    $"Erro ao abrir página de atualizações:\n{ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        #endregion

        #region CONSOLE E NOTIFICAÇÕES (Lógica)

        private void UpdateNotificationBadge()
        {
            Dispatcher.Invoke(() =>
            {
                int count = NotificationHistoryManager.History.Count;
                if (BtnNotifications.Template.FindName("Badge", BtnNotifications) is Border badge &&
                    BtnNotifications.Template.FindName("TxtBadgeCount", BtnNotifications) is TextBlock txtCount)
                {
                    if (count > 0)
                    {
                        badge.Visibility = Visibility.Visible;
                        txtCount.Text = count > 99 ? "99+" : count.ToString();
                    }
                    else
                    {
                        badge.Visibility = Visibility.Collapsed;
                    }
                }
            });
        }
        #endregion

        #region OVERLAYS E TOASTS (Helpers)

        public async Task<bool> ShowConfirmationDialog(string message)
        {
            TxtConfirmMessage.Text = message;
            OverlayContainer.Visibility = Visibility.Visible;
            OverlayConfirm.Visibility = Visibility.Visible;
            _confirmCompletionSource = new TaskCompletionSource<bool>();
            return await _confirmCompletionSource.Task;
        }

        private void BtnConfirmYes_Click(object sender, RoutedEventArgs e)
        {
            OverlayConfirm.Visibility = Visibility.Collapsed;
            OverlayContainer.Visibility = Visibility.Collapsed;
            _confirmCompletionSource?.SetResult(true);
        }

        private void BtnConfirmNo_Click(object sender, RoutedEventArgs e)
        {
            OverlayConfirm.Visibility = Visibility.Collapsed;
            OverlayContainer.Visibility = Visibility.Collapsed;
            _confirmCompletionSource?.SetResult(false);
        }

        public void ShowSuccess(string title, string message) => ShowNotification(title, message, NotificationType.Success);
        public void ShowError(string title, string message) => ShowNotification(title, message, NotificationType.Error);
        public void ShowInfo(string title, string message) => ShowNotification(title, message, NotificationType.Info);

        private int GetPriorityValue(NotificationType type)
        {
            return type switch { NotificationType.Error => 3, NotificationType.Info => 2, NotificationType.Success => 1, _ => 0 };
        }

        private void ShowNotification(string title, string message, NotificationType type)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConsoleManager.WriteLine($"NOTIFICAÇÃO: [{title}] {message}");

                if (title != "AGUARDE" && title != "PROCESSANDO")
                    NotificationHistoryManager.Add(title, message, type);

                string searchId = (type == NotificationType.Info) ? "GENERIC_INFO" : $"{type}|{title}|{message}";

                if (_activeToasts.TryGetValue(searchId, out LugiaToast? existingToast))
                {
                    if (type == NotificationType.Info) existingToast.UpdateMessage(message);
                    else existingToast.IncrementCounter();
                    return;
                }

                var toast = new LugiaToast();
                toast.SetContent(title, message, type);
                _activeToasts[searchId] = toast;

                if (ToastContainer.Children.Count >= MaxVisibleToasts)
                {
                    if (ToastContainer.Children[ToastContainer.Children.Count - 1] is LugiaToast last) last.Dismiss();
                }

                ToastContainer.Children.Insert(0, toast);

                void OnToastDismissed(LugiaToast t)
                {
                    t.Dismissed -= OnToastDismissed;
                    if (_activeToasts.ContainsKey(searchId)) _activeToasts.Remove(searchId);
                    ToastContainer.Children.Remove(t);
                }
                toast.Dismissed += OnToastDismissed;
            });
        }
        #endregion

        #region Loading Overlay Methods

        /// <summary>
        /// Mostra o overlay de carregamento com mensagem personalizada
        /// </summary>
        public void ShowLoading(string message = "Processando...")
        {
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Message = message;
                LoadingOverlay.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// Esconde o overlay de carregamento
        /// </summary>
        public void HideLoading()
        {
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Executa uma operação pesada em background mostrando loading
        /// </summary>
        public async Task<T> ExecuteWithLoadingAsync<T>(string message, Func<T> operation)
        {
            ShowLoading(message);
            try
            {
                return await Task.Run(() => operation());
            }
            finally
            {
                HideLoading();
            }
        }

        /// <summary>
        /// Executa uma operação pesada em background mostrando loading (void)
        /// </summary>
        public async Task ExecuteWithLoadingAsync(string message, Action operation)
        {
            ShowLoading(message);
            try
            {
                await Task.Run(() => operation());
            }
            finally
            {
                HideLoading();
            }
        }

        /// <summary>
        /// Cleanup de recursos e handlers para evitar memory leaks
        /// </summary>
        private void Cleanup()
        {
            try
            {
                // 🔥 CORREÇÃO: Cancelar todas as tasks de background
                if (_backgroundTasksCts != null)
                {
                    _backgroundTasksCts.Cancel();
                    _backgroundTasksCts.Dispose();
                    _backgroundTasksCts = null;
                }

                // 🔥 CORREÇÃO: Parar AggressiveMemoryCleaner
                AggressiveMemoryCleaner.StopIntelligentMonitoring();

                // 🔥 CORREÇÃO: Remover handlers de eventos estáticos para evitar memory leak
                if (_logHandler != null)
                {
                    KitLugia.Core.Logger.OnLogReceived -= _logHandler;
                    _logHandler = null;
                }
                if (_notificationCountHandler != null)
                {
                    NotificationHistoryManager.OnCountChanged -= _notificationCountHandler;
                    _notificationCountHandler = null;
                }

                // Limpa o timer de debounce
                _searchDebounceTimer?.Stop();

                // 🔥 CORREÇÃO: Parar healthCheckTimer
                _healthCheckTimer?.Stop();

                // Limpa o serviço de tray
                _trayService?.Dispose();

                // Limpa toasts ativos
                _activeToasts.Clear();

                // 🔥 CORREÇÃO: Dispose do EventWaitHandle
                _showWindowEvent?.Dispose();
                _showWindowEvent = null;
            }
            catch (Exception ex)
            {
                Logger.LogError("MainWindow.Cleanup", ex.Message);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Verificar se deve minimizar para tray em vez de fechar
            if (TrayService != null && TrayService.CloseToTray && TrayService.IsTrayEnabled)
            {
                e.Cancel = true; // Cancelar o fechamento
                this.Hide(); // Minimizar para tray
                KitLugia.Core.Logger.Log("🔔 Janela minimizada para Tray (Close to Tray ativado)");
            }
            else
            {
                KitLugia.Core.Logger.Log("👋 Fechando aplicação (Close to Tray desativado ou Tray desativado)");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            Cleanup();
            base.OnClosed(e);
        }

        #endregion
    }
}