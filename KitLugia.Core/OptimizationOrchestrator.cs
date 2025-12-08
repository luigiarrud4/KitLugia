using System;
using System.Threading.Tasks;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class OptimizationOrchestrator
    {
        /// <summary>
        /// Executa a rotina "1-Click Optimization" completa.
        /// </summary>
        /// <param name="progress">Interface para reportar progresso à UI (ex: barra de progresso ou label).</param>
        public static async Task RunFullOptimizationAsync(IProgress<string> progress)
        {
            await Task.Run(() =>
            {
                progress?.Report("Iniciando Otimização Essencial...");
                
                // Passo 1: Tweaks de Registro
                progress?.Report("[1/5] Aplicando Tweaks de Registro e Cache...");
                SystemTweaks.ApplyAutoCacheTweak();
                SystemTweaks.ApplyLastClickTweak();
                SystemTweaks.ApplyBingTweak();
                System.Threading.Thread.Sleep(500); // Pequeno delay apenas para UX visual na barra

                // Passo 2: Energia
                progress?.Report("[2/5] Configurando Plano de Energia (Bitsum/Ultimate)...");
                // Tenta importar o plano Bitsum se o recurso existir, senão tenta Ultimate
                bool powerSuccess = SystemTweaks.ImportAndActivatePowerPlan("KitLugia.Core.Resources.BitsumHighestPerformance.pow").Success;
                if (!powerSuccess)
                {
                    // Fallback para High Performance nativo
                    SystemUtils.RunExternalProcess("powercfg", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", true);
                }

                // Passo 3: Jogos
                progress?.Report("[3/5] Aplicando Prioridade de GPU para Jogos...");
                SystemTweaks.ApplyGamingOptimizations();

                // Passo 4: Boot
                progress?.Report("[4/5] Ativando Mensagens Detalhadas de Boot...");
                SystemTweaks.ApplyVerboseStatus();

                // Passo 5: VRAM (Integrada)
                progress?.Report("[5/5] Ajustando Alocação de VRAM (se aplicável)...");
                SystemTweaks.ApplyAutomaticVramTweak();

                progress?.Report("Otimização Concluída! Reinicie o computador.");
            });
        }
    }
}