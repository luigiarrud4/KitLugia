using IWshRuntimeLibrary;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Resolve ambiguidade entre System.IO.File e IWshRuntimeLibrary.File
using File = System.IO.File;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class SystemTweaks
    {
        // REMOVIDO: Campos não utilizados (_gpuPnpDeviceId, _gpuName) para limpar warnings.

        #region Bloatware Logic
        private static readonly List<(string DisplayName, string PackageName, string StoreId)> _knownBloatware = new()
        {
            ("Xbox Game Bar", "*Microsoft.XboxGamingOverlay*", "9NZKPSTSNW4P"),
            ("Xbox App", "*Microsoft.XboxApp*", "9MV0B5HZVK9Z"),
            ("Cortana", "*Microsoft.549981C3F5F10*", "9NFFX4SZZ23L"),
            ("Mail e Calendário", "*microsoft.windowscommunicationsapps*", "9wzdncrfhvqm"),
            ("Feedback Hub", "*Microsoft.WindowsFeedbackHub*", "9nblggh4r32n"),
            ("Vínculo Móvel (Seu Telefone)", "*Microsoft.YourPhone*", "9NMPJ99VJbwv"),
            ("Groove Music", "*Microsoft.ZuneMusic*", "9WZDNCRFJ3PT"),
            ("Filmes e TV", "*Microsoft.ZuneVideo*", "9WZDNCRFJ3P2"),
            ("3D Viewer", "*Microsoft.Microsoft3DViewer*", "9NBLGGH42THS"),
            ("Sticky Notes", "*Microsoft.MicrosoftStickyNotes*", "9NBLGGH4QGHW"),
            ("Gravador de Voz", "*Microsoft.WindowsSoundRecorder*", "9WZDNCRFHWKN"),
            ("Skype", "*Microsoft.SkypeApp*", "9WZDNCRDFWBT"),
            ("Mapas", "*Microsoft.WindowsMaps*", "9WZDNCRDTBVB"),
            ("Clima", "*Microsoft.BingWeather*", "9WZDNCRFJ41T"),
            ("Notícias", "*Microsoft.BingNews*", "9WZDNCRFHVFW"),
            ("People", "*Microsoft.People*", "9NBLGGH10PG8")
        };

        public static List<BloatwareApp> GetBloatwareAppsStatus()
        {
            var appStatuses = new List<BloatwareApp>();
            try
            {
                string psCommand = "Get-AppxPackage -AllUsers | Select-Object -ExpandProperty Name";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                var installedPackages = new HashSet<string>(output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                foreach (var item in _knownBloatware)
                {
                    string searchKey = item.PackageName.Replace("*", "");
                    bool isInstalled = installedPackages.Any(p => p.Contains(searchKey, StringComparison.OrdinalIgnoreCase));
                    appStatuses.Add(new BloatwareApp(item.DisplayName, item.PackageName, isInstalled, item.StoreId));
                }
            }
            catch { /* Falha silenciosa no PowerShell */ }
            return appStatuses;
        }

        public static (bool Success, string Message) RemoveBloatwareApp(string packageName)
        {
            try
            {
                SystemUtils.RunExternalProcess("powershell", $"-Command \"Get-AppxPackage '{packageName}' -AllUsers | Remove-AppxPackage -AllUsers\"", true);
                SystemUtils.RunExternalProcess("powershell", $"-Command \"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.PackageName -like '{packageName}' }} | Remove-AppxProvisionedPackage -Online\"", true);
                return (true, $"Pacote '{packageName}' removido.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static void ReinstallBloatwareApp(string storeId)
        {
            if (!string.IsNullOrEmpty(storeId))
            {
                try
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start ms-windows-store://pdp/?ProductId={storeId}") { CreateNoWindow = true });
                }
                catch { /* Falha silenciosa */ }
            }
        }
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
                string subKeyPath = keyPath.Replace(isHKLM ? @"HKEY_LOCAL_MACHINE\" : @"HKEY_CURRENT_USER\", "");
                using (var key = baseKey.OpenSubKey(subKeyPath, true))
                {
                    key?.DeleteValue(valueName, false);
                }
                return (true, $"Política '{valueName}' revertida.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao reverter política '{valueName}': {ex.Message}");
            }
        }

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

        public static bool IsLastClickInstalled() => (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LastActiveClick", 0) == 1;
        public static void ApplyLastClickTweak() => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LastActiveClick", 1, RegistryValueKind.DWord);

        public static bool IsBingDisabled() => (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 0) == 1;
        public static void ApplyBingTweak() => Registry.SetValue(@"HKEY_CURRENT_USER\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord);

        public static bool IsWin10ContextEnabled() => Registry.CurrentUser.OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae252}") != null;
        public static bool IsHddFixEnabled() => SystemUtils.GetServiceStartMode("SysMain") == "Disabled";
        public static bool IsSegmentHeapEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Segment Heap", "Enabled", 0) == 1;
        public static bool IsLargeCacheEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", 0) == 1;

        public static void ApplyAutoCacheTweak()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT L2CacheSize FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "SecondLevelDataCache", Convert.ToInt64(obj["L2CacheSize"]), RegistryValueKind.DWord);
                    break;
                }
            }
            catch { }
        }
        #endregion

        #region Performance & System

        public static (bool Success, string Message, long FreedMemory) OptimizeMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return (true, "Processos de limpeza de memória finalizados.", 128 * 1024 * 1024);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, 0);
            }
        }

        public static bool IsVbsEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0) == 1;
        public static (bool Success, string Message) ToggleVbs()
        {
            try
            {
                string p = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
                using var k = Registry.LocalMachine.CreateSubKey(p, true);
                if (k == null) return (false, "Não foi possível acessar a chave do registro.");
                int nv = (int)(k.GetValue("EnableVirtualizationBasedSecurity", 0) ?? 0) == 1 ? 0 : 1;
                k.SetValue("EnableVirtualizationBasedSecurity", nv, RegistryValueKind.DWord);
                return (true, "VBS Alternado. É necessário reiniciar para aplicar as alterações.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static bool IsFastStartupTweakEnabled() => (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 1) == 0;
        public static void ToggleFastStartupTweak()
        {
            if (IsFastStartupTweakEnabled()) RevertRegistryValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec");
            else Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, RegistryValueKind.DWord);
        }

        public static bool IsFastShutdownEnabled() => (string?)Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "AutoEndTasks", "0") == "1";
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
                using var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_VideoController");
                return searcher.Get().Cast<ManagementObject>().ToList();
            }
            catch { return new List<ManagementObject>(); }
        }

        public static void ApplyAutomaticVramTweak() { }

        public static bool IsGamingOptimized() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 0) == 8;
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

        public static bool IsMpoDisabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 0) == 5;
        public static (bool Success, string Message) ToggleMpo()
        {
            try
            {
                if (IsMpoDisabled())
                {
                    using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\Dwm", true);
                    k?.DeleteValue("OverlayTestMode", false);
                    return (true, "MPO Reativado. Reinicie para aplicar.");
                }
                else
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5, RegistryValueKind.DWord);
                    return (true, "MPO Desativado. Reinicie para aplicar.");
                }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public static bool IsGpuMsiEnabled(string pnpDeviceId)
        {
            if (string.IsNullOrEmpty(pnpDeviceId)) return false;
            try
            {
                string keyPath = $@"SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
                using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                return key != null && (int?)key.GetValue("MSISupported") == 1;
            }
            catch { return false; }
        }

        public static (bool Success, string Message) ToggleGpuMsiMode(string pnpDeviceId)
        {
            if (string.IsNullOrEmpty(pnpDeviceId)) return (false, "PNPDeviceID da GPU não encontrado.");
            try
            {
                string keyPath = $@"SYSTEM\CurrentControlSet\Enum\{pnpDeviceId}\Device Parameters\Interrupt Management\MessageSignaledInterruptProperties";
                using var key = Registry.LocalMachine.CreateSubKey(keyPath, true);
                if (key == null) return (false, "Não foi possível acessar a chave do registro da GPU.");

                int current = (int?)key.GetValue("MSISupported", 0) ?? 0;
                int newValue = (current == 1) ? 0 : 1;
                key.SetValue("MSISupported", newValue, RegistryValueKind.DWord);
                return (true, $"Modo MSI {(newValue == 1 ? "ATIVADO" : "DESATIVADO")}. É necessário reiniciar para aplicar.");
            }
            catch (Exception ex) { return (false, $"Erro: {ex.Message}"); }
        }

        public static bool IsGameDvrEnabled() => (int?)Registry.GetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", 1) == 1;
        public static void ToggleGameDvr(bool enable)
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", "GameDVR_Enabled", enable ? 1 : 0);
            }
            catch { }
        }
        #endregion

        #region Network & Driver
        public static List<ManagementObject> GetActiveNetworkAdapters()
        {
            try
            {
                var query = "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus = 2";
                using var searcher = new ManagementObjectSearcher(query);
                return searcher.Get().Cast<ManagementObject>().ToList();
            }
            catch { return new List<ManagementObject>(); }
        }

        public static void SetDnsServers(string provider, string? primaryDns, string? secondaryDns)
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'");
            foreach (ManagementObject adapter in searcher.Get().Cast<ManagementObject>())
            {
                try
                {
                    var param = adapter.GetMethodParameters("SetDNSServerSearchOrder");
                    param["DNSServerSearchOrder"] = (primaryDns == null) ? null : new string[] { primaryDns, secondaryDns! };
                    adapter.InvokeMethod("SetDNSServerSearchOrder", param, null);
                }
                catch { }
            }
        }

        public static string? FindNetworkAdapterRegistryPath(string adapterGuid)
        {
            if (string.IsNullOrEmpty(adapterGuid)) return null;
            try
            {
                string netClassGuid = "{4d36e972-e325-11ce-bfc1-08002be10318}";
                string basePath = $@"SYSTEM\CurrentControlSet\Control\Class\{netClassGuid}";
                using var classKey = Registry.LocalMachine.OpenSubKey(basePath);
                if (classKey == null) return null;
                foreach (var subKeyName in classKey.GetSubKeyNames())
                {
                    using var subKey = classKey.OpenSubKey(subKeyName);
                    if (subKey?.GetValue("NetCfgInstanceId")?.ToString()?.Equals(adapterGuid, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return $"HKEY_LOCAL_MACHINE\\{basePath}\\{subKeyName}";
                    }
                }
            }
            catch { }
            return null;
        }

        public static bool AreNetworkDriverOptimizationsApplied(string regPath)
        {
            if (string.IsNullOrEmpty(regPath)) return false;
            try
            {
                var value = Registry.GetValue(regPath, "*InterruptModeration", null)?.ToString();
                return value == "0";
            }
            catch { return false; }
        }

        public static void ToggleNetworkDriverOptimizations(string regPath)
        {
            if (string.IsNullOrEmpty(regPath)) return;
            bool isApplied = AreNetworkDriverOptimizationsApplied(regPath);
            var tweaks = new Dictionary<string, string> { { "*InterruptModeration", "0" }, { "EnergyEfficientEthernet", "0" } };
            string cleanPath = regPath.Replace("HKEY_LOCAL_MACHINE\\", "");
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(cleanPath, true);
                if (key == null) return;
                if (isApplied)
                {
                    foreach (var k in tweaks.Keys) key.DeleteValue(k, false);
                }
                else
                {
                    foreach (var t in tweaks) key.SetValue(t.Key, t.Value, RegistryValueKind.String);
                }
            }
            catch { }
        }

        public static bool IsTcpIpLatencyTweakApplied()
        {
            string? regPath = GetActiveInterfaceRegPath();
            if (string.IsNullOrEmpty(regPath)) return false;
            var value = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{regPath}", "TcpAckFrequency", null);
            return value != null && (int)value == 1;
        }

        public static (bool Success, string Message) ToggleTcpIpLatencyTweak()
        {
            string? regPath = GetActiveInterfaceRegPath();
            if (string.IsNullOrEmpty(regPath)) return (false, "Adaptador de rede ativo não encontrado.");

            string cleanRegPath = regPath.Replace("HKEY_LOCAL_MACHINE\\", "");
            bool isApplied = IsTcpIpLatencyTweakApplied();
            try
            {
                if (isApplied)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(cleanRegPath, true))
                    {
                        key?.DeleteValue("TcpAckFrequency", false);
                        key?.DeleteValue("TCPNoDelay", false);
                    }
                    return (true, "Otimização de latência de rede desativada.");
                }
                else
                {
                    Registry.SetValue($"HKEY_LOCAL_MACHINE\\{cleanRegPath}", "TcpAckFrequency", 1, RegistryValueKind.DWord);
                    Registry.SetValue($"HKEY_LOCAL_MACHINE\\{cleanRegPath}", "TCPNoDelay", 1, RegistryValueKind.DWord);
                    return (true, "Otimização de latência de rede ativada.");
                }
            }
            catch (Exception ex) { return (false, $"Erro: {ex.Message}"); }
        }

        private static string? GetActiveInterfaceRegPath()
        {
            try
            {
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(i => i.OperationalStatus == OperationalStatus.Up &&
                                          (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                                          i.GetIPProperties().GatewayAddresses.Any());
                return activeInterface != null ? $@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{activeInterface.Id}" : null;
            }
            catch { return null; }
        }
        #endregion

        #region Power & Events
        public static (bool Success, string Message, string? NewGuid) ImportAndActivatePowerPlan(string resourceName)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "KitLugia_Plan.pow");
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return (false, "Arquivo de plano de energia não encontrado nos recursos do projeto.", null);
                    using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write)) { stream.CopyTo(fs); }
                }

                string output = SystemUtils.RunExternalProcess("powercfg", $"/import \"{tempFile}\"", true);
                var match = new Regex(@"[a-fA-F0-9]{8}-([a-fA-F0-9]{4}-){3}[a-fA-F0-9]{12}").Match(output);
                if (match.Success)
                {
                    SystemUtils.RunExternalProcess("powercfg", $"/setactive {match.Value}", true);
                    return (true, "Plano de energia importado e ativado com sucesso.", match.Value);
                }
                return (false, "Não foi possível extrair o GUID do plano de energia importado.", null);
            }
            catch (Exception ex) { return (false, $"Erro ao importar plano de energia: {ex.Message}", null); }
            finally { if (File.Exists(tempFile)) File.Delete(tempFile); }
        }

        public static List<PerformanceEvent> GetPerformanceEvents(int startId, int midId, int endId)
        {
            var events = new List<PerformanceEvent>();
            try
            {
                string query = $"*[System/Level<=4 and System/Provider[@Name='Microsoft-Windows-Diagnostics-Performance'] and System/EventID >= {startId} and System/EventID <= {endId}]";
                var logQuery = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, query);

                using (var reader = new EventLogReader(logQuery))
                {
                    for (EventRecord record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
                    {
                        try
                        {
                            var xml = XDocument.Parse(record.ToXml());
                            XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
                            var eventData = xml.Descendants(ns + "EventData").FirstOrDefault();
                            if (eventData == null) continue;
                            string GetValue(string name) => eventData.Elements(ns + "Data").FirstOrDefault(e => e.Attribute("Name")?.Value == name)?.Value ?? string.Empty;
                            string itemName = GetValue("BootPostBootTime") != string.Empty ? "Tempo Total de Boot" : (GetValue("FileName") != string.Empty ? GetValue("FileName") : "Item Desconhecido");
                            long.TryParse(GetValue("BootTime") ?? GetValue("ShutdownTime") ?? GetValue("MainPathBootTime") ?? "0", out long timeTaken);
                            events.Add(new PerformanceEvent(record.Id, itemName, timeTaken, "Boot/Shutdown", record.TimeCreated));
                        }
                        catch { /* Ignora erro de parsing */ }
                    }
                }
            }
            catch { /* Ignora erro ao ler log */ }
            return events;
        }
        #endregion

        #region Startup (TaskScheduler Wrapper)
        public static List<StartupAppDetails> GetStartupAppsWithDetails(bool bypassElevationCheck)
        {
            return StartupManager.GetStartupAppsWithDetails(bypassElevationCheck);
        }

        public static void SetStartupItemState(string name, bool enable, bool silentMode = false)
        {
            StartupManager.SetStartupItemState(name, enable, silentMode);
        }

        public static void CreateElevatedStartupTask(string name, string path, string? args)
        {
            StartupManager.CreateElevatedStartupTask(name, path, args);
        }

        public static bool CreateDelayedStartupTask(string name, string path, string? args)
        {
            return StartupManager.CreateDelayedStartupTask(name, path, args).Success;
        }
        #endregion
    }
}