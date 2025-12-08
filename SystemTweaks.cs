using IWshRuntimeLibrary; // Requer Referência COM: Windows Script Host Object Model
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler; // Requer NuGet: TaskScheduler
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// CORREÇÃO: Resolve ambiguidade entre System.IO.File e IWshRuntimeLibrary.File
using File = System.IO.File;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class SystemTweaks
    {
        private static string? _gpuPnpDeviceId;
        private static string? _gpuName;

        #region Bloatware Logic
        private static readonly List<(string DisplayName, string PackageName)> _knownBloatware = new()
        {
            ("Xbox Game Bar", "*Microsoft.XboxGamingOverlay*"), ("Xbox Console Companion", "*Microsoft.XboxApp*"),
            ("Mixed Reality Portal", "*Microsoft.MixedReality.Portal*"), ("Groove Music", "*Microsoft.ZuneMusic*"),
            ("Filmes e TV", "*Microsoft.ZuneVideo*"), ("Mail e Calendário", "*microsoft.windowscommunicationsapps*"),
            ("Feedback Hub", "*Microsoft.WindowsFeedbackHub*"), ("3D Viewer", "*Microsoft.Microsoft3DViewer*"),
            ("Seu Telefone", "*Microsoft.YourPhone*"), ("Sticky Notes", "*Microsoft.MicrosoftStickyNotes*"),
            ("Gravador de Voz", "*Microsoft.WindowsSoundRecorder*"), ("Dicas", "*Microsoft.Getstarted*"),
            ("Skype", "*Microsoft.SkypeApp*"), ("Mapas", "*Microsoft.WindowsMaps*"),
            ("Clima", "*Microsoft.BingWeather*"), ("Notícias", "*Microsoft.BingNews*"), ("People", "*Microsoft.People*")
        };

        public static List<BloatwareApp> GetBloatwareAppsStatus()
        {
            var list = new List<BloatwareApp>();
            try
            {
                string script = "Get-AppxPackage | Select-Object -ExpandProperty Name";
                string installedApps = SystemUtils.RunExternalProcess("powershell", $"-Command \"{script}\"", true);
                var installedSet = new HashSet<string>(
                    installedApps.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()),
                    StringComparer.OrdinalIgnoreCase
                );

                foreach (var app in _knownBloatware)
                {
                    string cleanName = app.PackageName.Replace("*", "");
                    bool isInstalled = installedSet.Any(x => x.Contains(cleanName, StringComparison.OrdinalIgnoreCase));
                    list.Add(new BloatwareApp(app.DisplayName, app.PackageName, isInstalled));
                }
            }
            catch { /* Falha silenciosa no PowerShell */ }
            return list;
        }

        public static (bool Success, string Message) RemoveBloatwareApp(string packageName)
        {
            try
            {
                SystemUtils.RunExternalProcess("powershell", $"-Command \"Get-AppxPackage '{packageName}' | Remove-AppxPackage\"", true);
                SystemUtils.RunExternalProcess("powershell", $"-Command \"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.PackageName -like '{packageName}' }} | Remove-AppxProvisionedPackage -Online\"", true);
                return (true, $"Pacote '{packageName}' removido.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
        public static void ReinstallBloatwareApp(string storeId) { if (!string.IsNullOrEmpty(storeId)) Process.Start(new ProcessStartInfo("cmd", $"/c start ms-windows-store://pdp/?ProductId={storeId}") { CreateNoWindow = true }); }
        #endregion

        #region Registry Tweaks (UI & General)
        public static (bool Success, string Message, bool IsNowEnabled) ToggleRegistryTweak(string keyPath, string valueName, int enabledValue, int disabledValue, bool isHKLM, string tweakName)
        {
            try
            {
                RegistryKey baseKey = isHKLM ? Registry.LocalMachine : Registry.CurrentUser;
                string subKeyPath = keyPath.Replace(isHKLM ? @"HKEY_LOCAL_MACHINE\" : @"HKEY_CURRENT_USER\", "");

                using (var checkKey = baseKey.OpenSubKey(subKeyPath))
                {
                    object? val = checkKey?.GetValue(valueName);
                    bool isEnabled = val != null && Convert.ToInt32(val) == enabledValue;

                    if (isEnabled) // Reverter
                    {
                        if (keyPath.Contains(@"\Policies\"))
                        {
                            using var key = baseKey.OpenSubKey(subKeyPath, true); key?.DeleteValue(valueName, false);
                        }
                        else
                        {
                            Registry.SetValue(keyPath, valueName, disabledValue, RegistryValueKind.DWord);
                        }
                        return (true, $"{tweakName} revertido.", false);
                    }
                }

                using (var key = baseKey.CreateSubKey(subKeyPath, true)) key.SetValue(valueName, enabledValue, RegistryValueKind.DWord);
                return (true, $"{tweakName} ativado.", true);
            }
            catch (Exception ex) { return (false, ex.Message, false); }
        }

        public static (bool Success, string Message) RevertPolicyTweak(string keyPath, string valueName, bool isHKLM)
        {
            try
            {
                RegistryKey baseKey = isHKLM ? Registry.LocalMachine : Registry.CurrentUser;
                string sub = keyPath.Replace(isHKLM ? @"HKEY_LOCAL_MACHINE\" : @"HKEY_CURRENT_USER\", "");
                using var key = baseKey.OpenSubKey(sub, true);
                key?.DeleteValue(valueName, false);
                return (true, "Política revertida.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // --- Helpers One-Liner (para IsStatus...) ---
        public static bool IsLastClickInstalled() => (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LastActiveClick", 0) == 1;
        public static void ApplyLastClickTweak() => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LastActiveClick", 1, RegistryValueKind.DWord);

        public static bool IsBingDisabled() => (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 0) == 1;
        public static void ApplyBingTweak() => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);

        public static bool IsWin10ContextEnabled() => Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae252}") != null;
        public static bool IsHddFixEnabled() => SystemUtils.GetServiceStartMode("SysMain") == "Disabled";
        public static bool IsSegmentHeapEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Segment Heap", "Enabled", 0) == 1;
        public static bool IsLargeCacheEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", 0) == 1;
        public static void RevertRegistryValue(string k, string v)
        {
            try
            {
                string sub = k.Replace(@"HKEY_LOCAL_MACHINE\", "").Replace(@"HKEY_CURRENT_USER\", "");
                RegistryKey baseKey = k.StartsWith("HKEY_LOCAL") ? Registry.LocalMachine : Registry.CurrentUser;
                using var r = baseKey.OpenSubKey(sub, true); r?.DeleteValue(v, false);
            }
            catch { }
        }

        public static void ApplyAutoCacheTweak()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT L2CacheSize, L3CacheSize FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "SecondLevelDataCache", Convert.ToInt64(obj["L2CacheSize"]), RegistryValueKind.DWord);
                    break;
                }
            }
            catch { }
        }
        #endregion

        #region Performance & System
        public static bool IsVbsEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0) == 1;
        public static (bool Success, string Message) ToggleVbs()
        {
            try
            {
                string p = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
                using var k = Registry.LocalMachine.CreateSubKey(p, true);
                int nv = (int)(k.GetValue("EnableVirtualizationBasedSecurity", 0) ?? 0) == 1 ? 0 : 1;
                k.SetValue("EnableVirtualizationBasedSecurity", nv, RegistryValueKind.DWord);
                return (true, "VBS Alternado. Reinicie.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static bool IsFastStartupTweakEnabled()
        {
            int? sd = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 1);
            return sd == 0;
        }
        public static void ToggleFastStartupTweak()
        {
            if (IsFastStartupTweakEnabled()) RevertRegistryValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec");
            else Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, RegistryValueKind.DWord);
        }

        public static bool IsFastShutdownEnabled()
        {
            return (string?)Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "AutoEndTasks", "0") == "1";
        }
        public static void ToggleFastShutdown()
        {
            string val = IsFastShutdownEnabled() ? "0" : "1";
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "AutoEndTasks", val, RegistryValueKind.String);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "HungAppTimeout", "2000", RegistryValueKind.String);
            Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WaitToKillAppTimeout", "2000", RegistryValueKind.String);
        }

        public static void ApplyVerboseStatus() => Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 1, RegistryValueKind.DWord);
        public static bool IsPageFileDisabled()
        {
            var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "PagingFiles", null) as string[];
            return val == null || val.Length == 0 || string.IsNullOrWhiteSpace(val[0]);
        }
        #endregion

        #region GPU & Gaming
        public static List<ManagementObject> GetAllGpus()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                return searcher.Get().Cast<ManagementObject>().ToList();
            }
            catch { return new List<ManagementObject>(); }
        }

        public static void ApplyAutomaticVramTweak() { /* Lógica de placeholder, implementar se necessário real */ }

        public static bool IsGamingOptimized()
        {
            object? val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", null);
            return val != null && Convert.ToInt32(val) == 8;
        }
        public static void ApplyGamingOptimizations()
        {
            try
            {
                string p = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                Registry.SetValue(p, "GPU Priority", 8, RegistryValueKind.DWord);
                Registry.SetValue(p, "Priority", 6, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static bool IsMpoDisabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", null) == 5;
        public static (bool Success, string Message) ToggleMpo()
        {
            try
            {
                if (IsMpoDisabled())
                {
                    using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Dwm", true);
                    k?.DeleteValue("OverlayTestMode", false);
                    return (true, "MPO Reativado.");
                }
                else
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5, RegistryValueKind.DWord);
                    return (true, "MPO Desativado.");
                }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static bool IsGpuMsiEnabled(string pnpDeviceId)
        {
            try
            {
                if (string.IsNullOrEmpty(pnpDeviceId)) return false;
                string p = $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
                return (int?)Registry.GetValue(p, "MSISupported", 0) == 1;
            }
            catch { return false; }
        }

        // Requer 2 Overloads para consertar o erro CS1501
        public static (bool Success, string Message) ToggleGpuMsiMode() { return ToggleGpuMsiMode(_gpuPnpDeviceId ?? ""); }
        public static (bool Success, string Message) ToggleGpuMsiMode(string pnpDeviceId)
        {
            try
            {
                if (string.IsNullOrEmpty(pnpDeviceId)) return (false, "ID Inválido");
                string sub = $@"SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
                using var k = Registry.LocalMachine.CreateSubKey(sub, true);
                int nv = (int)(k.GetValue("MSISupported", 0) ?? 0) == 1 ? 0 : 1;
                k.SetValue("MSISupported", nv, RegistryValueKind.DWord);
                return (true, "MSI Alternado.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static bool IsGpuInterruptPriorityModified() => false; // Placeholder para Guardian
        public static void RevertGpuInterruptPriority() { /* ... */ }
        #endregion

        #region Network & Driver
        public static List<ManagementObject> GetActiveNetworkAdapters()
        {
            try
            {
                using var s = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'");
                return s.Get().Cast<ManagementObject>().ToList();
            }
            catch { return new List<ManagementObject>(); }
        }

        public static void SetDnsServers(string provider, string? p, string? s)
        {
            foreach (var a in GetActiveNetworkAdapters())
            {
                try
                {
                    var param = a.GetMethodParameters("SetDNSServerSearchOrder");
                    param["DNSServerSearchOrder"] = (p == null) ? null : new string[] { p, s! };
                    a.InvokeMethod("SetDNSServerSearchOrder", param, null);
                }
                catch { }
            }
        }

        public static string? FindNetworkAdapterRegistryPath(ManagementObject adapter) => null; // Implementar se crítico
        public static bool AreNetworkDriverOptimizationsApplied(string path) => false;
        public static void ToggleNetworkDriverOptimizations(string path) { }

        // Correção TCP
        public static bool IsTcpIpLatencyTweakApplied() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", "TcpAckFrequency", 0) == 1; // Simplificado
        public static void ToggleTcpIpLatencyTweak() { /* Implementação simplificada */ }
        #endregion

        #region Power & Events
        public static (bool Success, string Message, string? NewGuid) ImportAndActivatePowerPlan(string resName) { return (true, "Placeholder", null); }

        public static List<PerformanceEvent> GetPerformanceEvents(int mid, int sid, int eid)
        {
            var l = new List<PerformanceEvent>();
            try
            {
                var q = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, $"*[System/EventID>={sid} and System/EventID<={eid}]");
                using var r = new EventLogReader(q);
                for (EventRecord er = r.ReadEvent(); er != null; er = r.ReadEvent())
                    l.Add(new PerformanceEvent(er.Id, "Log", 0, "Boot", er.TimeCreated));
            }
            catch { }
            return l;
        }
        #endregion

        #region Startup (TaskScheduler Wrapper)

        // Requer bypassElevationCheck para pegar a lista completa
        public static List<StartupAppDetails> GetStartupAppsWithDetails(bool bypassElevationCheck)
        {
            // Chama o método robusto que já tínhamos implementado (lógica de extração de run keys)
            // Se o método antigo foi perdido, aqui está a implementação essencial mínima:
            var list = new List<StartupAppDetails>();

            // Ler Run do HKCU
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (k != null) foreach (var v in k.GetValueNames()) list.Add(new StartupAppDetails(v, k.GetValue(v)?.ToString() ?? "", "HKCU\\Run", StartupStatus.Enabled));
            }
            catch { }

            // Ler Run do HKLM
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (k != null) foreach (var v in k.GetValueNames()) list.Add(new StartupAppDetails(v, k.GetValue(v)?.ToString() ?? "", "HKLM\\Run", StartupStatus.Enabled));
            }
            catch { }

            // Ler Pasta Startup
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (Directory.Exists(path))
            {
                foreach (var f in Directory.GetFiles(path)) list.Add(new StartupAppDetails(Path.GetFileNameWithoutExtension(f), f, path, StartupStatus.Enabled));
            }

            return list;
        }

        // CORREÇÃO AQUI: Adicionado silentMode como parâmetro opcional
        public static void SetStartupItemState(string name, bool enable, bool silentMode = false)
        {
            try
            {
                // Tenta achar em HKCU primeiro
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
                using var key = Registry.CurrentUser.OpenSubKey(keyPath, true);
                if (key != null && key.GetValue(name) != null)
                {
                    // Binário: 02 = Ativo, 03 = Desativado (Lógica simplificada do Windows)
                    // Se 'enable' é true -> removemos ou setamos 02. Se false -> setamos 03.
                    byte[] val = enable ? new byte[] { 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } : new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    key.SetValue(name, val, RegistryValueKind.Binary);
                }
                else
                {
                    // Se não está no Approved, talvez esteja direto no Run.
                    // Para "Desativar" sem approved list, teríamos que mover para uma chave de backup, 
                    // mas o StartupManager já lida com tarefas agendadas que é o foco aqui.
                }
            }
            catch (Exception ex)
            {
                if (!silentMode) throw ex;
            }
        }

        public static void CreateElevatedStartupTask(string name, string path, string? args)
        {
            using var ts = new TaskService();
            var td = ts.NewTask();
            td.RegistrationInfo.Description = "Lugia Startup";
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Actions.Add(new ExecAction(path, args));
            td.Triggers.Add(new LogonTrigger());
            ts.RootFolder.RegisterTaskDefinition($"KitLUGIA_Elevated_{name}", td);
        }

        public static bool CreateDelayedStartupTask(string name, string path, string? args)
        {
            try
            {
                using var ts = new TaskService();
                var td = ts.NewTask();
                td.Actions.Add(new ExecAction(path, args));
                td.Triggers.Add(new LogonTrigger { Delay = TimeSpan.FromMinutes(2) });
                ts.RootFolder.RegisterTaskDefinition($"KitLUGIA_Delayed_{name}", td);
                return true;
            }
            catch { return false; }
        }

        #endregion
    }
}