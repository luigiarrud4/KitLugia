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

namespace KitLugia.GUI
{
    public partial class MainWindow : Window
    {
        private const int MaxVisibleToasts = 6;
        private Dictionary<string, LugiaToast> _activeToasts = new Dictionary<string, LugiaToast>();
        private TaskCompletionSource<bool>? _confirmCompletionSource;

        // Tray Icon RAM Monitor
        private TrayIconService? _trayService;
        public TrayIconService? TrayService => _trayService;

        // Timer para o Debounce da pesquisa
        private DispatcherTimer _searchDebounceTimer;

        // Single-instance show window signaling
        private System.Threading.EventWaitHandle? _showWindowEvent;
        private System.Threading.Thread? _showWindowMonitor;

        public MainWindow()
        {
            InitializeComponent();

            // Inicializa a Engine de Busca
            SearchEngine.Initialize();

            // Configura o timer de pesquisa (300ms)
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += SearchDebounce_Tick;

            // Inicia na Dashboard
            MainFrame.Navigate(new DashboardPage());
            if (BtnDashboard != null) BtnDashboard.IsChecked = true;

            // Conecta o Logger do Core ao Console da GUI
            KitLugia.Core.Logger.OnLogReceived += (msg) => ConsoleManager.WriteLine(msg);

            // Conecta o contador de notificações
            NotificationHistoryManager.OnCountChanged += UpdateNotificationBadge;

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

            // --- AUTO-START: Garante que o app inicie com o Windows se o Tray estiver ativo ---
            if (TrayIconService.IsTrayEnabledStatic())
            {
                TrayIconService.SetAutoStart(true);
                
                // 🔥 Verificação cirúrgica de instância única
                var currentPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                Logger.Log($"KitLugia iniciado: {currentPath}");
                Logger.Log($"Tray ativo: {TrayIconService.IsTrayEnabledStatic()}");
                
                // 🔥 CHECK 5: Verificação de saúde do Tray Icon após 3 segundos
                var healthCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                healthCheckTimer.Tick += (s, e) =>
                {
                    healthCheckTimer.Stop();
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
                healthCheckTimer.Start();
            }

            // 🔥 AUTO-UPDATER: Inicia verificação automática em background
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10)); // Espera 10s para iniciar
                await GitHubUpdater.StartAutoUpdateCheck();
            });

            // --- NAMED EVENT: Permite que uma segunda instância sinalize para mostrar a janela ---
            _showWindowEvent = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.AutoReset, "KitLugia_ShowWindow");
            _showWindowMonitor = new System.Threading.Thread(() =>
            {
                while (true)
                {
                    _showWindowEvent.WaitOne();
                    Dispatcher.Invoke(() =>
                    {
                        Show();
                        WindowState = WindowState.Normal;
                        Activate();
                        Focus();
                    });
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
                MainFrame.Navigate(new GlobalSearchPage(query));
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
            else if (MainFrame.Content is ScreenPage) BtnScreen.IsChecked = true;
            else if (MainFrame.Content is BloatwarePage) BtnApps.IsChecked = true;
            else if (MainFrame.Content is CleanupPage) BtnStorage.IsChecked = true;
            else if (MainFrame.Content is NetworkPage) BtnNetwork.IsChecked = true;
            else if (MainFrame.Content is GamesPage) BtnGames.IsChecked = true;
            else if (MainFrame.Content is ServicesPage) BtnServices.IsChecked = true;
            else if (MainFrame.Content is RepairsPage) BtnRepairs.IsChecked = true;
            else if (MainFrame.Content is DriversPage) BtnDrivers.IsChecked = true;
            else if (MainFrame.Content is PartitionsPage) BtnPartitions.IsChecked = true;
            else if (MainFrame.Content is TraySettingsPage) { if (BtnTray != null) BtnTray.IsChecked = true; }
        }

        private void UncheckAllNavButtons()
        {
            if (BtnDashboard != null) BtnDashboard.IsChecked = false;
            if (BtnTweaks != null) BtnTweaks.IsChecked = false;
            if (BtnScreen != null) BtnScreen.IsChecked = false;
            if (BtnApps != null) BtnApps.IsChecked = false;
            if (BtnStorage != null) BtnStorage.IsChecked = false;
            if (BtnNetwork != null) BtnNetwork.IsChecked = false;
            if (BtnGames != null) BtnGames.IsChecked = false;
            if (BtnServices != null) BtnServices.IsChecked = false;
            if (BtnRepairs != null) BtnRepairs.IsChecked = false;
            if (BtnDrivers != null) BtnDrivers.IsChecked = false;
            if (BtnPartitions != null) BtnPartitions.IsChecked = false;

            // CORREÇÃO: Garante que o botão de segurança no topo também seja desmarcado
            if (BtnSecurity != null) BtnSecurity.IsChecked = false;
            if (BtnIntegrity != null) BtnIntegrity.IsChecked = false;
            if (BtnTray != null) BtnTray.IsChecked = false;
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
                    case "🖥️": if (BtnScreen != null) BtnScreen.IsChecked = true; break;
                    case "📱": if (BtnApps != null) BtnApps.IsChecked = true; break;
                    case "💿": if (BtnStorage != null) BtnStorage.IsChecked = true; break;
                    case "🌐": if (BtnNetwork != null) BtnNetwork.IsChecked = true; break;
                    case "🎮": if (BtnGames != null) BtnGames.IsChecked = true; break;
                    case "🛡️": if (BtnServices != null) BtnServices.IsChecked = true; break;
                    case "🔧": if (BtnRepairs != null) BtnRepairs.IsChecked = true; break;
                    case "💾": if (BtnDrivers != null) BtnDrivers.IsChecked = true; break;
                    case "💽": if (BtnPartitions != null) BtnPartitions.IsChecked = true; break;
                    case "🛡️Scan": if (BtnSecurity != null) BtnSecurity.IsChecked = true; break;
                case "🔐": if (BtnIntegrity != null) BtnIntegrity.IsChecked = true; break;
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
                "🚀" => new AdvancedToolsPage(),
                "🛡️" => new ServicesPage(tabIndex),
                "🔧" => new RepairsPage(),
                "💾" => new DriversPage(),
                "💽" => new PartitionsPage(),
                "🛡️Scan" => new IntegrityPage(),
                "🔐" => new SecurityPage(),
                "⚙️" => new TweaksPage(), // Otimização integrada em Tweaks
                "🔒" => new PrivacyPage(),
                "🔑" => new ActivationPage(),
                "🔔" => new TraySettingsPage(),
                "🔄" => new UpdatePage(), // 🔥 Página de atualizações
                _ => null
            };

            if (newPage != null)
            {
                MainFrame.Navigate(newPage);
            }
            else
            {
                ShowInfo("EM BREVE", "Página em desenvolvimento.");
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

        // --- ATUALIZAÇÃO: Botão de Atualizações ---
        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
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
    }
}