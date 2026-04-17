using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using Application = System.Windows.Application;

#pragma warning disable CS4014 // Chamadas async não aguardadas são intencionais para operações em background

namespace KitLugia.GUI.Pages
{
    public partial class GamesPage : Page
    {
        public GamesPage()
        {
            InitializeComponent();
            LoadStats();
            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += GamesPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            this.Unloaded -= GamesPage_Unloaded;
        }

        private void GamesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private async Task LoadStats()
        {
            double totalRam = SystemUtils.GetTotalSystemRamGB();
            TxtTotalRam.Text = $"{totalRam:F1} GB";

            await Task.Run(() =>
            {
                bool gameMode = SystemTweaks.IsGamingOptimized();
                bool dvrEnabled = SystemTweaks.IsGameDvrEnabled();
                Dispatcher.Invoke(() =>
                {
                    ChkGameMode.IsChecked = gameMode;
                    ChkDvr.IsChecked = !dvrEnabled;
                });
            });
        }

        // --- RAM BOOSTER ---
        private async void BtnBoostRam_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;
            mw.ShowInfo("AGUARDE", "Otimizando Memória RAM...");

            var result = await Task.Run(() => SystemTweaks.OptimizeMemory());
            mw.ShowSuccess("RAM BOOSTER", $"Memória limpa com sucesso!\n{result.Message}");
        }

        // --- TWEAKS ---
        private void ChkGameMode_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;
            if (ChkGameMode.IsChecked == true)
            {
                SystemTweaks.ApplyGamingOptimizations();
                mw.ShowSuccess("MODO JOGO", "Prioridade de Jogo definida para ALTA.");
            }
            else
            {
                mw.ShowInfo("AVISO", "Use o backup do registro para reverter completamente esta otimização.");
            }
        }

        private void ChkDvr_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            bool turnOff = ChkDvr.IsChecked == true;
            SystemTweaks.ToggleGameDvr(!turnOff);

            string status = turnOff ? "DESATIVADO (Otimizado)" : "ATIVADO (Padrão)";
            mw.ShowSuccess("XBOX DVR", $"Game DVR do Xbox foi {status}.\nReinicie o computador para garantir o efeito.");
        }

        // --- ATALHOS ---
        private async void BtnClearShaders_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;
            mw.ShowInfo("AGUARDE", "Limpando caches de shaders...");

            var res = await Task.Run(() => Toolbox.CleanShaderCaches());
            mw.ShowSuccess("SUCESSO", $"Caches de shaders limpos.\nLiberado: {res.TotalBytesFreed / 1024 / 1024} MB");
        }

        private void BtnHighPerf_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            Toolbox.SetActivePowerPlan("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            mw.ShowSuccess("ENERGIA", "Plano de energia 'Alto Desempenho' foi ativado.");
        }
    }
}