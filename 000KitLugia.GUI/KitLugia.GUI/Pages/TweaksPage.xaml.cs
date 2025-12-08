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

                    ChkVBS.IsChecked = !vbsEnabledInSystem; // Inverte lógica para o botão "Desativar"
                    if (vbsEnabledInSystem)
                    {
                        StatusVBS.Text = "Padrão (Seguro)";
                        StatusVBS.Foreground = _colorDefault;
                    }
                    else
                    {
                        StatusVBS.Text = "⚡ Max FPS";
                        StatusVBS.Foreground = _colorActive;
                    }

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

        // --- HELPER PARA MOSTRAR AVISO INTEGRADO ---
        private void ShowAlert(string message, string title)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.ShowNotification(message, title);
            }
        }

        // --- CLIQUES ---

        private void ChkGameMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            // Se o usuário marcou (quer ativar)
            if (ChkGameMode.IsChecked == true)
            {
                SystemTweaks.ApplyGamingOptimizations();
                UpdateLabel(StatusGame, true, "Prioridade Alta", "Padrão");
            }
            // Se o usuário desmarcou (quer desativar)
            else
            {
                // 1. Mostra o aviso que não dá pra reverter por aqui
                ShowAlert("Para reverter completamente este tweak de registro, use o menu de Backup.", "Ação Limitada");

                // 2. CORREÇÃO DO BUG: Força o botão a voltar para LIGADO (Azul) visualmente
                // Já que não revertemos no registro, o botão não pode ficar cinza.
                ChkGameMode.IsChecked = true;

                // Mantém o texto verde
                UpdateLabel(StatusGame, true, "Prioridade Alta", "Padrão");
            }
        }

        private void ChkMPO_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var result = SystemTweaks.ToggleMpo();

            bool nowActive = ChkMPO.IsChecked == true;
            UpdateLabel(StatusMPO, nowActive, "Corrigido (OFF)", "Padrão (ON)");

            ShowAlert($"{result.Message}\n\nO Windows precisa ser reiniciado para aplicar.", "Configuração de Vídeo");
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

            ShowAlert(result.Message + "\n\nO Windows requer REINICIALIZAÇÃO para mudar este recurso de segurança.", "Kernel do Windows");
        }

        private void ChkBing_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (ChkBing.IsChecked == true)
            {
                SystemTweaks.ApplyBingTweak();
                UpdateLabel(StatusBing, true, "Limpo", "");
            }
            else
            {
                SystemTweaks.RevertRegistryValue(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions");
                UpdateLabel(StatusBing, false, "", "Padrão");
            }
        }
    }
}