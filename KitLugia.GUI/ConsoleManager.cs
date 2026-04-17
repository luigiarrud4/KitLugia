using System;
using System.Collections.ObjectModel;
using System.Windows;

// --- CORREÇÃO CRÍTICA: Resolve a ambiguidade ---
using Application = System.Windows.Application;

namespace KitLugia.GUI
{
    public static class ConsoleManager
    {
        // A lista de linhas de texto que aparecerá no terminal
        public static ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        // Evento para avisar a UI para rolar para o final
        public static event Action? OnLogAdded;

        // 🔥 PROPERTIE: Modo debug (controla visibilidade do menu de debug, não o terminal)
        public static bool IsDebugEnabled { get; set; } = false;

        public static void WriteLine(string message)
        {
            // 🔥 Terminal sempre loga - não é afetado pelo modo debug
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string time = DateTime.Now.ToString("HH:mm:ss");
                    // Adiciona a linha com horário
                    Logs.Add($"[{time}] {message}");

                    // Limita o histórico para não pesar a memória (200 linhas)
                    if (Logs.Count > 200)
                    {
                        Logs.RemoveAt(0);
                    }

                    OnLogAdded?.Invoke();
                });
            }
        }

        public static void WriteError(string error)
        {
            // 🔥 NOVO: Erros sempre são logados, mesmo com debug desativado
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string time = DateTime.Now.ToString("HH:mm:ss");
                    Logs.Add($"[{time}] [ERRO] {error}");

                    if (Logs.Count > 200)
                    {
                        Logs.RemoveAt(0);
                    }

                    OnLogAdded?.Invoke();
                });
            }
        }

        public static void Clear()
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() => Logs.Clear());
            }
        }
    }
}