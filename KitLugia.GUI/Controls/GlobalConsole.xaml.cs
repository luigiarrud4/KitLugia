using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KitLugia.GUI; // Para acessar ConsoleManager

// Resolve ambiguidade
using UserControl = System.Windows.Controls.UserControl;

namespace KitLugia.GUI.Controls
{
    public partial class GlobalConsole : UserControl
    {
        // Evento para avisar a MainWindow que o usuário quer fechar o console
        public event EventHandler? RequestClose;

        public GlobalConsole()
        {
            InitializeComponent();

            // Inscreve no evento para atualizar o TextBox quando chegar msg nova
            ConsoleManager.OnLogAdded += UpdateTextBox;

            // Mensagem inicial de teste
            ConsoleManager.WriteLine("KitLugia Console Inicializado.");
            ConsoleManager.WriteLine("Aguardando comandos...");
        }

        private void UpdateTextBox()
        {
            // Executa na thread da UI
            Dispatcher.Invoke(() =>
            {
                // Atualiza o TextBox com todo o conteúdo do ConsoleManager
                TxtLog.Text = string.Join(Environment.NewLine, ConsoleManager.Logs);
                
                // Rola para o final
                LogScroller.ScrollToBottom();
            });
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtLog.Text))
                {
                    System.Windows.Clipboard.SetText(TxtLog.Text);
                    ConsoleManager.WriteLine("Conteúdo do console copiado para a área de transferência.");
                }
                else
                {
                    ConsoleManager.WriteLine("Nenhum conteúdo para copiar.");
                }
            }
            catch (Exception ex)
            {
                ConsoleManager.WriteLine($"Erro ao copiar: {ex.Message}");
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            ConsoleManager.Clear();
            ConsoleManager.WriteLine("Console limpo pelo usuário.");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Dispara evento para quem estiver usando o controle (MainWindow)
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void TxtLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Mantém o scroll no final quando o texto é atualizado
            if (TxtLog.IsLoaded)
            {
                LogScroller.ScrollToBottom();
            }
        }

        // Boa prática: Desinscrever eventos ao destruir o controle para não vazar memória
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ConsoleManager.OnLogAdded -= UpdateTextBox;
        }
    }
}