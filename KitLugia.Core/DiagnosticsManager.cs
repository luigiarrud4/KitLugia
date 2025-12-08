using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceProcess; // Essencial para controlar serviços do Windows (ex: wuauserv)
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        /// <summary>
        /// Reseta os componentes do Windows Update para corrigir problemas de atualização.
        /// </summary>
        /// <returns>Uma tupla com o status da operação e um log detalhado.</returns>
        public static (bool Success, List<string> Log) ResetWindowsUpdateComponents()
        {
            var log = new List<string>();
            bool overallSuccess = true;

            // Lista de serviços essenciais para o funcionamento do Windows Update.
            string[] services = { "wuauserv", "cryptSvc", "bits", "msiserver" };

            log.Add("Parando serviços do Windows Update...");
            foreach (var serviceName in services)
            {
                var result = ManageService(serviceName, "stop");
                log.Add(result.Message);
                if (!result.Success) overallSuccess = false;
            }

            log.Add("Renomeando pastas de cache do Windows Update...");
            try
            {
                string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string systemdir = Environment.GetFolderPath(Environment.SpecialFolder.System);

                // Renomeia a pasta principal de download do Update.
                string sd = Path.Combine(windir, "SoftwareDistribution");
                string oldSd = sd + ".old";
                if (Directory.Exists(oldSd)) try { Directory.Delete(oldSd, true); } catch { /* Ignora */ }
                if (Directory.Exists(sd)) try { Directory.Move(sd, oldSd); } catch { /* Ignora */ }
                log.Add("  - Pasta 'SoftwareDistribution' renomeada.");

                // Renomeia a pasta de catálogo de componentes.
                string cr = Path.Combine(systemdir, "catroot2");
                string oldCr = cr + ".old";
                if (Directory.Exists(oldCr)) try { Directory.Delete(oldCr, true); } catch { /* Ignora */ }
                if (Directory.Exists(cr)) try { Directory.Move(cr, oldCr); } catch { /* Ignora */ }
                log.Add("  - Pasta 'catroot2' renomeada.");
            }
            catch (Exception ex)
            {
                log.Add($"ERRO CRÍTICO ao renomear pastas: {ex.Message}");
                overallSuccess = false;
            }

            log.Add("Reiniciando serviços do Windows Update...");
            // Reinicia os serviços na ordem inversa para garantir dependências.
            foreach (var serviceName in services.Reverse())
            {
                var result = ManageService(serviceName, "start");
                log.Add(result.Message);
                if (!result.Success) overallSuccess = false;
            }

            return (overallSuccess, log);
        }

        /// <summary>
        /// Helper interno para parar ou iniciar um serviço do Windows.
        /// Visível apenas dentro do projeto KitLugia.Core.
        /// </summary>
        internal static (bool Success, string Message) ManageService(string serviceName, string action)
        {
            string status = action == "stop" ? "Parando" : "Iniciando";
            try
            {
                using var service = new ServiceController(serviceName);
                if (action == "stop" && service.Status != ServiceControllerStatus.Stopped)
                {
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
                else if (action == "start" && service.Status != ServiceControllerStatus.Running)
                {
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                return (true, $"  - Serviço '{serviceName}': {action} OK.");
            }
            catch (Exception ex)
            {
                // Retorna uma mensagem de falha mais específica.
                return (false, $"  - Serviço '{serviceName}': FALHA ao {action}. ({ex.Message})");
            }
        }

        /// <summary>
        /// Executa o Verificador de Arquivos do Sistema (SFC) para reparar arquivos corrompidos.
        /// </summary>
        public static void RepairSystemComponentsSFC()
        {
            SystemUtils.RunExternalProcess("cmd.exe", "/c sfc /scannow & pause", hidden: false, waitForExit: false);
        }

        /// <summary>
        /// Executa o DISM (Gerenciamento e Manutenção de Imagens de Implantação) para reparar a imagem do Windows.
        /// </summary>
        public static void RepairSystemComponentsDISM()
        {
            SystemUtils.RunExternalProcess("cmd.exe", "/c DISM /Online /Cleanup-Image /RestoreHealth & pause", hidden: false, waitForExit: false);
        }

        /// <summary>
        /// Reinstala e re-registra todos os aplicativos padrão do Windows (Store, Calculadora, Fotos, etc.).
        /// </summary>
        public static void ReinstallDefaultApps()
        {
            const string command = "Get-AppxPackage -AllUsers | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\"}";
            SystemUtils.RunExternalProcess("cmd.exe", $"/c powershell -ExecutionPolicy Bypass -Command \"{command}\" & pause", hidden: false, waitForExit: false);
        }

        /// <summary>
        /// Reseta a pilha de rede do Windows (TCP/IP e Winsock).
        /// </summary>
        /// <returns>Uma tupla com o status da operação e uma mensagem.</returns>
        public static (bool Success, string Message) ResetNetworkStack()
        {
            try
            {
                SystemUtils.RunExternalProcess("netsh", "winsock reset", hidden: true);
                SystemUtils.RunExternalProcess("netsh", "int ip reset", hidden: true);
                return (true, "A pilha de rede foi resetada. É altamente recomendável reiniciar o computador para que as alterações tenham efeito.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao executar o reset de rede: {ex.Message}");
            }
        }
    }
}