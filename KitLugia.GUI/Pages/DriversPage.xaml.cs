using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KitLugia.Core;
// --- RESOLUÇÃO DE CONFLITOS DE NAMESPACE ---
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Application = System.Windows.Application;
using WinForms = System.Windows.Forms; // Para diálogos de pasta
using Color = System.Windows.Media.Color;

#pragma warning disable CS4014 // Chamadas async não aguardadas são intencionais para operações em background

namespace KitLugia.GUI.Pages
{
    public partial class DriversPage : Page
    {
        private List<DriverItem> _allDrivers = new();
        private CancellationTokenSource? _cts;

        public DriversPage()
        {
            InitializeComponent();
            _cts = new CancellationTokenSource();
            LoadDrivers();
            CheckVerifierStatus(); // Inicia a checagem da aba Diagnóstico
            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += DriversPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            // 🔥 Cancela todas as tasks em background
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            // 🔥 Limpa todas as listas e bindings
            _allDrivers?.Clear();
            _allDrivers = null!;

            if (GridDrivers != null)
            {
                GridDrivers.ItemsSource = null;
                GridDrivers.Items.Clear();
            }

            this.Unloaded -= DriversPage_Unloaded;
        }

        private void DriversPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        // =========================================================
        // ABA 1: LISTA DE DRIVERS (GERENCIAMENTO)
        // =========================================================
        #region Drivers List Logic

        private async Task LoadDrivers()
        {
            SetLoading(true, "Analisando Hardware...");

            bool showMicrosoft = ChkShowMicrosoft.IsChecked == true;

            // Carrega usando o novo método nativo Async
            _allDrivers = await DriverManager.GetSystemDriversAsync(showMicrosoft);

            FilterAndRefresh();
            SetLoading(false);
        }

        private void FilterAndRefresh()
        {
            string query = TxtFilter.Text.ToLower().Trim();
            var filtered = _allDrivers;

            if (!string.IsNullOrEmpty(query))
            {
                filtered = _allDrivers.Where(d =>
                    d.DeviceName.ToLower().Contains(query) ||
                    d.Provider.ToLower().Contains(query) ||
                    d.InfName.ToLower().Contains(query)
                ).ToList();
            }

            GridDrivers.ItemsSource = filtered;
            TxtCount.Text = $"{filtered.Count} Drivers";
            TxtStatus.Text = "Pronto.";
        }

        private void SetLoading(bool isLoading, string msg = "Processando...")
        {
            if (LoadingOverlay != null)
            {
                if (TxtLoadingMsg != null) TxtLoadingMsg.Text = msg;
                LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // --- EVENTOS DE UI ---

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e) => FilterAndRefresh();

        private void ChkShowMicrosoft_Checked(object sender, RoutedEventArgs e) => LoadDrivers();

        // --- AUTOMAÇÃO (SCAN E ATUALIZAÇÃO) ---

        private async void BtnAutoScan_Click(object sender, RoutedEventArgs e)
        {
            if (_allDrivers.Count == 0) return;

            SetLoading(true, "Verificando versões...");
            TxtStatus.Text = "Comparando datas...";

            int outdatedCount = 0;

            await Task.Run(async () =>
            {
                foreach (var driver in _allDrivers)
                {
                    if (_cts?.IsCancellationRequested == true) break;

                    driver.UpdateStatus = "Verificando...";
                    await Task.Delay(5); // Pequeno delay para a UI processar a string

                    bool isOld = false;
                    if (DateTime.TryParse(driver.Date, out DateTime dDate))
                    {
                        if (dDate < DateTime.Now.AddYears(-2)) isOld = true;
                    }

                    if (isOld)
                    {
                        driver.UpdateStatus = "Antigo";
                        driver.IsSelected = true;
                        outdatedCount++;
                    }
                    else
                    {
                        driver.UpdateStatus = "Atualizado";
                        driver.IsSelected = false;
                    }
                }
            });

            SetLoading(false);

            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (outdatedCount > 0)
                {
                    TxtStatus.Text = $"{outdatedCount} drivers antigos.";
                    mw.ShowInfo("SCAN CONCLUÍDO", $"{outdatedCount} drivers parecem antigos e foram selecionados.");
                }
                else
                {
                    TxtStatus.Text = "Drivers recentes.";
                    mw.ShowSuccess("TUDO OK", "Seus drivers parecem atualizados (baseado na data).");
                }
            }
        }

        private async void BtnInstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrivers = _allDrivers.Where(d => d.IsSelected).ToList();

            if (selectedDrivers.Count == 0)
            {
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowInfo("NADA SELECIONADO", "Marque as caixas ou use o botão 'Procurar' primeiro.");
                return;
            }

            if (Application.Current.MainWindow is MainWindow mainWin)
            {
                bool confirm = await mainWin.ShowConfirmationDialog(
                    $"Você selecionou {selectedDrivers.Count} drivers.\n\n" +
                    "Abriremos o Catálogo Oficial da Microsoft para cada um.\n" +
                    "Deseja abrir as páginas de download agora?");

                if (!confirm) return;

                foreach (var driver in selectedDrivers)
                {
                    DriverManager.SearchDriverOnWeb(driver.DeviceName, driver.HardwareId);
                    await Task.Delay(800);
                }
            }
        }

