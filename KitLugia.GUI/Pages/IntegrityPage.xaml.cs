using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Media;
using KitLugia.Core;

// === RESOLUÇÃO DE AMBIGUIDADES ===
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;

#pragma warning disable CS4014 // Chamadas async não aguardadas são intencionais para operações em background


namespace KitLugia.GUI.Pages
{
    public partial class IntegrityPage : Page
    {
        private bool _isBusy = false;

        public IntegrityPage()
        {
            InitializeComponent();
            RunScan();
        }

        private async Task RunScan()
        {
            if (_isBusy) return;
            _isBusy = true;

            try
            {
                UpdateUiState(isLoading: true);
                if (TxtScore != null) TxtScore.Text = "...";

                var tweaks = await Task.Run(() => Guardian.GetHarmfulTweaksWithStatus());

                if (ItemsList != null)
                {
                    ItemsList.ItemsSource = null;
                    ItemsList.ItemsSource = tweaks;
                }

                var badItems = tweaks.Where(t => t.Status == TweakStatus.MODIFIED).ToList();
                int total = tweaks.Count;
                int score = total > 0 ? 100 - (100 * badItems.Count / total) : 100;

                if (TxtScore != null) TxtScore.Text = score + "%";
                UpdateScoreColor(score);

                if (BtnFixAll != null && BtnRescan != null)
                {
                    if (score == 100)
                    {
                        BtnFixAll.Visibility = Visibility.Collapsed;
                        BtnRescan.Margin = new Thickness(0, 0, 0, 0);
                    }
                    else
                    {
                        BtnFixAll.Visibility = Visibility.Visible;
                        BtnRescan.Margin = new Thickness(15, 0, 0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                if (Application.Current.MainWindow is MainWindow mw)
                    mw.ShowError("ERRO NO SCAN", ex.Message);
            }
            finally
            {
                _isBusy = false;
                UpdateUiState(isLoading: false);
            }
        }

        private void UpdateUiState(bool isLoading)
        {
            if (BtnRescan != null) BtnRescan.IsEnabled = !isLoading;
            if (BtnFixAll != null) BtnFixAll.IsEnabled = !isLoading;
            if (ItemsList != null) ItemsList.IsEnabled = !isLoading;

            if (isLoading && BtnFixAll != null && BtnFixAll.Visibility == Visibility.Visible)
                BtnFixAll.Content = "⏳ PROCESSANDO...";
            else if (BtnFixAll != null)
                BtnFixAll.Content = "🛡️ RESTAURAR TODOS (PADRÃO SEGURO)";
        }

        private void UpdateScoreColor(int score)
        {
            if (BorderScore != null && TxtScore != null)
            {
                if (score == 100)
                {
                    var green = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    BorderScore.BorderBrush = green;
                    TxtScore.Foreground = green;
                }
                else if (score > 60)
                {
                    var gold = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                    BorderScore.BorderBrush = gold;
                    TxtScore.Foreground = gold;
                }
                else
                {
                    var red = new SolidColorBrush(Color.FromRgb(196, 43, 28));
                    BorderScore.BorderBrush = red;
                    TxtScore.Foreground = red;
                }
            }
        }

        private void BtnRescan_Click(object sender, RoutedEventArgs e)
        {
            RunScan();
        }

        // --- CLIQUE NO ÍCONE DE INFORMAÇÃO (i) ---
        private void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string description)
            {
                if (string.IsNullOrEmpty(description)) description = "Sem descrição disponível.";

                // Exibe uma caixa de mensagem simples com o detalhe
                MessageBox.Show(description, "Detalhes de Segurança", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnToggleItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            if (sender is Button btn && btn.Tag is ScannableTweak tweak)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow == null) return;

                if (tweak.Status == TweakStatus.OK)
                {
                    bool confirm = await mainWindow.ShowConfirmationDialog(
                        $"⚠️ PERIGO: Desativar '{tweak.Name}' reduz a segurança.\nTem certeza?");

                    if (!confirm) return;
                }

                _isBusy = true;
                btn.IsEnabled = false;
                btn.Content = "⏳";

                try
                {
                    var result = await Task.Run(() => Guardian.ToggleTweak(tweak));
                    await Task.Delay(500);

                    if (result.Success)
                    {
                        if (tweak.Status == TweakStatus.MODIFIED)
                            mainWindow.ShowSuccess("SUCESSO", "Item restaurado.");
                        else
                            mainWindow.ShowInfo("ATENÇÃO", "Item modificado (Personalizado).");
                    }
                    else
                    {
                        mainWindow.ShowError("FALHA", result.Message);
                    }
                }
                catch (Exception ex)
                {
                    mainWindow.ShowError("ERRO CRÍTICO", ex.Message);
                }
                finally
                {
                    _isBusy = false;
                    RunScan();
                }
            }
        }

        private async void BtnFixAll_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy) return;

            if (Application.Current.MainWindow is MainWindow mw)
            {
                bool confirm = await mw.ShowConfirmationDialog(
                    "RESTAURAÇÃO TOTAL DE INTEGRIDADE\n\n" +
                    "Isso corrigirá TODAS as vulnerabilidades detectadas.\nContinuar?");

                if (!confirm) return;

                _isBusy = true;
                UpdateUiState(isLoading: true);

                mw.ShowInfo("INICIANDO", "Analisando e corrigindo itens...");

                int fixedCount = 0;
                int errorCount = 0;

                await Task.Run(async () =>
                {
                    var currentTweaks = Guardian.GetHarmfulTweaksWithStatus();
                    var badTweaks = currentTweaks.Where(t => t.Status == TweakStatus.MODIFIED).ToList();

                    foreach (var t in badTweaks)
                    {
                        try
                        {
                            var res = Guardian.ToggleTweak(t);
                            if (res.Success) fixedCount++;
                            else errorCount++;
                        }
                        catch { errorCount++; }
                        await Task.Delay(150);
                    }
                    await Task.Delay(800);
                });

                if (errorCount == 0)
                    mw.ShowSuccess("CONCLUÍDO", $"{fixedCount} itens foram corrigidos com sucesso.");
                else
                    mw.ShowInfo("FINALIZADO", $"{fixedCount} corrigidos. {errorCount} falharam.");

                _isBusy = false;
                RunScan();
            }
        }
    }
}