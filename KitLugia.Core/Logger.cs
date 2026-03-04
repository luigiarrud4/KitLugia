using System;

namespace KitLugia.Core
{
    // Classe estática simples para enviar mensagens do Core para a GUI
    public static class Logger
    {
        // Evento que a GUI vai "escutar"
        public static event Action<string>? OnLogReceived;

        public static void Log(string message)
        {
            // Dispara o evento se houver alguém escutando
            OnLogReceived?.Invoke(message);
        }

        public static void LogProcess(string filename, string args)
        {
            OnLogReceived?.Invoke($"[EXEC] {filename} {args}");
        }

        public static void LogRegistry(string key, string value, object data)
        {
            OnLogReceived?.Invoke($"[REG] Setando '{value}' = '{data}' em {key}");
        }

        public static void LogError(string context, string error)
        {
            OnLogReceived?.Invoke($"[ERRO] ({context}): {error}");
        }
    }
}