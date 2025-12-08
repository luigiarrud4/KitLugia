using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class Guardian
    {
        #region Definições de Tweaks

        // Lista de tweaks que, se modificados do padrão, podem comprometer a segurança ou estabilidade.
        private static readonly List<ScannableTweak> HarmfulTweaks = new()
        {
            // Categoria: Segurança Crítica
            new() { Name = "Mitigações de CPU (Spectre/Meltdown)", Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "FeatureSettingsOverride", HarmfulValue = 3, DefaultValue = 0 },
            new() { Name = "Control Flow Guard (CFG)", Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "EnableCfg", HarmfulValue = 0, DefaultValue = 1 },
            new() { Name = "Controle de Conta de Usuário (UAC)", Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "EnableLUA", HarmfulValue = 0, DefaultValue = 1 },
            new() { Name = "Proteção de Execução de Dados (DEP)", Category = "Segurança Crítica", Type = TweakType.Bcd, ValueName = "nx", HarmfulValue = "AlwaysOff", DefaultValue = "OptIn" },

            // Categoria: Estabilidade e Diagnósticos
            new() { Name = "Arquivo de Paginação (Page File)", Category = "Estabilidade e Diagnósticos", Type = TweakType.PageFile },
            new() { Name = "Hibernação (e Fast Startup)", Category = "Estabilidade e Diagnósticos", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", ValueName = "HibernateEnabled", HarmfulValue = 0, DefaultValue = 1 },
            new() { Name = "Serviço de Log de Eventos do Windows", Category = "Estabilidade e Diagnósticos", Type = TweakType.Service, ServiceName = "eventlog", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"},

            // Categoria: Serviços Essenciais
            new() { Name = "Serviço de Chamada Remota (RPC)", Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "RpcSs", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"},
            new() { Name = "Mecanismo de Filtragem Base (BFE)", Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "BFE", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"},
            new() { Name = "Serviço de Áudio do Windows", Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "AudioSrv", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto" },
            new() { Name = "Cliente DNS", Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "Dnscache", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto" },

            // Categoria: Atualizações e Defesa
            new() { Name = "Windows Update", Category = "Atualizações e Defesa", Type = TweakType.Service, ServiceName = "wuauserv", HarmfulStartMode = "Disabled", DefaultStartMode = "Manual" }, // Padrão é Manual
            new() { Name = "Firewall do Windows", Category = "Atualizações e Defesa", Type = TweakType.Service, ServiceName = "MpsSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto" },
            new() { Name = "Antivírus Windows Defender", Category = "Atualizações e Defesa", Type = TweakType.Service, ServiceName = "WinDefend", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto" },

            // Categoria: Outros
            new() { Name = "Aceleração de Ponteiro do Mouse", Category = "Outros", Type = TweakType.Mouse }
        };

        // Lista de tweaks que, se estiverem no estado "nocivo", significam que o sistema NÃO está otimizado.
        private static readonly List<ScannableTweak> OptimizationTweaks = new()
        {
            new() { Name = "Acelerar Resposta Visual dos Menus", Category = "Performance e UI", KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop", ValueName = "MenuShowDelay", HarmfulValue = "400", DefaultValue = "100", ValueKind = RegistryValueKind.String },
            new() { Name = "Remover Atraso na Inicialização de Apps", Category = "Performance e UI", KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", ValueName = "StartupDelayInMSec", HarmfulValue = null, DefaultValue = 0, ValueKind = RegistryValueKind.DWord },
            new() { Name = "Desativar Game DVR e Game Bar", Category = "Performance", KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore", ValueName = "GameDVR_Enabled", HarmfulValue = 1, DefaultValue = 0 },
            new() { Name = "Desativar Throttling de Rede", Category = "Performance", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", ValueName = "NetworkThrottlingIndex", HarmfulValue = 10, DefaultValue = unchecked((int)0xFFFFFFFF) },
            new() { Name = "Desativar SysMain (Superfetch)", Category = "Performance", Type = TweakType.Service, ServiceName = "SysMain", HarmfulStartMode = "Auto", DefaultStartMode = "Disabled" },
            new() { Name = "Priorizar Tarefas de Jogos (GPU)", Category = "Performance", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", ValueName = "GPU Priority", HarmfulValue = 6, DefaultValue = 8 }, // Valor padrão do Windows é 6, otimizado é 8
            new() { Name = "Bloquear 'Experiências do Consumidor'", Category = "Interface e Bloatware", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent", ValueName = "DisableWindowsConsumerFeatures", HarmfulValue = 0, DefaultValue = 1 },
            new() { Name = "Desativar ID de Anúncio do Usuário", Category = "Privacidade", KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", ValueName = "Enabled", HarmfulValue = 1, DefaultValue = 0 },
            new() { Name = "Desativar Serviço de Telemetria", Category = "Privacidade", Type = TweakType.Service, ServiceName = "DiagTrack", HarmfulStartMode = "Auto", DefaultStartMode = "Disabled" },
            new() { Name = "Resolver Vazamento de Memória (NDU)", Category = "Manutenção", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Ndu", ValueName = "Start", HarmfulValue = 2, DefaultValue = 4 } // Padrão é 2 (Auto), desativado (bom) é 4
        };

        #endregion

        #region API Pública para a GUI

        public static List<ScannableTweak> GetHarmfulTweaksWithStatus()
        {
            HarmfulTweaks.ForEach(CheckTweak);
            return HarmfulTweaks;
        }

        public static List<ScannableTweak> GetOptimizationTweaksWithStatus()
        {
            OptimizationTweaks.ForEach(CheckTweak);
            return OptimizationTweaks;
        }

        public static (bool Success, string Message) ToggleTweak(ScannableTweak tweak)
        {
            try
            {
                bool applyDefaultValue = tweak.Status == TweakStatus.MODIFIED;
                string action = applyDefaultValue ? "restaurado/otimizado" : "revertido/desotimizado";

                switch (tweak.Type)
                {
                    case TweakType.Registry:
                        if (string.IsNullOrEmpty(tweak.KeyPath) || string.IsNullOrEmpty(tweak.ValueName)) return (false, "Registro inválido.");
                        string path = tweak.KeyPath.Replace(@"HKEY_LOCAL_MACHINE\", "").Replace(@"HKEY_CURRENT_USER\", "");
                        RegistryKey baseKey = tweak.KeyPath.StartsWith("HKEY_LOCAL_MACHINE") ? Registry.LocalMachine : Registry.CurrentUser;
                        using (RegistryKey? key = baseKey.OpenSubKey(path, true) ?? baseKey.CreateSubKey(path))
                        {
                            object? valueToSet = applyDefaultValue ? tweak.DefaultValue : tweak.HarmfulValue;
                            if (valueToSet == null)
                            {
                                if (key.GetValue(tweak.ValueName) != null) key.DeleteValue(tweak.ValueName, false);
                            }
                            else
                            {
                                key.SetValue(tweak.ValueName, valueToSet, tweak.ValueKind);
                            }
                        }
                        break;

                    case TweakType.Service:
                        if (string.IsNullOrEmpty(tweak.ServiceName) || string.IsNullOrEmpty(tweak.DefaultStartMode) || string.IsNullOrEmpty(tweak.HarmfulStartMode)) return (false, "Serviço inválido.");
                        string mode = applyDefaultValue ? tweak.DefaultStartMode : tweak.HarmfulStartMode;
                        SystemUtils.RunExternalProcess("sc.exe", $"config {tweak.ServiceName} start={mode.ToLower()}", true);
                        break;

                    case TweakType.Mouse:
                        bool setHarmful = !applyDefaultValue;
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", setHarmful ? "1" : "0", RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", setHarmful ? "6" : "0", RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", setHarmful ? "10" : "0", RegistryValueKind.String);
                        break;

                    case TweakType.Bcd:
                        if (string.IsNullOrEmpty(tweak.ValueName) || tweak.DefaultValue == null || tweak.HarmfulValue == null) return (false, "BCD inválido.");
                        string value = (applyDefaultValue ? tweak.DefaultValue.ToString() : tweak.HarmfulValue.ToString()) ?? "";
                        SystemUtils.RunExternalProcess("bcdedit", $"/set {tweak.ValueName} {value}", true);
                        break;

                    case TweakType.PageFile:
                        const string pageFileKeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
                        var pageFileValue = applyDefaultValue ? new string[] { @"?:\pagefile.sys" } : Array.Empty<string>();
                        Registry.SetValue(pageFileKeyPath, "PagingFiles", pageFileValue, RegistryValueKind.MultiString);
                        break;
                }

                CheckTweak(tweak); // Re-verifica o status após a alteração.
                return (true, $"'{tweak.Name}' foi {action} com sucesso.");
            }
            catch (Exception ex)
            {
                return (false, $"ERRO ao alterar '{tweak.Name}': {ex.Message}");
            }
        }

        #endregion

        #region Lógica Interna de Verificação

        private static void CheckTweak(ScannableTweak tweak)
        {
            try
            {
                switch (tweak.Type)
                {
                    case TweakType.Registry:
                        if (string.IsNullOrEmpty(tweak.KeyPath) || string.IsNullOrEmpty(tweak.ValueName)) { tweak.Status = TweakStatus.ERROR; return; }
                        object? currentValue = Registry.GetValue(tweak.KeyPath, tweak.ValueName, null);
                        bool isHarmful;
                        if (tweak.HarmfulValue == null) isHarmful = (currentValue != null);
                        else
                        {
                            if (currentValue == null) isHarmful = false; // Valor não existe, assume que não é nocivo.
                            else if (currentValue is int or long) isHarmful = Convert.ToInt64(currentValue) == Convert.ToInt64(tweak.HarmfulValue);
                            else isHarmful = currentValue.ToString()?.Equals(tweak.HarmfulValue.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
                        }
                        tweak.Status = isHarmful ? TweakStatus.MODIFIED : TweakStatus.OK;
                        break;

                    case TweakType.Service:
                        if (string.IsNullOrEmpty(tweak.ServiceName) || string.IsNullOrEmpty(tweak.HarmfulStartMode)) { tweak.Status = TweakStatus.ERROR; return; }
                        string? startMode = SystemUtils.GetServiceStartMode(tweak.ServiceName);
                        if (startMode == null) { tweak.Status = TweakStatus.NOT_FOUND; return; }
                        tweak.Status = startMode.Equals(tweak.HarmfulStartMode, StringComparison.OrdinalIgnoreCase) ? TweakStatus.MODIFIED : TweakStatus.OK;
                        break;

                    case TweakType.Mouse:
                        string speed = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "0")?.ToString() ?? "0";
                        string thresh1 = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", "0")?.ToString() ?? "0";
                        tweak.Status = (speed != "0" || thresh1 != "0") ? TweakStatus.MODIFIED : TweakStatus.OK;
                        break;

                    case TweakType.Bcd:
                        if (string.IsNullOrEmpty(tweak.ValueName) || tweak.HarmfulValue == null) { tweak.Status = TweakStatus.ERROR; return; }
                        string output = SystemUtils.RunExternalProcess("bcdedit", "/v", true);
                        var line = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(l => l.Trim().StartsWith(tweak.ValueName));
                        tweak.Status = (line != null && line.EndsWith(tweak.HarmfulValue.ToString() ?? "", StringComparison.OrdinalIgnoreCase)) ? TweakStatus.MODIFIED : TweakStatus.OK;
                        break;

                    case TweakType.PageFile:
                        var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "PagingFiles", null) as string[];
                        tweak.Status = (val == null || val.Length == 0) ? TweakStatus.MODIFIED : TweakStatus.OK;
                        break;
                }
            }
            catch { tweak.Status = TweakStatus.ERROR; }
        }

        #endregion
    }
}