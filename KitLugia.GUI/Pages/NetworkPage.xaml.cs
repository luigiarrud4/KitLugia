using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KitLugia.Core;
using KitLugia.GUI.Controls;

// === CORREÇÃO DAS AMBIGUIDADES ===
// Define que Color, Application e MessageBox devem vir do WPF (System.Windows), não do Forms.
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

#pragma warning disable CS4014 // Chamadas async não aguardadas são intencionais para operações em background
using Brushes = System.Windows.Media.Brushes;
using ToolTip = System.Windows.Controls.ToolTip;

namespace KitLugia.GUI.Pages
{
    public partial class NetworkPage : Page
    {
        private bool _isLoading = true;
        private readonly SolidColorBrush _colorActive = new SolidColorBrush(Color.FromRgb(108, 203, 95)); // Verde
        private readonly SolidColorBrush _colorDefault = new SolidColorBrush(Color.FromRgb(150, 150, 150)); // Cinza
        private readonly SolidColorBrush _colorWarning = new SolidColorBrush(Color.FromRgb(244, 129, 32)); // Laranja

        public NetworkPage()
        {
            InitializeComponent();
            LoadStatus();
        }

        private async Task LoadStatus()
        {
            await Task.Run(() =>
            {
                var dnsInfo = Toolbox.GetActiveDnsInfo();

                Dispatcher.Invoke(() =>
                {
                    _isLoading = true;

                    UpdateDnsUi(dnsInfo.Provider, dnsInfo.DnsIp);

                    if (ChkDriver != null)
                    {
                        ChkDriver.IsChecked = false;
                        if (StatusDriver != null) StatusDriver.Text = "Pronto";
                    }

                    if (ChkTcp != null)
                    {
                        ChkTcp.IsChecked = false;
                    }

                    _isLoading = false;
                });
            });
        }

        private void UpdateDnsUi(string provider, string ip)
        {
            if (BtnCloudflare == null) return;

            BtnCloudflare.Tag = null;
            BtnGoogle.Tag = null;
            BtnDhcp.Tag = null;

            TxtCurrentDnsIp.Text = string.IsNullOrEmpty(ip) || ip == "N/A" ? "Automático / DHCP" : ip;
            TxtCurrentDnsIp.Foreground = _colorDefault;

            if (provider.ToUpper().Contains("CLOUDFLARE")) { BtnCloudflare.Tag = "Selected"; TxtCurrentDnsIp.Foreground = _colorActive; }
            else if (provider.ToUpper().Contains("GOOGLE")) { BtnGoogle.Tag = "Selected"; TxtCurrentDnsIp.Foreground = _colorActive; }
            else if (provider.ToUpper().Contains("DHCP")) { BtnDhcp.Tag = "Selected"; TxtCurrentDnsIp.Foreground = _colorActive; }
            else { TxtCurrentDnsIp.Text = $"{ip} (Custom)"; TxtCurrentDnsIp.Foreground = _colorWarning; }
        }

        private void UpdateLabel(TextBlock label, bool isActive)
        {
            if (label == null) return;
            label.Text = isActive ? "Otimizado" : "Padrão";
            label.Foreground = isActive ? _colorActive : _colorDefault;
        }

        // =========================================================
        // SEÇÃO: DNS
        // =========================================================
        private async Task ApplyDns(string provider)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            mw.ShowInfo("DNS", $"Configurando {provider}...");
            var result = await Task.Run(() => Toolbox.SetDns(provider));

            if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
            else mw.ShowError("ERRO", result.Message);

            LoadStatus();
        }

        private void BtnDnsCloudflare_Click(object sender, RoutedEventArgs e) => ApplyDns("Cloudflare");
        private void BtnDnsGoogle_Click(object sender, RoutedEventArgs e) => ApplyDns("Google");
        private void BtnDnsReset_Click(object sender, RoutedEventArgs e) => ApplyDns("DHCP");

