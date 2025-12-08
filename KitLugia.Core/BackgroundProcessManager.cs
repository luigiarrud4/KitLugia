using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management; // Necessário para ler serviços detalhados (WMI)
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class BackgroundProcessManager
    {
        // Lista de serviços que sabemos que são seguros para desativar (Otimização)
        private static readonly HashSet<string> _safeToDisable = new(StringComparer.OrdinalIgnoreCase)
        {
            "DiagTrack", "dmwappushservice", "SysMain", "WSearch", "MapsBroker", "lfsvc", "Fax", "RetailDemo",
            "XblGameSave", "XboxNetApiSvc", "XboxGipSvc", "XblAuthManager", "WerSvc", "PcaSvc", "DPS", "WdiServiceHost",
            "PrintWorkflow", "Spooler", "W32Time", "RemoteRegistry", "WalletService", "NcdAutoSetup", "SharedAccess",
            "TouchKeyboard", "TabletInputService"
        };

        // Lista de serviços CRÍTICOS (Se desativar, o Windows pode quebrar/tela azul/sem internet)
        private static readonly HashSet<string> _criticalServices = new(StringComparer.OrdinalIgnoreCase)
        {
            "RpcSs", "DcomLaunch", "RpcEptMapper", "LSM", "gpsvc", "WinDefend", "Audiosrv", "Dhcp", "Dnscache",
            "EventLog", "lmhosts", "MpsSvc", "nsi", "Power", "ProfSvc", "SamSs", "Schedule", "SENS", "ShellHWDetection",
            "SystemEventsBroker", "Themes", "UserManager", "Winmgmt", "WpnService", "BFE", "CryptSvc", "PlugPlay"
        };

        /// <summary>
        /// Obtém TODOS os serviços do Windows com detalhes ricos.
        /// </summary>
        public static List<ServiceInfo> GetAllServices()
        {
            var services = new List<ServiceInfo>();

            try
            {
                var query = "SELECT Name, DisplayName, Description, State, StartMode FROM Win32_Service";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (var item in searcher.Get().Cast<ManagementObject>())
                {
                    string name = item["Name"]?.ToString() ?? ""; // Nome Técnico (ex: wuauserv)
                    string display = item["DisplayName"]?.ToString() ?? ""; // Nome Amigável (ex: Windows Update)
                    string desc = item["Description"]?.ToString() ?? "Sem descrição disponível.";
                    string state = item["State"]?.ToString() ?? "Unknown";
                    string startMode = item["StartMode"]?.ToString() ?? "Manual";

                    ServiceSafetyLevel safety = ServiceSafetyLevel.Unknown;

                    if (_criticalServices.Contains(name))
                        safety = ServiceSafetyLevel.Dangerous;
                    else if (_safeToDisable.Contains(name))
                        safety = ServiceSafetyLevel.Safe;
                    else
                        safety = ServiceSafetyLevel.Caution;

                    string uiStatus = state == "Running" ? "Executando" : "Parado";
                    string uiStart = startMode == "Auto" ? "Automático" : (startMode == "Manual" ? "Manual" : "Desativado");

                    services.Add(new ServiceInfo(name, display, desc, uiStatus, uiStart, safety));
                }
            }
            catch { }

            return services
                .OrderBy(s => s.Safety)
                .ThenBy(s => s.DisplayName)
                .ToList();
        }

        public static (bool Success, string Message) ToggleServiceState(string serviceName, string newMode)
        {
            try
            {
                string cmd = $"config \"{serviceName}\" start= {newMode}";
                string result = SystemUtils.RunExternalProcess("sc.exe", cmd, true);

                // --- CORREÇÃO AQUI ---
                // Verifica "XITO" para cobrir tanto "ÊXITO" quanto "XITO" (erro de encoding)
                if (result.Contains("sucesso", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase) ||
                    result.Contains("XITO", StringComparison.OrdinalIgnoreCase))
                {
                    if (newMode == "disabled") SystemUtils.RunExternalProcess("sc.exe", $"stop \"{serviceName}\"", true);

                    if (newMode == "auto") SystemUtils.RunExternalProcess("sc.exe", $"start \"{serviceName}\"", true);

                    // Formata a mensagem para ficar bonita na notificação
                    string modePt = newMode == "auto" ? "Automático" : (newMode == "demand" ? "Manual" : "Desativado");
                    return (true, $"Serviço definido como {modePt} com sucesso.");
                }
                else
                {
                    return (false, $"Erro ao configurar: {result}");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool Success, string Message) ResetServiceToDefault(string serviceName)
        {
            string mode = "demand";

            if (_criticalServices.Contains(serviceName) || _safeToDisable.Contains(serviceName))
            {
                mode = "auto";
                if (serviceName == "XblGameSave" || serviceName == "Fax" || serviceName == "WerSvc" || serviceName == "RetailDemo")
                    mode = "demand";
            }

            return ToggleServiceState(serviceName, mode);
        }

        public static (bool Success, string Message) ApplyServicePreset(string presetName)
        {
            List<string> targets = new();
            string mode = "disabled";

            if (presetName == "Safe")
            {
                targets.AddRange(new[] { "Fax", "RetailDemo", "Spooler", "PrintWorkflow" });
            }
            else if (presetName == "Gamer")
            {
                targets.AddRange(_safeToDisable);
            }
            else if (presetName == "Restore")
            {
                mode = "auto";
                targets.AddRange(_safeToDisable);
            }

            int successCount = 0;
            foreach (var svc in targets)
            {
                string currentMode = mode;
                if (presetName == "Restore" && (svc == "XblGameSave" || svc == "Fax" || svc == "WerSvc"))
                    currentMode = "demand";

                if (ToggleServiceState(svc, currentMode).Success) successCount++;
            }

            return (true, $"{successCount} serviços foram processados no perfil '{presetName}'.");
        }
    }
}