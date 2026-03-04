using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;

namespace KitLugia.GUI.Pages
{
    public partial class CleanupPage : Page
    {
        private bool _isCleaning = false;

        public CleanupPage()
        {
            InitializeComponent();
        }

        private void AddLog(string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.AppendText($"\n[{time}] {message}");
            LogScroller.ScrollToBottom();
        }

        private async void BtnCleanTemp_Click(object sender, RoutedEventArgs e)
        {
            if (_isCleaning) return;
            _isCleaning = true;

            TxtLog.Text = "[Iniciando] Limpeza de Temporários...";

            var result = await Task.Run(() => Toolbox.CleanTemporaryFiles());

            foreach (var line in result.Log) AddLog(line);

            AddLog($"CONCLUÍDO. Liberado: {result.TotalBytesFreed / 1024 / 1024:N2} MB.");
            _isCleaning = false;
        }

        private async void BtnCleanUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (_isCleaning) return;
            _isCleaning = true;
            TxtLog.Text = "[Iniciando] Limpeza de Windows Update...";

            var result = await Task.Run(() => Toolbox.CleanWindowsUpdateCache());
            foreach (var line in result.Log) AddLog(line);

            AddLog($"CONCLUÍDO. Liberado: {result.TotalBytesFreed / 1024 / 1024:N2} MB.");
            _isCleaning = false;
        }

        private async void BtnCleanShaders_Click(object sender, RoutedEventArgs e)
        {
            if (_isCleaning) return;
            _isCleaning = true;
            TxtLog.Text = "[Iniciando] Limpeza de Cache GPU...";

            var result = await Task.Run(() => Toolbox.CleanShaderCaches());
            foreach (var line in result.Log) AddLog(line);

            AddLog($"CONCLUÍDO. Liberado: {result.TotalBytesFreed / 1024 / 1024:N2} MB.");
            _isCleaning = false;
        }

        private async void BtnFullClean_Click(object sender, RoutedEventArgs e)
        {
            if (_isCleaning) return;
            _isCleaning = true;
            TxtLog.Text = "=== INICIANDO LIMPEZA COMPLETA ===";

            var result = await Task.Run(() => Toolbox.RunFullCleanup());
            foreach (var line in result.Log) AddLog(line);

            AddLog("==================================");
            AddLog($"TOTAL LIBERADO: {result.TotalBytesFreed / 1024 / 1024:N2} MB");
            _isCleaning = false;
        }

        private void BtnCompactOS_Click(object sender, RoutedEventArgs e)
        {
            // Abre nova janela porque é um processo muito longo e externo
            _ = Task.Run(() => Toolbox.CompactOS());
            AddLog("Iniciado processo de CompactOS em janela externa.");
        }
    }
}