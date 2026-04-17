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
        private readonly SolidColorBrush _colorSlideActive = new SolidColorBrush(Color.FromRgb(255, 170, 0)); // Amarelo Escuro para SLIDE

        public TweaksPage()
        {
            InitializeComponent();
            _ = LoadCurrentStatus();
            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += TweaksPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            this.Unloaded -= TweaksPage_Unloaded;
        }

        private void TweaksPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private async Task LoadCurrentStatus()
        {
            await Task.Run(() =>
            {
                bool gamesOptimized = SystemTweaks.IsGamingOptimized();
                bool mpoDisabled = SystemTweaks.IsMpoDisabled();
                bool vbsEnabledInSystem = SystemTweaks.IsVbsEnabled();
                bool bingDisabled = SystemTweaks.IsBingDisabled();
                bool memoryUsageEnabled = SystemTweaks.IsMemoryUsageEnabled();
                bool timerOptimized = SystemTweaks.IsTimerResolutionOptimized();
                bool shutdownOptimized = SystemTweaks.IsFastShutdownEnabled();

                bool slideInput = SystemTweaks.IsInputLatencyOptimized();
                bool slideUsb = SystemTweaks.IsUsbPowerSavingDisabled();
                bool slideGaming = SystemTweaks.IsGamingLatencyOptimized();

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

                    ChkMemoryUsage.IsChecked = memoryUsageEnabled;
                    UpdateLabel(StatusMemoryUsage, memoryUsageEnabled, "Otimizado", "Padrão");

                    ChkTimer.IsChecked = timerOptimized;
                    UpdateLabel(StatusTimer, timerOptimized, "Latência Mínima", "Padrão");

                    ChkShutdown.IsChecked = shutdownOptimized;
                    UpdateLabel(StatusShutdown, shutdownOptimized, "⚡ Turbo Boot", "Padrão");

                    ChkSlideInput.IsChecked = slideInput;
                    UpdateSlideLabel(StatusSlideInput, slideInput, "Nível Máximo", "Padrão");

                    ChkSlideUsb.IsChecked = slideUsb;
                    UpdateSlideLabel(StatusSlideUsb, slideUsb, "Desativado", "Padrão");

                    ChkSlideGaming.IsChecked = slideGaming;
                    UpdateSlideLabel(StatusSlideGaming, slideGaming, "Extremo (GameDVR OFF)", "Padrão");

                    _isLoading = false;
                });
            });
        }

        private void UpdateLabel(TextBlock label, bool isActive, string textActive, string textInactive)
        {
            label.Text = isActive ? textActive : textInactive;
            label.Foreground = isActive ? _colorActive : _colorDefault;
        }

        private void UpdateSlideLabel(TextBlock label, bool isActive, string textActive, string textInactive)
        {
            label.Text = isActive ? textActive : textInactive;
            label.Foreground = isActive ? _colorSlideActive : _colorDefault;
        }

        // --- CLIQUES ---

        private async void ChkSlideInput_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            bool targetActive = ChkSlideInput.IsChecked == true;
            UpdateSlideLabel(StatusSlideInput, targetActive, "Aplicando...", "Revertendo...");

            await Task.Run(() =>
            {
                if (targetActive) SystemTweaks.OptimizeInputLatency();
                else SystemTweaks.RevertInputLatency();
            });

            UpdateSlideLabel(StatusSlideInput, targetActive, "Nível Máximo", "Padrão");
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", "As mudanças na latência de input exigem reiniciar o computador.");
        }

        private async void ChkSlideUsb_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            bool targetActive = ChkSlideUsb.IsChecked == true;
            UpdateSlideLabel(StatusSlideUsb, targetActive, "Aplicando...", "Revertendo...");

            await Task.Run(() =>
            {
                if (targetActive) SystemTweaks.DisableUsbPowerSaving();
                else SystemTweaks.RevertUsbPowerSaving();
            });

            UpdateSlideLabel(StatusSlideUsb, targetActive, "Desativado", "Padrão");
        }

        private async void ChkSlideGaming_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            bool targetActive = ChkSlideGaming.IsChecked == true;
            UpdateSlideLabel(StatusSlideGaming, targetActive, "Aplicando...", "Revertendo...");

            await Task.Run(() =>
            {
                if (targetActive) SystemTweaks.OptimizeGamingLatency();
                else SystemTweaks.RevertGamingLatency();
            });

            UpdateSlideLabel(StatusSlideGaming, targetActive, "Extremo (DWM/GameDVR OFF)", "Padrão");
            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", "As alterações estruturais do Thread e GameDVR exigem reiniciar o computador.");
        }

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

        private void ChkMemoryUsage_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var result = SystemTweaks.ToggleMemoryUsage();

            bool nowActive = ChkMemoryUsage.IsChecked == true;
            UpdateLabel(StatusMemoryUsage, nowActive, "Otimizado", "Padrão");

            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", $"{result.Message}\nO Windows precisa ser reiniciado para aplicar.");
        }

        private void ChkTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            var result = SystemTweaks.ToggleTimerResolution();

            bool nowActive = ChkTimer.IsChecked == true;
            UpdateLabel(StatusTimer, nowActive, "Latência Mínima", "Padrão");

            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", $"{result.Message}\nO Windows precisa ser reiniciado para aplicar as mudanças de Timer.");
        }

        private void ChkShutdown_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            SystemTweaks.ToggleFastShutdown();

            bool nowActive = ChkShutdown.IsChecked == true;
            UpdateLabel(StatusShutdown, nowActive, "⚡ Turbo Boot", "Padrão");

            // Update Tray if exists
            var tray = (Application.Current.MainWindow as MainWindow)?.TrayService;
            if (tray != null) tray.TurboShutdownEnabled = nowActive;

            if (Application.Current.MainWindow is MainWindow mw)
                mw.ShowInfo("REINÍCIO NECESSÁRIO", "Sistema de desligamento otimizado.\nRecomendado reiniciar para aplicar as mudanças de registro.");
        }
    }
}