        // --- FERRAMENTAS ---

        private async void BtnInstallFromFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Selecione o driver baixado (CAB, ZIP ou INF)",
                Filter = "Drivers Compactados|*.cab;*.zip|Arquivo INF|*.inf|Todos|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                SetLoading(true, "Extraindo e Instalando...");

                var result = await DriverManager.SmartInstallDriver(path);

                SetLoading(false);

                if (Application.Current.MainWindow is MainWindow mw)
                {
                    if (result.Success)
                    {
                        mw.ShowSuccess("SUCESSO", result.Message);
                        LoadDrivers();
                    }
                    else mw.ShowError("FALHA", result.Message);
                }
            }
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Selecione onde salvar o backup dos drivers";
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    if (Application.Current.MainWindow is MainWindow mw)
                    {
                        var res = DriverManager.BackupDrivers(dialog.SelectedPath);
                        if (res.Success) mw.ShowSuccess("BACKUP", res.Message);
                        else mw.ShowError("ERRO", res.Message);
                    }
                }
            }
        }

        private void BtnExportList_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "Drivers_List.txt",
                Filter = "Texto (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                DriverManager.ExportDriverListToTxt(dialog.FileName);
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowSuccess("EXPORTADO", "Lista salva com sucesso.");
            }
        }

        private void BtnWindowsUpdate_Click(object sender, RoutedEventArgs e)
        {
            DriverManager.OpenWindowsUpdateSettings();
        }

        // --- MENU DE CONTEXTO ---

        private async void CtxUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (GridDrivers.SelectedItem is DriverItem driver && Application.Current.MainWindow is MainWindow mw)
            {
                if (await mw.ShowConfirmationDialog($"REMOVER DRIVER?\n\n{driver.DeviceName}\nIsso pode desativar o dispositivo."))
                {
                    SetLoading(true, "Removendo...");
                    var result = await Task.Run(() => DriverManager.UninstallDriver(driver.InfName));
                    SetLoading(false);

                    if (result.Success) { mw.ShowSuccess("SUCESSO", result.Message); LoadDrivers(); }
                    else mw.ShowError("ERRO", result.Message);
                }
            }
        }

        private void CtxCopyName_Click(object sender, RoutedEventArgs e)
        {
            if (GridDrivers.SelectedItem is DriverItem driver) Clipboard.SetText(driver.DeviceName);
        }

        private void CtxCopyId_Click(object sender, RoutedEventArgs e)
        {
            if (GridDrivers.SelectedItem is DriverItem driver) Clipboard.SetText(driver.HardwareId);
        }
        #endregion

        // =========================================================
        // ABA 2: DIAGNÓSTICO (BSOD / VERIFIER)
        // =========================================================
        #region Diagnostics Logic

        private async Task CheckVerifierStatus()
        {
            await Task.Run(() =>
            {
                if (_cts?.IsCancellationRequested == true) return;

                // Chama o método que restauramos no DiagnosticsManager
                var status = Toolbox.GetDriverVerifierStatus();

                Dispatcher.Invoke(() =>
                {
                    TxtVerifierStatus.Text = status.StatusMessage;

                    if (status.IsActive)
                    {
                        // Vermelho (Ativo = Teste de estresse rodando)
                        TxtVerifierStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 85));
                    }
                    else
                    {
                        // Cinza (Inativo = Normal)
                        TxtVerifierStatus.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
                    }
                });
            });
        }

        private async void BtnEnableVerifier_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                bool confirm = await mw.ShowConfirmationDialog(
                    "PERIGO: ATIVAR DRIVER VERIFIER\n\n" +
                    "Isso forçará um teste de estresse em todos os drivers na próxima reinicialização.\n" +
                    "Se houver um driver ruim, seu PC dará TELA AZUL (BSOD) durante o boot.\n\n" +
                    "Você sabe entrar em Modo de Segurança para desativar isso se algo der errado?");

                if (!confirm) return;

                mw.ShowInfo("ATIVANDO", "Configurando Verifier...");

                var result = await Task.Run(() => Toolbox.EnableDriverVerifier());

                if (result.Success)
                {
                    mw.ShowSuccess("ATIVADO", result.Message);
                    CheckVerifierStatus();
                }
                else
                {
                    mw.ShowError("ERRO", result.Message);
                }
            }
        }

        private async void BtnDisableVerifier_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = await Task.Run(() => Toolbox.ResetDriverVerifier());

                if (result.Success)
                {
                    mw.ShowSuccess("DESATIVADO", "Driver Verifier foi resetado com sucesso.");
                    CheckVerifierStatus();
                }
                else
                {
                    mw.ShowError("ERRO", result.Message);
                }
            }
        }
        #endregion
    }
}