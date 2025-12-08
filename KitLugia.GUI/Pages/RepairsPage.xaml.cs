using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core; // Importante para reconhecer RepairAction

// Resolve conflito se houver (WPF vs Forms)
using Button = System.Windows.Controls.Button;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class RepairsPage : Page
    {
        // Cache da lista completa para filtros rápidos
        private List<RepairAction> _allRepairs = new();

        public RepairsPage()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            // 1. Pega TUDO do Core (GeneralRepairManager)
            _allRepairs = GeneralRepairManager.GetAllRepairs();

            // 2. Extrai categorias únicas dinamicamente
            // Isso significa que se você adicionar uma categoria nova no Core,
            // ela aparece aqui no menu sem mexer no XAML.
            var categories = _allRepairs.Select(x => x.Category).Distinct().OrderBy(c => c).ToList();

            // Adiciona a opção de ver todos no topo
            categories.Insert(0, "Todos");

            // Liga os dados à lista da esquerda
            LstCategories.ItemsSource = categories;

            // Seleciona o primeiro por padrão
            LstCategories.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            // Se a lista ainda não carregou, sai
            if (_allRepairs == null || !_allRepairs.Any()) return;

            string searchText = TxtFilter.Text.ToLower().Trim();
            string selectedCat = LstCategories.SelectedItem as string ?? "Todos";

            // LINQ: O filtro mágico que atualiza a tela
            var filtered = _allRepairs.Where(r =>
            {
                // Verifica Categoria
                bool catMatch = selectedCat == "Todos" || r.Category == selectedCat;

                // Verifica Texto (Busca no Nome e na Descrição)
                bool textMatch = string.IsNullOrEmpty(searchText) ||
                                 r.Name.ToLower().Contains(searchText) ||
                                 r.Description.ToLower().Contains(searchText);

                return catMatch && textMatch;
            }).ToList();

            // Atualiza a visualização (o ItemsControl recria os botões automaticamente)
            ItemsRepairs.ItemsSource = filtered;
        }

        // --- EVENTOS DE INTERFACE ---

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void LstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // --- LÓGICA DE CLIQUE NO BOTÃO (Executar Reparo) ---
        private async void BtnRunRepair_Click(object sender, RoutedEventArgs e)
        {
            // O objeto "RepairAction" está escondido na propriedade Tag do botão
            if (sender is Button btn && btn.Tag is RepairAction action)
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;

                // 1. CHECAGEM DE SEGURANÇA
                // Se o reparo for marcado como 'IsDangerous' no Core, pede confirmação
                if (action.IsDangerous && mainWindow != null)
                {
                    bool confirm = await mainWindow.ShowConfirmationDialog(
                        $"ATENÇÃO: A ação '{action.Name}' altera configurações críticas.\nIsso pode afetar a conectividade ou sistema.\n\nDeseja realmente continuar?");

                    if (!confirm) return;
                }

                // 2. FEEDBACK VISUAL (Início)
                btn.IsEnabled = false; // Trava o botão para não clicar 2x
                btn.Opacity = 0.6;
                if (mainWindow != null) mainWindow.ShowInfo("PROCESSANDO", $"Executando: {action.Name}...");

                try
                {
                    // 3. EXECUÇÃO EM BACKGROUND (Thread Separada)
                    // Isso impede que a janela congele enquanto roda o comando
                    await Task.Run(() =>
                    {
                        // Roda o código Action definido no Core
                        action.Execute?.Invoke();
                    });

                    // 4. SUCESSO
                    if (mainWindow != null)
                    {
                        if (action.IsSlow)
                            mainWindow.ShowSuccess("INICIADO", $"{action.Name} foi iniciado em uma nova janela.");
                        else
                            mainWindow.ShowSuccess("CONCLUÍDO", $"{action.Name} finalizado com sucesso.");
                    }
                }
                catch (Exception ex)
                {
                    // 5. ERRO
                    if (mainWindow != null) mainWindow.ShowError("FALHA", $"Erro ao executar: {ex.Message}");
                }
                finally
                {
                    // 6. LIMPEZA (Destrava o botão)
                    btn.IsEnabled = true;
                    btn.Opacity = 1.0;
                }
            }
        }
    }
}