        private async void BtnFlushDns_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                var result = await Task.Run(() => Toolbox.FlushDnsCache());
                mw.ShowSuccess("CACHE", result.Message);
            }
        }

        // =========================================================
        // SEÇÃO: TCP (CTCP)
        // =========================================================
        private async void ChkTcp_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading || !(Application.Current.MainWindow is MainWindow mw)) return;

            if (ChkTcp.IsChecked == true)
            {
                mw.ShowInfo("TCP AVANÇADO", "Aplicando algoritmo CTCP...");

                var result = await Task.Run(() => Toolbox.ApplyLatencyCongestionControl());

                if (result.Success)
                {
                    mw.ShowSuccess("LATÊNCIA", "TCP otimizado para jogos (CTCP/NoDelay).");
                    StatusTcp.Text = "Otimizado (CTCP)";
                    StatusTcp.Foreground = _colorActive;
                }
                else
                {
                    mw.ShowError("ERRO", result.Message);
                    ChkTcp.IsChecked = false;
                }
            }
            else
            {
                StatusTcp.Text = "Padrão";
                StatusTcp.Foreground = _colorDefault;
                mw.ShowInfo("REVERTER", "Para desfazer totalmente, use 'Resetar Pilha de Rede' na aba Ferramentas.");
            }
        }

        // =========================================================
        // SEÇÃO: DRIVER (SINTONIA INTELIGENTE)
        // =========================================================
        private async void ChkDriver_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoading || !(Application.Current.MainWindow is MainWindow mw)) return;

            // 1. LÓGICA DE DESATIVAR (RESETAR)
            if (ChkDriver.IsChecked == false)
            {
                if (await mw.ShowConfirmationDialog("Deseja restaurar TODAS as configurações de Ethernet para o padrão de fábrica?"))
                {
                    mw.ShowInfo("RESTAURANDO", "Resetando adaptador...");
                    await Task.Run(() => SystemTweaks.ResetEthernetSettings());
                    mw.ShowSuccess("RESTAURADO", "Configurações restauradas com sucesso!");
                    StatusDriver.Text = "Padrão";
                    StatusDriver.Foreground = _colorDefault;
                }
                else
                {
                    _isLoading = true;
                    ChkDriver.IsChecked = true;
                    _isLoading = false;
                }
                return;
            }

            // 2. LÓGICA DE ATIVAR (AUTO-TUNER)
            bool confirm = await mw.ShowConfirmationDialog(
                "SINTONIA DE REDE INTELIGENTE\n\n" +
                "O KitLugia irá testar configurações uma por uma (Flow Control, Interrupções, etc).\n" +
                "Se a latência subir, ele reverte a configuração automaticamente.\n\n" +
                "Isso levará cerca de 30-60 segundos. Continuar?");

            if (!confirm)
            {
                ChkDriver.IsChecked = false;
                return;
            }

            ChkDriver.IsEnabled = false;
            StatusDriver.Text = "Sintonizando...";
            StatusDriver.Foreground = _colorWarning;

            try
            {
                await Task.Run(() => SystemTweaks.AutoTuneNetworkAdapter());

                mw.ShowSuccess("CONCLUÍDO", "Sintonia finalizada com sucesso!");

                StatusDriver.Text = "Sintonizado";
                StatusDriver.Foreground = _colorActive;
            }
            catch (Exception ex)
            {
                mw.ShowError("ERRO CRÍTICO", $"Falha na sintonia: {ex.Message}");
                StatusDriver.Text = "Erro";
                ChkDriver.IsChecked = false;
            }
            finally
            {
                ChkDriver.IsEnabled = true;
            }
        }

        // MÉTODOS DO DIAGNÓSTICO E REPARO DE REDE
        private async void BtnRunDiagnostics_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            // Mostrar status bar
            NetworkStatusBar.Visibility = Visibility.Visible;
            NetworkStatusText.Text = "Running Diagnostics";
            NetworkStatusDetails.Text = "Executing 9 advanced network tests...";
            BtnRunDiagnostics.IsEnabled = false;

            try
            {
                await Task.Run(() =>
                {
                    var results = SystemTweaks.RunNetworkDiagnostics();
                    
                    Dispatcher.Invoke(() =>
                    {
                        DisplayDiagnosticsResults(results);
                    });
                });

                mw.ShowSuccess("Diagnóstico Concluído", "Todos os testes foram executados com sucesso!");
            }
            catch (Exception ex)
            {
                mw.ShowError("Erro no Diagnóstico", $"Falha ao executar diagnóstico: {ex.Message}");
            }
            finally
            {
                NetworkStatusBar.Visibility = Visibility.Collapsed;
                BtnRunDiagnostics.IsEnabled = true;
            }
        }

        private void DisplayDiagnosticsResults(List<SystemTweaks.NetworkDiagnosticResult> results)
        {
            DiagnosticsResultsList.Children.Clear();
            DiagnosticsResultsPanel.Visibility = Visibility.Visible;
            
            foreach (var result in results)
            {
                var resultItem = CreateDiagnosticResultItem(result);
                DiagnosticsResultsList.Children.Add(resultItem);
            }
        }

        private Border CreateDiagnosticResultItem(SystemTweaks.NetworkDiagnosticResult result)
        {
            var border = new Border
            {
                Background = result.Success ? new SolidColorBrush(Color.FromRgb(34, 34, 34)) : new SolidColorBrush(Color.FromRgb(44, 34, 34)),
                BorderBrush = result.Success ? _colorActive : _colorWarning,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon
            var icon = new TextBlock
            {
                Text = result.Success ? "✅" : "⚠️",
                FontSize = 18,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(icon, 0);

            // Content
            var stackPanel = new StackPanel();
            stackPanel.VerticalAlignment = VerticalAlignment.Top;

            // Título bem destacado
            var title = new TextBlock
            {
                Text = result.TestName,
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };

            // Descrição com cor diferente
            var details = new TextBlock
            {
                Text = result.Details,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                Margin = new Thickness(0, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };

            stackPanel.Children.Add(title);
            stackPanel.Children.Add(details);
            Grid.SetColumn(stackPanel, 1);

            // Status
            var status = new TextBlock
            {
                Text = result.Success ? "OK" : "ISSUE",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = result.Success ? _colorActive : _colorWarning,
                Margin = new Thickness(15, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(status, 2);

            grid.Children.Add(icon);
            grid.Children.Add(stackPanel);
            grid.Children.Add(status);

            border.Child = grid;

            // Add tooltip with recommendation
            if (!string.IsNullOrEmpty(result.Recommendation))
            {
                var tooltipText = new TextBlock
                {
                    Text = result.Recommendation,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300
                };

                var tooltip = new ToolTip
                {
                    Content = tooltipText,
                    Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                    Foreground = Brushes.White,
                    Padding = new Thickness(12)
                };
                border.ToolTip = tooltip;
            }

            return border;
        }

        private async void BtnRepairNetwork_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            bool confirm = await mw.ShowConfirmationDialog(
                "Reparo Automático de Rede\n\n" +
                "Esta ação irá:\n" +
                "• Resetar adaptadores de rede\n" +
                "• Limpar cache DNS\n" +
                "• Resetar configuração Winsock\n" +
                "• Resetar configuração de proxy\n" +
                "• Reiniciar serviços de rede\n\n" +
                "A conexão pode ser interrompida por alguns segundos. Continuar?");

            if (!confirm) return;

            BtnRepairNetwork.IsEnabled = false;
            NetworkStatusBar.Visibility = Visibility.Visible;
            NetworkStatusText.Text = "Repairing Network";
            NetworkStatusDetails.Text = "Applying automatic fixes...";

            try
            {
                await Task.Run(() =>
                {
                    var result = SystemTweaks.RepairNetworkIssues();
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (result.Success)
                        {
                            mw.ShowSuccess("Reparo Concluído", result.Message);
                            // Re-run diagnostics to show improvements
                            BtnRunDiagnostics_Click(sender!, e!);
                        }
                        else
                        {
                            mw.ShowError("Erro no Reparo", result.Message);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                mw.ShowError("Erro no Reparo", $"Falha ao reparar rede: {ex.Message}");
            }
            finally
            {
                BtnRepairNetwork.IsEnabled = true;
                NetworkStatusBar.Visibility = Visibility.Collapsed;
            }
        }

        private async void BtnOptimizeGaming_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            bool confirm = await mw.ShowConfirmationDialog(
                "Otimização para Gaming\n\n" +
                "Esta ação irá:\n" +
                "• Desativar Nagle's Algorithm (reduz latência)\n" +
                "• Otimizar QoS para priorizar jogos\n" +
                "• Configurar auto-tuning para restricted\n\n" +
                "Ideal para jogos online e streaming. Continuar?");

            if (!confirm) return;

            BtnOptimizeGaming.IsEnabled = false;
            NetworkStatusBar.Visibility = Visibility.Visible;
            NetworkStatusText.Text = "Optimizing for Gaming";
            NetworkStatusDetails.Text = "Applying gaming optimizations...";

            try
            {
                await Task.Run(() =>
                {
                    var result = SystemTweaks.OptimizeNetworkForGaming();
                    
                    Dispatcher.Invoke(() =>
                    {
                        if (result.Success)
                        {
                            mw.ShowSuccess("Otimização Concluída", result.Message);
                            // Re-run diagnostics to show improvements
                            BtnRunDiagnostics_Click(sender!, e!);
                        }
                        else
                        {
                            mw.ShowError("Erro na Otimização", result.Message);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                mw.ShowError("Erro na Otimização", $"Falha ao otimizar rede: {ex.Message}");
            }
            finally
            {
                BtnOptimizeGaming.IsEnabled = true;
                NetworkStatusBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}