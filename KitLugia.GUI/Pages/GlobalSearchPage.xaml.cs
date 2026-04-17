using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
// Resolve ambiguidade
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;
using CheckBox = System.Windows.Controls.CheckBox;
namespace KitLugia.GUI.Pages
{
    public partial class GlobalSearchPage : Page
    {
        private string _currentQuery = "";
        private CancellationTokenSource? _cts; // Token para cancelar tarefas antigas
      
        public GlobalSearchPage(string query = "")
        {
            InitializeComponent();
            UpdateSearch(query);

            // 🔥 LIMPEZA: Cancela operações ao sair da página
            this.Unloaded += GlobalSearchPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            // Cancela e libera recursos
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            this.Unloaded -= GlobalSearchPage_Unloaded;
        }

        private void GlobalSearchPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        public void UpdateSearch(string query)
        {
            _currentQuery = query;

            // 1. Cancela qualquer verificação de status anterior que ainda esteja rodando
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // 2. Busca Rápida (Síncrona - Apenas filtra texto na memória)
            // Isso é instantâneo e mostra os resultados na tela na hora
            var results = SearchEngine.Search(query);

            ListResults.ItemsSource = null;
            ListResults.ItemsSource = results;
            TxtResultCount.Text = $"{results.Count} itens";

            bool hasResults = results.Count > 0;
            ListResults.Visibility = hasResults ? Visibility.Visible : Visibility.Collapsed;
            PanelNoResults.Visibility = hasResults ? Visibility.Collapsed : Visibility.Visible;

            if (!hasResults) return;

            // 3. Verificação de Status em Background (Assíncrona)
            // Isso roda em outra thread e vai atualizando os switches um por um
            Task.Run(() =>
            {
                foreach (var item in results)
                {
                    // Se o usuário digitou outra coisa ou saiu da página, para de processar
                    if (token.IsCancellationRequested) break;

                    if (item.IsToggle && item.CheckState != null)
                    {
                        try
                        {
                            // Executa a verificação pesada (Registro, BCD, etc)
                            bool state = item.CheckState.Invoke();

                            // Atualiza a propriedade (Como implementamos INotifyPropertyChanged, a UI atualiza sozinha)
                            item.IsActive = state;
                        }
                        catch { }
                    }
                }
            }, token);
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            GlobalSearchResult? item = null;
            if (sender is Button btn) item = btn.Tag as GlobalSearchResult;
            else if (sender is CheckBox chk) item = chk.Tag as GlobalSearchResult;

            if (item == null) return;

            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            if (item.Type == SearchResultType.Navigation)
            {
                mw.NavigateToPage(item.NavigationTag);
            }
            else
            {
                try
                {
                    // Confirmação para itens críticos
                    if (item.Title.Contains("Reiniciar") || item.Title.Contains("MPO"))
                    {
                        if (!await mw.ShowConfirmationDialog($"Executar '{item.Title}'?"))
                        {
                            // Reverte visualmente se for um toggle e o usuário cancelar
                            UpdateSearch(_currentQuery);
                            return;
                        }
                    }

                    mw.ShowInfo("PROCESSANDO", $"Aplicando: {item.Title}...");

                    (bool success, string message) result = (false, "");

                    // Executa a ação em background
                    await Task.Run(() =>
                    {
                        if (item.ExecuteAction != null)
                            result = item.ExecuteAction.Invoke();

                        // Atualiza o status visual após a ação (re-checa o estado real)
                        if (item.IsToggle && item.CheckState != null)
                        {
                            try { item.IsActive = item.CheckState.Invoke(); } catch { }
                        }
                    });

                    if (result.success) mw.ShowSuccess("CONCLUÍDO", result.message);
                    else mw.ShowError("ATENÇÃO", result.message);
                }
                catch (Exception ex)
                {
                    mw.ShowError("ERRO CRÍTICO", ex.Message);
                }
            }
        }
    }
}
