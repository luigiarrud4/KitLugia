using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.GUI.Controls;
using KitLugia.GUI.Pages;
using RadioButton = System.Windows.Controls.RadioButton;
using Application = System.Windows.Application;

namespace KitLugia.GUI
{
    public class SearchAction
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string PageTag { get; set; } = "";
        public string Keywords { get; set; } = "";
    }

    public partial class MainWindow : Window
    {
        private const int MaxVisibleToasts = 6;
        private Dictionary<string, LugiaToast> _activeToasts = new Dictionary<string, LugiaToast>();
        private List<SearchAction> _allSearchActions = new List<SearchAction>();

        public MainWindow()
        {
            InitializeComponent();
            InitializeSearchIndex();
            MainFrame.Navigate(new DashboardPage());
        }

        #region Search Logic
        private void InitializeSearchIndex()
        {
            _allSearchActions = new List<SearchAction>
            {
                new SearchAction { Title = "Dashboard", PageTag = "🏠", Description = "Visão geral." },
                new SearchAction { Title = "Modo Jogo", PageTag = "⚡", Description = "Prioridade GPU.", Keywords = "game fps" },
                new SearchAction { Title = "MPO Fix", PageTag = "⚡", Description = "Correção flicker.", Keywords = "nvidia amd" },
                new SearchAction { Title = "Central AIO", PageTag = "🔧", Description = "Soluções de reparo.", Keywords = "fix repair" },
                new SearchAction { Title = "Windows Update", PageTag = "🔧", Description = "Reparar erros update.", Keywords = "update erro" },
                new SearchAction { Title = "Explorer Fix", PageTag = "🔧", Description = "Reiniciar Explorer.", Keywords = "taskbar" },
                new SearchAction { Title = "Bloatware", PageTag = "📱", Description = "Remover apps.", Keywords = "xbox" },
                new SearchAction { Title = "Limpeza", PageTag = "💿", Description = "Lixo e Temp.", Keywords = "clean" },
                new SearchAction { Title = "DNS", PageTag = "🌐", Description = "Mudar DNS.", Keywords = "ping net" },
                new SearchAction { Title = "Serviços", PageTag = "🛡️", Description = "Otimizar serviços.", Keywords = "services" }
            };
        }

        private void TxtGlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtGlobalSearch.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query)) { SearchPopup.IsOpen = false; return; }

            var results = _allSearchActions.Where(x => x.Title.ToLower().Contains(query) || x.Keywords.ToLower().Contains(query)).Take(8).ToList();
            LstSearchResults.ItemsSource = results;
            SearchPopup.IsOpen = results.Any();
        }

        private void LstSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSearchResults.SelectedItem is SearchAction action) { NavigateToPage(action.PageTag); TxtGlobalSearch.Text = ""; SearchPopup.IsOpen = false; }
        }
        #endregion

        #region Navigation
        private void NavButton_Click(object sender, RoutedEventArgs e) { if (sender is RadioButton btn) NavigateToPage(btn.Tag?.ToString()); }

        public void NavigateToPage(string? pageTag, object? senderButton = null)
        {
            if (pageTag == null) return;
            if (senderButton == null)
            {
                BtnDashboard.IsChecked = false; BtnTweaks.IsChecked = false; BtnApps.IsChecked = false;
                BtnStorage.IsChecked = false; BtnNetwork.IsChecked = false; BtnGames.IsChecked = false;
                BtnTools.IsChecked = false; BtnServices.IsChecked = false; BtnRepairs.IsChecked = false;

                switch (pageTag)
                {
                    case "🏠": BtnDashboard.IsChecked = true; break;
                    case "⚡": BtnTweaks.IsChecked = true; break;
                    case "📱": BtnApps.IsChecked = true; break;
                    case "💿": BtnStorage.IsChecked = true; break;
                    case "🌐": BtnNetwork.IsChecked = true; break;
                    case "🎮": BtnGames.IsChecked = true; break;
                    case "🛠️": BtnTools.IsChecked = true; break;
                    case "🛡️": BtnServices.IsChecked = true; break;
                    case "🔧": BtnRepairs.IsChecked = true; break;
                }
            }

            switch (pageTag)
            {
                case "🏠": MainFrame.Navigate(new DashboardPage()); break;
                case "⚡": MainFrame.Navigate(new TweaksPage()); break;
                case "📱": MainFrame.Navigate(new BloatwarePage()); break;
                case "💿": MainFrame.Navigate(new CleanupPage()); break;
                case "🌐": MainFrame.Navigate(new NetworkPage()); break;
                case "🎮": MainFrame.Navigate(new GamesPage()); break;
                case "🛠️": MainFrame.Navigate(new ToolsPage()); break;
                case "🛡️": MainFrame.Navigate(new ServicesPage()); break;
                case "🔧": MainFrame.Navigate(new RepairsPage()); break;
                default: ShowInfo("EM BREVE", "Página em desenvolvimento."); break;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = (WindowState == WindowState.Normal) ? WindowState.Maximized : WindowState.Normal;
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        #endregion

        #region Notifications Logic (Corrigida)

        public async Task<bool> ShowConfirmationDialog(string message)
        {
            var overlay = new LugiaConfirmationOverlay(message);
            OverlayContainer.Children.Clear();
            OverlayContainer.Children.Add(overlay);
            OverlayContainer.Visibility = Visibility.Visible;
            bool result = await overlay.WaitForUserSelection();
            OverlayContainer.Visibility = Visibility.Collapsed;
            OverlayContainer.Children.Clear();
            return result;
        }

        public void ShowSuccess(string title, string message) => ShowNotification(title, message, NotificationType.Success);
        public void ShowError(string title, string message) => ShowNotification(title, message, NotificationType.Error);
        public void ShowInfo(string title, string message) => ShowNotification(title, message, NotificationType.Info);

        // Helper para definir valor de importância (Maior número = Mais ao topo)
        private int GetPriorityValue(NotificationType type)
        {
            return type switch
            {
                NotificationType.Error => 3,   // Topo absoluto
                NotificationType.Info => 2,    // Meio (Amarelo)
                NotificationType.Success => 1, // Base (Verde)
                _ => 0
            };
        }

        private void ShowNotification(string title, string message, NotificationType type)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                string searchId = (type == NotificationType.Info) ? "GENERIC_INFO" : $"{type}|{title}|{message}";

                // Se já existe, atualiza mas mantendo posição lógica
                if (_activeToasts.TryGetValue(searchId, out LugiaToast? existingToast))
                {
                    if (type == NotificationType.Info) existingToast.UpdateMessage(message);
                    else existingToast.IncrementCounter();
                    return;
                }

                // Cria novo Toast
                var toast = new LugiaToast();
                toast.SetContent(title, message, type);
                _activeToasts[searchId] = toast;

                // Limita quantidade (remove o mais antigo de menor prioridade visual, que está no final da lista)
                if (ToastContainer.Children.Count >= MaxVisibleToasts)
                {
                    if (ToastContainer.Children[ToastContainer.Children.Count - 1] is LugiaToast last) last.Dismiss();
                }

                // --- ALGORITMO DE INSERÇÃO ORDENADA (Top = Index 0) ---
                // Queremos: Index 0 -> Vermelho, Index X -> Amarelo, Index Y -> Verde.
                int newPriority = GetPriorityValue(type);
                int insertIndex = 0;
                bool inserted = false;

                foreach (UIElement child in ToastContainer.Children)
                {
                    if (child is LugiaToast childToast)
                    {
                        int childPriority = GetPriorityValue(childToast.ToastType);

                        // Se a nova notificação for mais importante que a atual da lista, insere aqui.
                        // (Isso empurra a atual e as próximas para baixo).
                        // Se for igual, continuamos descendo para que a NOVA fique ACIMA das velhas de mesma cor (Stack normal).
                        if (newPriority > childPriority)
                        {
                            ToastContainer.Children.Insert(insertIndex, toast);
                            inserted = true;
                            break;
                        }
                    }
                    insertIndex++;
                }

                // Se não inseriu ainda (significa que é a menor prioridade ou a lista estava vazia), coloca no fim.
                if (!inserted)
                {
                    ToastContainer.Children.Add(toast);
                }

                // Callback de Limpeza de Memória
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