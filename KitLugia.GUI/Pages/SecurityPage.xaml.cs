using System;
using System.Windows;
using System.Windows.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using KitLugia.Core;

namespace KitLugia.GUI.Pages
{
    public partial class SecurityPage : Page
    {
        public SecurityPage()
        {
            InitializeComponent();
            LoadState();
            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += SecurityPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            this.Unloaded -= SecurityPage_Unloaded;
        }

        private void SecurityPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private void LoadState()
        {
            try 
            {
                // Carregar estado real usando SystemTweaks
                // Exemplo: CheckboxVBS.IsChecked = SystemTweaks.IsVbsEnabled();
            }
            catch { }
        }

        private void ToggleVbs_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                var result = SystemTweaks.ToggleVbs();
                System.Windows.MessageBox.Show(result.Message, "Segurança", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void ToggleTelemetry_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                // Lógica de Telemetria: Se marcado, desativa (protege)
                bool disableTelemetry = chk.IsChecked ?? false;
                
                // Implementar lógica de desativar telemetria aqui ou chamar SystemTweaks
                // Exemplo simplificado:
                try
                {
                    string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
                    Microsoft.Win32.Registry.SetValue(key, "AllowTelemetry", disableTelemetry ? 0 : 3, Microsoft.Win32.RegistryValueKind.DWord);
                    System.Windows.MessageBox.Show(disableTelemetry ? "Telemetria Desativada." : "Telemetria Restaurada.", "Privacidade", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Erro ao alterar telemetria: " + ex.Message, "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
