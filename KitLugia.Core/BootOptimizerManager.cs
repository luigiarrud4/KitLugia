using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        /// <summary>
        /// Analisa os eventos de inicialização (boot) do Windows para identificar gargalos.
        /// </summary>
        /// <returns>Um objeto 'BootAnalysisResult' com os dados da análise.</returns>
        public static BootAnalysisResult AnalyzeBootPerformance()
        {
            var result = new BootAnalysisResult();

            // Passo 1: Verificar se o serviço de coleta de logs de performance está ativo.
            if (SystemUtils.GetServiceStartMode("pla") == "Disabled")
            {
                result.ServiceStatusMessage = "AVISO: O serviço 'Logs e Alertas de Desempenho' (pla) está desativado. A análise pode estar incompleta.";
            }

            // Passo 2: Obter os dados brutos.
            var allEvents = SystemTweaks.GetPerformanceEvents(100, 101, 199);
            var startupApps = StartupManager.GetStartupAppsWithDetails(true);

            result.TotalTimeEvent = allEvents.FirstOrDefault(e => e.EventId == 100);

            // Passo 3: Filtrar e processar os eventos relevantes.
            var recentSlowItems = allEvents
                .Where(e => e.EventId != 100 && e.TimeTaken > 1000 && e.TimeOfEvent >= DateTime.Now.AddMonths(-1))
                .GroupBy(e => e.ItemName)
                .Select(g => g.OrderByDescending(e => e.TimeOfEvent).First())
                .ToList();

            // Passo 4: Classificar os itens lentos.
            result.SlowStartupItems = recentSlowItems
                .Where(e => startupApps.Any(s => s.FullCommand.Contains(e.ItemName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(e => e.TimeTaken)
                .ToList();

            result.HighImpactApps = recentSlowItems
                .Where(e => !result.SlowStartupItems.Any(s => s.ItemName == e.ItemName))
                .OrderByDescending(e => e.TimeTaken)
                .ToList();

            return result;
        }

        /// <summary>
        /// Analisa o tempo total do último desligamento (shutdown).
        /// </summary>
        public static PerformanceEvent? AnalyzeShutdownPerformance()
        {
            var shutdownEvents = SystemTweaks.GetPerformanceEvents(200, 201, 299);
            return shutdownEvents.FirstOrDefault(e => e.EventId == 200);
        }

        /// <summary>
        /// Abre uma pesquisa no Google para ajudar o usuário a entender o que é um determinado processo.
        /// </summary>
        public static void OpenGoogleSearch(string itemName)
        {
            try
            {
                string query = Uri.EscapeDataString($"what is {itemName}");
                string url = $"https://www.google.com/search?q={query}";
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            catch { }
        }
    }
}