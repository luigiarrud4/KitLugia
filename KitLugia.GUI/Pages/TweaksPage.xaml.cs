using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using KitLugia.Core;

// Resolve ambiguidade da Cor
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class TweaksPage : Page
    {
        private bool _isLoading = true;
        private readonly SolidColorBrush _colorActive = new SolidColorBrush(Color.FromRgb(108, 203, 95));
        private readonly SolidColorBrush _colorDefault = new SolidColorBrush(Color.FromRgb(150, 150, 150));

        public TweaksPage()
        {
            InitializeComponent();
            LoadCurrentStatus();
        }

        private async void LoadCurrentStatus()
        {
            await Task.Run(() =>
            {
                bool gamesOptimized = SystemTweaks.IsGamingOptimized();
                bool mpoDisabled = SystemTweaks.IsMpoDisabled();
                bool vbsEnabledInSystem = SystemTweaks.IsVbsEnabled();
                bool bingDisabled = SystemTweaks.IsBingDisabled();

                Dispatcher.Invoke(() =>
                {
                    _isLoading = true; // Pausa eventos durante carga

                    ChkGameMode.IsChecked = gamesOptimized;
                    UpdateLabel(StatusGame, gamesOptimized, "Prioridade Alta", "Padrão");

                    ChkMPO.IsChecked = mpoDisabled;
                    UpdateLabel(StatusMPO, mpoDisabled, "Corrigido (OFF)", "Padrão (ON)");

                    ChkVBS.IsChecked = !vbsEnabledInSystem;
                    StatusVBS.Text = vbsEnabledInSystem ? "Padrão (Seguro)" : "⚡ Max FPS";
                    StatusVBS.Foreground = vbsEnabledInSystem ? _colorDefault : _colorActive;

                    ChkBing.IsChecked = bingDisabled;
                    UpdateLabel(StatusBing, bingDisabled, "Limpo", "Padrão");

                    _isLoading = false;
                });
            });
        }

        private void UpdateLabel(TextBlock label, bool isActive, string textActive, string textInactive)
        {
            label.Text = isActive ? textActive : textInactive;
            label.Foreground = isActive ? _colorActive : _colorDefault;
        }

        // --- CLIQUES ---

        private void ChkGameMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            if (ChkGameMode.IsChecked == true)
            {
                SystemTweaks.ApplyGamingOptimizations();
                UpdateLabel(StatusGame, true, "Prioridade Alta", "Padrão");
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowSuccess("MODO JOGO", "Prioridade de jogo definida para Alta.");
            }
            else
            {
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowInfo("AÇÃO LIMITADA", "Para reverter completamente este tweak de registro, use o menu de Backup.");

                ChkGameMode.IsChecked = true;
                UpdateLabel(StatusGame, true, "Prioridade Alta", "Padrão");
            }
        }

        private void ChkMPO_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var result = SystemTweaks.ToggleMpo();

            bool nowActive = ChkMPO.IsChecked == true;
            UpdateLabel(StatusMPO, nowActive, "Corrigido (OFF)", "Padrão (ON)");

            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", $"{result.Message}\nO Windows precisa ser reiniciado para aplicar.");
        }

        private void ChkVBS_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var result = SystemTweaks.ToggleVbs();

            bool isOptimizationActive = ChkVBS.IsChecked == true;
            if (isOptimizationActive)
            {
                StatusVBS.Text = "⚡ Max FPS (Ao Reiniciar)";
                StatusVBS.Foreground = _colorActive;
            }
            else
            {
                StatusVBS.Text = "Padrão (Seguro)";
                StatusVBS.Foreground = _colorDefault;
            }

            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", result.Message + "\nO Windows requer REINICIALIZAÇÃO para mudar este recurso de segurança.");
        }

        private void ChkBing_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (ChkBing.IsChecked == true)
            {
                SystemTweaks.ApplyBingTweak();
                UpdateLabel(StatusBing, true, "Limpo", "Padrão");
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowSuccess("PESQUISA OTIMIZADA", "Sugestões do Bing na busca foram desativadas.");
            }
            else
            {
                SystemTweaks.RevertRegistryValue(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions");
                UpdateLabel(StatusBing, false, "Padrão", "Limpo");
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowInfo("PESQUISA RESTAURADA", "Sugestões do Bing na busca foram reativadas.");
            }
        }
    }
}
