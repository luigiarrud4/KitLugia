using System;
using System.Windows;
using System.Windows.Controls;

namespace KitLugia.GUI.Pages
{
    public partial class OptimizationPage : Page
    {
        public OptimizationPage()
        {
            InitializeComponent();
            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += OptimizationPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            this.Unloaded -= OptimizationPage_Unloaded;
        }

        private void OptimizationPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }
    }
}
