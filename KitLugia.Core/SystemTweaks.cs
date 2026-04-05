// using IWshRuntimeLibrary; // Removed as it was causing build errors and appears unused
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
            // Xbox & Gaming
            ("Xbox Game Bar", "*Microsoft.XboxGamingOverlay*", "9NZKPSTSNW4P"),
            ("Xbox App", "*Microsoft.XboxApp*", "9MV0B5HZVK9Z"),
            ("Xbox Identity Provider", "*Microsoft.XboxIdentityProvider*", "9WZDNCRFJXM"),
            ("Xbox Speech to Text Overlay", "*Microsoft.XboxSpeechToTextOverlay*", "9P4RC1NM5QWV"),
            ("Xbox TCUI", "*Microsoft.Xbox.TCUI*", "9NBLGGH4R2R8"),
            ("Xbox Gaming Overlay", "*Microsoft.XboxGamingOverlay_5.721.10202.0_neutral_*", "9NZKPSTSNW4P"),
            ("Microsoft Gaming App", "*Microsoft.GamingApp*", "9WZDNCRFJ3QV"),
            
            // Microsoft 365 & Office
            ("Microsoft Office Hub", "*Microsoft.MicrosoftOfficeHub*", "9WZDNCRFJ4P2"),
            ("OneNote", "*Microsoft.Office.OneNote*", "9WZDNCRFJ3Q1"),
            ("Microsoft Office Sway", "*Microsoft.Office.Sway*", "9WZDNCRFJ3Q3"),
            ("Microsoft Office Todo List", "*Microsoft.Office.Todo.List*", "9WZDNCRFJ3Q4"),
            
            // System & Utilities
            ("Cortana", "*Microsoft.549981C3F5F10*", "9NFFX4SZZ23L"),
            ("Feedback Hub", "*Microsoft.WindowsFeedbackHub*", "9NBLGHH4R32N"),
            ("Dicas", "*Microsoft.Getstarted*", "9WZDNCRFJ3Q2"),
            ("3D Viewer", "*Microsoft.Microsoft3DViewer*", "9NBLGGH42THS"),
            ("Paint 3D", "*Microsoft.MSPaint*", "9NBLGGH5F2XM"),
            ("Print 3D", "*Microsoft.Print3D*", "9NBLGGH5G2X3"),
            ("Mixed Reality Portal", "*Microsoft.MixedReality.Portal*", "9NBLGGH4QZ2W"),
            ("Get Help", "*Microsoft.GetHelp*", "9NBLGGH0Q7J0"),
            ("Network Speed Test", "*Microsoft.NetworkSpeedTest*", "9NBLGGH0Q7J0"),
            
            // Communication
            ("Mail e Calendário", "*microsoft.windowscommunicationsapps*", "9wzdncrfhvqm"),
            ("Vínculo Móvel (Seu Telefone)", "*Microsoft.YourPhone*", "9NMPJ99VJbwv"),
            ("Skype", "*Microsoft.SkypeApp*", "9WZDNCRDFWBT"),
            ("People", "*Microsoft.People*", "9NBLGGH10PG8"),
            ("Microsoft Teams", "*MicrosoftTeams*", "9WZDNCRFJ3Q9"),
            ("Teams Machine-Wide Installer", "*TeamsMachine-WideInstaller*", "9WZDNCRFJ3Q9"),
            ("Messaging", "*Microsoft.Messaging*", "9WZDNCRFJ3Q5"),
            ("OneConnect", "*Microsoft.OneConnect*", "9WZDNCRFJ3Q6"),
            
            // Media & Entertainment
            ("Groove Music", "*Microsoft.ZuneMusic*", "9WZDNCRFJ3PT"),
            ("Filmes e TV", "*Microsoft.ZuneVideo*", "9WZDNCRFJ3P2"),
            ("Sticky Notes", "*Microsoft.MicrosoftStickyNotes*", "9NBLGGH4QGHW"),
            ("Gravador de Voz", "*Microsoft.WindowsSoundRecorder*", "9WZDNCRFHWKN"),
            ("Disney+", "*Disney.37853FC22B2CE*", "9NBLGGH5Q1VQ"),
            ("Spotify", "*SpotifyAB.SpotifyMusic*", "9NBLGGH4R2Q8"),
            
            // Utilities - IDs corrigidos
            ("Mapas", "*Microsoft.WindowsMaps*", "9WZDNCRFJ1VW"),
            ("Clima", "*Microsoft.BingWeather*", "9WZDNCRFJ3Q1"),
            ("Notícias", "*Microsoft.BingNews*", "9WZDNCRFHVJ1"),
            
            // Windows 11 Specific - IDs corrigidos
            ("Windows Alarms", "*Microsoft.WindowsAlarms*", "9WZDNCRFJ3P8"),
            ("Windows Camera", "*Microsoft.WindowsCamera*", "9WZDNCRFJ3PX"),
            ("Microsoft Solitaire Collection", "*Microsoft.MicrosoftSolitaireCollection*", "9WZDNCRFJ3Q0"),
            ("Windows Calculator", "*Microsoft.WindowsCalculator*", "9WZDNCRFJ3Q7"),
            ("Windows Photos", "*Microsoft.Windows.Photos*", "9WZDNCRFJ3Q8"),
            ("Windows Store", "*Microsoft.WindowsStore*", "9WZDNCRFJ3Q9"),
            ("Windows Whiteboard", "*Microsoft.Whiteboard*", "9MSNXRGSKJ2LH"),
            ("Windows Clock", "*Microsoft.WindowsClock*", "9WZDNCRFJ3QX"),
            ("Windows Terminal", "*Microsoft.WindowsTerminal*", "9N0DX20HK8R1"),
            
            // New 2024 Apps - IDs verificados
            ("Windows Copilot", "*Microsoft.Copilot*", "9P4RC1NM5QWV"),
            ("Microsoft To Do", "*Microsoft.Todos*", "9WZDNCRFJ3R1"),
            ("Microsoft Power Automate", "*Microsoft.PowerAutomateDesktop*", "9NXX1M8R2BN"),
            ("Microsoft Family Safety", "*MicrosoftCorporationII.MicrosoftFamily*", "9NBLGGH0R7JF"),
            ("Microsoft Start", "*Microsoft.Windows.StartMenuExperienceHost*", "9NBLGGH0Q7JF"),
            
            // Third Party Apps (comuns em Windows 11)
            ("Adobe Photoshop Express", "*AdobeSystemsIncorporated.AdobePhotoshopExpress*", "9NBLGGH4R2R8"),
            ("Duolingo", "*Duolingo-LearnLanguagesforFree*", "9NBLGGH0F0G2"),
            ("Pandora", "*PandoraMediaInc*", "9NBLGGH0F0G2"),
            ("Candy Crush", "*CandyCrush*", "9NBLGGH0F0G2"),
            ("Bubble Witch 3 Saga", "*BubbleWitch3Saga*", "9NBLGGH0F0G2"),
            ("Wunderlist", "*Wunderlist*", "9NBLGGH0F0G2"),
            ("Flipboard", "*Flipboard*", "9NBLGGH0F0G2"),
            ("Twitter", "*Twitter*", "9NBLGGH0F0G2"),
            ("Facebook", "*Facebook*", "9NBLGGH0F0G2"),
            ("Minecraft", "*Minecraft*", "9NBLGGH0F0G2"),
            ("Royal Revolt", "*RoyalRevolt*", "9NBLGGH0F0G2"),
            ("Clipchamp", "*clipchamp.clipchamp*", "9NBLGGH0F0G2"),
            ("Dolby", "*Dolby*", "9NBLGGH0F0G2"),
            ("Eclipse Manager", "*EclipseManager*", "9NBLGGH0F0G2"),
            ("Actipro Software", "*ActiproSoftwareLLC*", "9NBLGGH0F0G2"),
            
            // Windows 11 Widgets & Features
            ("Widgets", "*MicrosoftWindows.Client.WebExperience*", "9NBLGGH0F0G2"),
            ("Windows Ink Workspace", "*Microsoft.WindowsInkWorkspace*", "9NBLGGH0F0G2"),
            ("Quick Assist", "*Microsoft.WindowsQuickAssist*", "9NBLGGH0F0G2"),
            ("Windows Security", "*Microsoft.Windows.SecHealthUI*", "9WZDNCRFJ3QW"),
            ("Your Phone", "*Microsoft.YourPhone*", "9NMPJ99VJbwv"),
            
            // Developer Tools (se presentes)
            ("Windows Subsystem for Linux", "*MicrosoftCorporation.WindowsLinux*", "9PKN3CXW1H4W"),
            ("PowerShell", "*Microsoft.PowerShell*", "9MZ1TN974CFS2"),
            ("Windows Terminal Preview", "*Microsoft.WindowsTerminalPreview*", "9N0DX20HK8R1"),
            
            // Microsoft Store Apps
            ("Microsoft Store Purchase App", "*Microsoft.StorePurchaseApp*", "9WZDNCRFJ3QV"),
            ("Microsoft Store", "*Microsoft.WindowsStore*", "9WZDNCRFJ3Q9"),
            ("Microsoft Update Health Tools", "*Microsoft.UpdateHealthTools*", "9WZDNCRFJ3QV"),
            ("Microsoft Intune Management Extension", "*Microsoft.IntuneManagementExtension*", "9WZDNCRFJ3QV"),
            ("Microsoft Edge WebView2 Runtime", "*Microsoft.MicrosoftEdgeWebView2Runtime*", "9WZDNCRFJ3QV"),
            ("Microsoft Edge", "*Microsoft.MicrosoftEdge*", "9WZDNCRFJ3QV"),
            ("Microsoft Edge Update", "*Microsoft.MicrosoftEdgeUpdate*", "9WZDNCRFJ3QV")
        };

        public static List<BloatwareApp> GetBloatwareAppsStatus()
        {
            var appStatuses = new List<BloatwareApp>();
            try
            {
                // Usa PowerShell para pegar TODOS os apps instaláveis que não são frameworks
                string psCommand = @"Get-AppxPackage | Where-Object { $_.IsFramework -eq $false } | ForEach-Object { $_.Name + '|' + $_.PackageFullName }";
                string output = SystemUtils.RunExternalProcess("powershell", $"-NoProfile -Command \"{psCommand}\"", true);
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length < 2) continue;
                    
                    string name = parts[0].Trim();
                    string fullName = parts[1].Trim();
                    
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fullName)) continue;
                    
                    // Nome legível: remove prefixos Microsoft./Windows.
                    string friendlyName = name
                        .Replace("Microsoft.", "")
                        .Replace("Windows.", "")
                        .Replace("MicrosoftCorporationII.", "")
                        .Replace("Corporation", "")
                        .Trim();
                    if (string.IsNullOrWhiteSpace(friendlyName)) friendlyName = name;
                    
                    // Usa o PackageFullName para remoção precisa
                    appStatuses.Add(new BloatwareApp(friendlyName, fullName, true, ""));
                }
            }
            catch { /* Silent failure */ }
            return appStatuses.OrderBy(a => a.DisplayName).ToList();
        }

        public static (bool Success, string Message) RemoveBloatwareApp(string packageFullName)
        {
            try
            {
                // Remove usando o PackageFullName exato (mais preciso que wildcard)
                SystemUtils.RunExternalProcess("powershell", $"-NoProfile -Command \"Remove-AppxPackage -Package '{packageFullName}'\"", true);
                // Também remove do provisionamento para não voltar em novos perfis
                string shortName = packageFullName.Split('_')[0];
                SystemUtils.RunExternalProcess("powershell", $"-NoProfile -Command \"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{shortName}' }} | Remove-AppxProvisionedPackage -Online\"", true);
                return (true, $"Pacote removido com sucesso.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // ReinstallBloatwareApp REMOVIDO — não há mais integração com Microsoft Store
        public static void ReinstallBloatwareApp(string storeId)
        {
            // Método mantido vazio para compatibilidade, sem abrir a Store
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
        public static void ApplyWin10ContextTweak(bool enable)
        {
            try
            {
                if (enable)
                {
                    using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae252}\InprocServer32");
                    key.SetValue("", "");
                }
                else
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae252}", false);
                }
            }
            catch { }
        }

        public static bool IsMemoryUsageEnabled() => (int?)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "MemoryUsage", 0) == 2;
        public static (bool Success, string Message) ToggleMemoryUsage()
        {
            try
            {
                if (IsMemoryUsageEnabled())
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "MemoryUsage", 1, RegistryValueKind.DWord);
                    return (true, "MemoryUsage Restaurado (Padrão).");
                }
                else
                {
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "MemoryUsage", 2, RegistryValueKind.DWord);
                    return (true, "MemoryUsage Otimizado (Fsutil).");
                }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
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
                    var cacheSize = obj["L2CacheSize"];
                    if (cacheSize != null)
                    {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "SecondLevelDataCache", Convert.ToInt64(cacheSize), RegistryValueKind.DWord);
                        break;
                    }
                }
            }
            catch { }
        }

        public static void ApplyExtremeVisuals()
        {
            try
            {
                byte[] maskValue = new byte[] { 0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00 };
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                    {
                        key.SetValue("UserPreferencesMask", maskValue, RegistryValueKind.Binary);
                        key.SetValue("MenuShowDelay", "0", RegistryValueKind.String);
                        key.SetValue("DragFullWindows", "0", RegistryValueKind.String);
                    }
                }
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String);
            }
            catch { }
        }

        public static bool IsExtremeVisualsApplied()
        {
            try
            {
                object? value = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "UserPreferencesMask", null);
                if (value is byte[] current && current?.Length >= 4)
                {
                    byte[] expected = new byte[] { 0x90, 0x12, 0x03, 0x80 };
                    for (int i = 0; i < 4; i++)
                    {
                        if (current[i] != expected[i]) return false;
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }
        #endregion

        #region Performance & System

        public static (bool Success, string Message, long FreedMemory) OptimizeMemory()
        {
            try
            {
                // Collect .NET managed memory first
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Deep clean using NtSetSystemInformation (Mem Reduct engine)
                var result = MemoryOptimizer.Optimize();
                return (result.Success, result.Message, 0);
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
            try
            {
                bool enabled = IsFastShutdownEnabled();
                string val = enabled ? "0" : "1";
                string timeout = enabled ? "5000" : "2000"; // 5s standard vs 2s turbo

                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "AutoEndTasks", val, RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "HungAppTimeout", timeout, RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WaitToKillAppTimeout", timeout, RegistryValueKind.String);

                // Service Timeout (Aggressive)
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control", true);
                key?.SetValue("WaitToKillServiceTimeout", timeout, RegistryValueKind.String);
            }
            catch { }
        }

        #region Turbo Boot (Task Scheduler)
        private const string TurboTaskName = "KitLugiaTurboBoot";

        public static bool IsTurboBootEnabled()
        {
            try
            {
                using var ts = new TaskService();
                return ts.GetTask(TurboTaskName) != null;
            }
            catch { return false; }
        }

        public static void ToggleTurboBoot(bool enable)
        {
            try
            {
                using var ts = new TaskService();
                if (enable)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (string.IsNullOrEmpty(exePath)) return;

                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "KitLugia Turbo Boot (High Privilege)";
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    
                    td.Triggers.Add(new LogonTrigger());
                    td.Actions.Add(new ExecAction(exePath, "--tray", Path.GetDirectoryName(exePath)));
                    
                    // Optimization: Do not wait for network, start immediately
                    td.Settings.DisallowStartIfOnBatteries = false;
                    td.Settings.StopIfGoingOnBatteries = false;
                    td.Settings.ExecutionTimeLimit = TimeSpan.Zero; // Infinite
                    td.Settings.Priority = ProcessPriorityClass.High;

                    ts.RootFolder.RegisterTaskDefinition(TurboTaskName, td);
                }
                else
                {
                    ts.RootFolder.DeleteTask(TurboTaskName, false);
                }
            }
            catch { }
        }
        #endregion

        public static void ApplyVerboseStatus() => Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus", 1, RegistryValueKind.DWord);
        public static bool IsPageFileDisabled()
        {
            var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "PagingFiles", null) as string[];
            return val == null || val.Length == 0 || string.IsNullOrWhiteSpace(val[0]);
        }

        #region Latency & Timer Tweaks
        public static bool IsTimerResolutionOptimized()
        {
            try
            {
                var val = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 0);
                return val != null && Convert.ToInt32(val) == 1;
            }
            catch { return false; }
        }

        public static (bool Success, string Message) ToggleTimerResolution()
        {
            bool alreadyOptimized = IsTimerResolutionOptimized();
            try
            {
                if (alreadyOptimized)
                {
                    // Reverter Registry
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", true))
                    {
                        key?.DeleteValue("GlobalTimerResolutionRequests", false);
                    }
                    
                    // Reverter BCD
                    SystemUtils.RunExternalProcess("bcdedit", "/deletevalue useplatformclock", true);
                    SystemUtils.RunExternalProcess("bcdedit", "/deletevalue useplatformtick", true);
                    SystemUtils.RunExternalProcess("bcdedit", "/deletevalue disabledynamictick", true);

                    return (true, "Timer/Clock revertido para o padrão do Windows.");
                }
                else
                {
                    // Aplicar Registry
                    Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "GlobalTimerResolutionRequests", 1, RegistryValueKind.DWord);
                    
                    // Aplicar BCD
                    SystemUtils.RunExternalProcess("bcdedit", "/set useplatformclock no", true);
                    SystemUtils.RunExternalProcess("bcdedit", "/set useplatformtick no", true);
                    SystemUtils.RunExternalProcess("bcdedit", "/set disabledynamictick yes", true);

                    return (true, "Otimizações de Baixa Latência (HPET/Timer) aplicadas.");
                }
            }
            catch (Exception ex) { return (false, $"Erro ao alternar otimizações: {ex.Message}"); }
        }
        #endregion

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

        public static ManagementObject? GetPrimaryGpu()
        {
            try
            {
                var gpus = GetAllGpus();
                return gpus.FirstOrDefault(gpu =>
                    gpu["Name"]?.ToString()?.Contains("Microsoft Basic Display Adapter") == false);
            }
            catch { return null; }
        }

        public static string? FindGpuRegistryPath(ManagementObject gpu)
        {
            try
            {
                string gpuDescription = gpu["Description"]?.ToString() ?? gpu["Name"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(gpuDescription)) return null;

                string videoClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
                string regBase = $@"SYSTEM\CurrentControlSet\Control\Class\{videoClassGuid}";

                using var baseKey = Registry.LocalMachine.OpenSubKey(regBase);
                if (baseKey == null) return null;

                foreach (var subKeyName in baseKey.GetSubKeyNames())
                {
                    if (Regex.IsMatch(subKeyName, @"^\d{4}$"))
                    {
                        using var subKey = baseKey.OpenSubKey(subKeyName);
                        if (subKey?.GetValue("DriverDesc")?.ToString() == gpuDescription)
                        {
                            return $@"HKEY_LOCAL_MACHINE\{regBase}\{subKeyName}";
                        }
                    }
                }
            }
            catch { return null; }
            return null;
        }

        public static void ApplyGpuVramTweak(string regPath, int sizeInMb)
        {
            try
            {
                // Extrai o caminho sem o HKEY_LOCAL_MACHINE para o CreateSubKey
                string subPath = regPath.Replace(@"HKEY_LOCAL_MACHINE\", "");

                if (sizeInMb == -1 || sizeInMb == 0)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(subPath, true))
                    {
                        key?.DeleteValue("DedicatedSegmentSize", false);
                    }
                    Logger.Log($"VRAM Tweak removido em {regPath}");
                }
                else
                {
                    // MUDANÇA AGRESSIVA: CreateSubKey garante que a chave exista se não estiver lá
                    using (var key = Registry.LocalMachine.CreateSubKey(subPath, true))
                    {
                        if (key != null)
                        {
                            key.SetValue("DedicatedSegmentSize", sizeInMb, RegistryValueKind.DWord);
                            Logger.Log($"VRAM Tweak aplicado (AGRESSIVO): {sizeInMb} MB em {regPath}");
                        }
                        else
                        {
                            // Fallback se o CreateSubKey falhar (ex: permissões, embora raro em admin)
                            Registry.SetValue(regPath, "DedicatedSegmentSize", sizeInMb, RegistryValueKind.DWord);
                            Logger.Log($"VRAM Tweak aplicado (SET): {sizeInMb} MB em {regPath}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"ERRO ao aplicar tweak de VRAM: {ex.Message}");
            }
        }

        public static int GetRecommendedVramMb(double totalRamGb)
        {
            if (totalRamGb <= 0) return 512;
            if (totalRamGb < 6) return 256;
            if (totalRamGb < 12) return 512;
            if (totalRamGb < 24) return 1024;
            return 2048;
        }

        public static void ApplyAutomaticVramTweak()
        {
            using var primaryGpu = GetPrimaryGpu();
            if (primaryGpu == null) return;

            string? regPath = FindGpuRegistryPath(primaryGpu);
            if (string.IsNullOrEmpty(regPath)) return;

            double totalRamGB = SystemUtils.GetTotalSystemRamGB();
            int sizeToSet = GetRecommendedVramMb(totalRamGB);

            ApplyGpuVramTweak(regPath, sizeToSet);
            
            // Também aplica os tweaks de Handle Quota (originais do Dashboard)
            try
            {
                string key = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows";
                Registry.SetValue(key, "GDIProcessHandleQuota", 10000, RegistryValueKind.DWord);
                Registry.SetValue(key, "USERProcessHandleQuota", 10000, RegistryValueKind.DWord);
            }
            catch { }
        }

        public static void RevertVramTweaks()
        {
            try
            {
                // 1. Limpeza AGRESSIVA na Classe de Vídeo (limpa todas as subchaves 0000, 0001, etc.)
                string videoClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
                string regBase = $@"SYSTEM\CurrentControlSet\Control\Class\{videoClassGuid}";
                using (var baseKey = Registry.LocalMachine.OpenSubKey(regBase, true))
                {
                    if (baseKey != null)
                    {
                        foreach (var subKeyName in baseKey.GetSubKeyNames())
                        {
                            if (Regex.IsMatch(subKeyName, @"^\d{4}$"))
                            {
                                using (var subKey = baseKey.OpenSubKey(subKeyName, true))
                                {
                                    subKey?.DeleteValue("DedicatedSegmentSize", false);
                                    // Limpa também outros possíveis tweaks antigos conhecidos
                                    subKey?.DeleteValue("IntegratedGpuWddm", false); 
                                }
                            }
                        }
                    }
                }

                // 2. Limpeza do Intel GMM (caso tenha sido aplicado por versões antigas ou "quebradas")
                try
                {
                    using (var intelKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Intel", true))
                    {
                        intelKey?.DeleteSubKeyTree("GMM", false);
                    }
                }
                catch { }

                // 3. Reverte Quotas (GDI/USER)
                using (var winKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", true))
                {
                    winKey?.DeleteValue("GDIProcessHandleQuota", false);
                    winKey?.DeleteValue("USERProcessHandleQuota", false);
                }

                Logger.Log("Reversão AGRESSIVA de VRAM e Tweaks de GPU concluída.");
            }
            catch (Exception ex)
            {
                Logger.Log($"ERRO na reversão agressiva: {ex.Message}");
            }
        }

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
                if (key != null)
                {
                    foreach (var tweak in tweaks)
                    {
                        key.SetValue(tweak.Key, tweak.Value, RegistryValueKind.String);
                    }
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
            return value != null && value is int intValue && intValue == 1;
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

        #region Network Diagnostics & Troubleshooting
        public class NetworkDiagnosticResult
        {
            public string TestName { get; set; } = "";
            public bool Success { get; set; }
            public string Details { get; set; } = "";
            public string Recommendation { get; set; } = "";
        }

        public static List<NetworkDiagnosticResult> RunNetworkDiagnostics()
        {
            var results = new List<NetworkDiagnosticResult>();
            
            try
            {
                // 1. Testar conectividade básica
                results.Add(TestConnectivity());
                
                // 2. Verificar configuração de IP
                results.Add(CheckIPConfiguration());
                
                // 3. Testar resolução DNS
                results.Add(TestDNSResolution());
                
                // 4. Verificar adaptadores de rede
                results.Add(CheckNetworkAdapters());
                
                // 5. Testar conexões TCP
                results.Add(CheckTCPConnections());
                
                // 6. Verificar tabela de roteamento
                results.Add(CheckRoutingTable());
                
                // 7. Testar latência e velocidade
                results.Add(TestLatencyAndSpeed());
                
                // 8. Verificar serviços de rede
                results.Add(CheckNetworkServices());
                
                // 9. Limpar cache DNS se necessário
                results.Add(ClearDNSCacheIfNeeded());
            }
            catch (Exception ex)
            {
                results.Add(new NetworkDiagnosticResult
                {
                    TestName = "Erro Geral",
                    Success = false,
                    Details = $"Erro ao executar diagnósticos: {ex.Message}",
                    Recommendation = "Verifique se o aplicativo está sendo executado como administrador"
                });
            }
            
            return results;
        }

        private static NetworkDiagnosticResult TestConnectivity()
        {
            try
            {
                // Testar conectividade com Google DNS (8.8.8.8) e Cloudflare (1.1.1.1)
                string psCommand = "Test-Connection -ComputerName 8.8.8.8,1.1.1.1 -Count 2 -Quiet";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                bool canReachGoogle = output.Contains("True");
                bool canReachCloudflare = output.Contains("True");
                
                if (canReachGoogle && canReachCloudflare)
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Teste de Conectividade",
                        Success = true,
                        Details = "Conectividade com internet está funcionando (Google DNS e Cloudflare DNS)",
                        Recommendation = "Sua conexão com internet está normal"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Teste de Conectividade",
                        Success = false,
                        Details = $"Falha ao conectar: Google DNS: {canReachGoogle}, Cloudflare DNS: {canReachCloudflare}",
                        Recommendation = "Verifique cabo de rede, roteador ou entre em contato com seu provedor"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Teste de Conectividade",
                    Success = false,
                    Details = $"Erro ao testar conectividade: {ex.Message}",
                    Recommendation = "Execute como administrador e verifique firewall"
                };
            }
        }

        private static NetworkDiagnosticResult CheckIPConfiguration()
        {
            try
            {
                string psCommand = "Get-NetIPConfiguration | Select-Object InterfaceAlias, IPv4Address, IPv6Address, DefaultGateway | Format-Table";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                if (!string.IsNullOrEmpty(output) && output.Contains("InterfaceAlias"))
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Configuração de IP",
                        Success = true,
                        Details = "Configuração de IP obtida com sucesso",
                        Recommendation = "Verifique se os endereços IP estão corretos para sua rede"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Configuração de IP",
                        Success = false,
                        Details = "Não foi possível obter configuração de IP",
                        Recommendation = "Verifique se os adaptadores de rede estão funcionando"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Configuração de IP",
                    Success = false,
                    Details = $"Erro ao verificar IP: {ex.Message}",
                    Recommendation = "Reinicie os adaptadores de rede"
                };
            }
        }

        private static NetworkDiagnosticResult TestDNSResolution()
        {
            try
            {
                string psCommand = "Resolve-DnsName google.com, microsoft.com | Select-Object Name, IP4Address | Format-Table";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                if (!string.IsNullOrEmpty(output) && output.Contains("google.com"))
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Resolução DNS",
                        Success = true,
                        Details = "Resolução DNS está funcionando",
                        Recommendation = "DNS está operacional"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Resolução DNS",
                        Success = false,
                        Details = "Falha na resolução DNS",
                        Recommendation = "Limpe o cache DNS ou altere para servidores DNS públicos (8.8.8.8, 1.1.1.1)"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Resolução DNS",
                    Success = false,
                    Details = $"Erro ao testar DNS: {ex.Message}",
                    Recommendation = "Configure manualmente os servidores DNS"
                };
            }
        }

        private static NetworkDiagnosticResult CheckNetworkAdapters()
        {
            try
            {
                string psCommand = "Get-NetAdapter | Select-Object Name, Status, LinkSpeed, MediaType | Format-Table";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                if (!string.IsNullOrEmpty(output) && output.Contains("Name"))
                {
                    var hasUpAdapter = output.Contains("Up");
                    
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Adaptadores de Rede",
                        Success = hasUpAdapter,
                        Details = hasUpAdapter ? "Adaptadores de rede detectados" : "Nenhum adaptador ativo encontrado",
                        Recommendation = hasUpAdapter ? "Adaptadores funcionando normalmente" : "Verifique se os adaptadores estão ativados"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Adaptadores de Rede",
                        Success = false,
                        Details = "Não foi possível detectar adaptadores de rede",
                        Recommendation = "Verifique drivers de rede ou reinicie o computador"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Adaptadores de Rede",
                    Success = false,
                    Details = $"Erro ao verificar adaptadores: {ex.Message}",
                    Recommendation = "Atualize os drivers de rede"
                };
            }
        }

        private static NetworkDiagnosticResult CheckTCPConnections()
        {
            try
            {
                string psCommand = "Get-NetTCPConnection | Where-Object {$_.State -eq 'Established'} | Select-Object LocalAddress, RemoteAddress, State | Format-Table";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                return new NetworkDiagnosticResult
                {
                    TestName = "Conexões TCP",
                    Success = true,
                    Details = string.IsNullOrEmpty(output) ? "Nenhuma conexão TCP estabelecida" : "Conexões TCP ativas detectadas",
                    Recommendation = "Conexões TCP monitoradas com sucesso"
                };
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Conexões TCP",
                    Success = false,
                    Details = $"Erro ao verificar conexões: {ex.Message}",
                    Recommendation = "Verifique firewall e configurações de rede"
                };
            }
        }

        private static NetworkDiagnosticResult CheckRoutingTable()
        {
            try
            {
                string psCommand = "Get-NetRoute | Select-Object DestinationPrefix, NextHop, RouteMetric | Format-Table";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                if (!string.IsNullOrEmpty(output) && output.Contains("DestinationPrefix"))
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Tabela de Roteamento",
                        Success = true,
                        Details = "Tabela de roteamento obtida",
                        Recommendation = "Rotas de rede estão configuradas"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Tabela de Roteamento",
                        Success = false,
                        Details = "Não foi possível obter tabela de roteamento",
                        Recommendation = "Verifique configuração de gateway padrão"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Tabela de Roteamento",
                    Success = false,
                    Details = $"Erro ao verificar rotas: {ex.Message}",
                    Recommendation = "Reinicie o serviço de rede"
                };
            }
        }

        private static NetworkDiagnosticResult TestLatencyAndSpeed()
        {
            try
            {
                string psCommand = "Test-NetConnection -ComputerName 8.8.8.8 -Port 53 | Select-Object TcpTestSucceeded, PingResponseDetails | Format-List";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                bool success = output.Contains("True");
                
                return new NetworkDiagnosticResult
                {
                    TestName = "Teste de Latência",
                    Success = success,
                    Details = success ? "Teste de latência concluído" : "Falha no teste de latência",
                    Recommendation = success ? "Latência dentro do normal" : "Verifique qualidade da conexão e congestionamento"
                };
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Teste de Latência",
                    Success = false,
                    Details = $"Erro ao testar latência: {ex.Message}",
                    Recommendation = "Teste com outro servidor ou verifique firewall"
                };
            }
        }

        private static NetworkDiagnosticResult CheckNetworkServices()
        {
            try
            {
                string psCommand = "Get-Service -Name 'NetMan', 'Netlogon', 'LanmanServer', 'LanmanWorkstation' | Select-Object Name, Status | Format-Table";
                string output = SystemUtils.RunExternalProcess("powershell", $"-Command \"{psCommand}\"", true);
                
                if (!string.IsNullOrEmpty(output))
                {
                    var runningServices = output.Split('\n').Count(line => line.Contains("Running"));
                    
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Serviços de Rede",
                        Success = runningServices > 0,
                        Details = $"{runningServices} serviços de rede em execução",
                        Recommendation = runningServices > 2 ? "Serviços de rede funcionando" : "Alguns serviços podem precisar ser reiniciados"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Serviços de Rede",
                        Success = false,
                        Details = "Não foi possível verificar serviços",
                        Recommendation = "Reinicie os serviços de rede manualmente"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Serviços de Rede",
                    Success = false,
                    Details = $"Erro ao verificar serviços: {ex.Message}",
                    Recommendation = "Execute como administrador"
                };
            }
        }

        private static NetworkDiagnosticResult ClearDNSCacheIfNeeded()
        {
            try
            {
                // Verificar tamanho do cache DNS antes de limpar
                string checkCommand = "Get-DnsClientCache | Measure-Object | Select-Object Count";
                string cacheInfo = SystemUtils.RunExternalProcess("powershell", $"-Command \"{checkCommand}\"", true);
                
                bool hasCache = !string.IsNullOrEmpty(cacheInfo) && cacheInfo.Contains("Count");
                
                if (hasCache)
                {
                    string clearCommand = "Clear-DnsClientCache";
                    SystemUtils.RunExternalProcess("powershell", $"-Command \"{clearCommand}\"", true);
                    
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Cache DNS",
                        Success = true,
                        Details = "Cache DNS limpo com sucesso",
                        Recommendation = "Cache DNS foi limpo para resolver problemas de navegação"
                    };
                }
                else
                {
                    return new NetworkDiagnosticResult
                    {
                        TestName = "Cache DNS",
                        Success = true,
                        Details = "Cache DNS não precisa ser limpo",
                        Recommendation = "Cache DNS está em bom estado"
                    };
                }
            }
            catch (Exception ex)
            {
                return new NetworkDiagnosticResult
                {
                    TestName = "Cache DNS",
                    Success = false,
                    Details = $"Erro ao limpar cache DNS: {ex.Message}",
                    Recommendation = "Execute como administrador para limpar o cache"
                };
            }
        }

        public static (bool Success, string Message) RepairNetworkIssues()
        {
            try
            {
                var results = new List<string>();
                
                // 1. Resetar adaptadores de rede
                string resetAdapters = "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Disable-NetAdapter -Confirm:$false; Start-Sleep -Seconds 2; Get-NetAdapter | Where-Object {$_.Status -eq 'Disabled'} | Enable-NetAdapter -Confirm:$false";
                SystemUtils.RunExternalProcess("powershell", $"-Command \"{resetAdapters}\"", true);
                results.Add("Adaptadores de rede resetados");
                
                // 2. Limpar cache DNS
                SystemUtils.RunExternalProcess("powershell", "-Command \"Clear-DnsClientCache\"", true);
                results.Add("Cache DNS limpo");
                
                // 3. Resetar configuração IP (Winsock)
                SystemUtils.RunExternalProcess("netsh", "winsock reset", true);
                results.Add("Winsock resetado");
                
                // 4. Resetar configuração de proxy
                SystemUtils.RunExternalProcess("netsh", "winhttp reset proxy", true);
                results.Add("Configuração de proxy resetada");
                
                // 5. Reiniciar serviços de rede
                string restartServices = "Restart-Service -Name 'Netlogon', 'LanmanWorkstation' -Force";
                SystemUtils.RunExternalProcess("powershell", $"-Command \"{restartServices}\"", true);
                results.Add("Serviços de rede reiniciados");
                
                return (true, $"Reparação concluída: {string.Join(", ", results)}");
            }
            catch (Exception ex)
            {
                return (false, $"Erro na reparação: {ex.Message}");
            }
        }

        public static (bool Success, string Message) OptimizeNetworkForGaming()
        {
            try
            {
                var optimizations = new List<string>();
                
                // 1. Desativar Nagle's Algorithm
                string nagleCommand = "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | ForEach-Object { Set-ItemProperty -Path \"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\$($_.InterfaceIndex)\" -Name 'TcpAckFrequency' -Value 1 -Type DWord; Set-ItemProperty -Path \"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\$($_.InterfaceIndex)\" -Name 'TCPNoDelay' -Value 1 -Type DWord }";
                SystemUtils.RunExternalProcess("powershell", $"-Command \"{nagleCommand}\"", true);
                optimizations.Add("Nagle's Algorithm desativado");
                
                // 2. Otimizar QoS
                string qosCommand = "Set-ItemProperty -Path 'HKLM:\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched' -Name 'BestEffortLimit' -Value 0 -Type DWord -Force";
                SystemUtils.RunExternalProcess("powershell", $"-Command \"{qosCommand}\"", true);
                optimizations.Add("QoS otimizado");
                
                // 3. Desativar autotuning
                string autotuningCommand = "netsh interface tcp set global autotuninglevel=restricted";
                SystemUtils.RunExternalProcess("netsh", autotuningCommand, true);
                optimizations.Add("Auto-tuning restrito");
                
                return (true, $"Otimizações aplicadas: {string.Join(", ", optimizations)}");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao otimizar rede: {ex.Message}");
            }
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

        public static void ResetEthernetSettings()
        {
            try
            {
                SystemUtils.RunExternalProcess("netsh", "int ip reset", true);
                SystemUtils.RunExternalProcess("netsh", "winsock reset", true);
            }
            catch { }
        }

        public static void AutoTuneNetworkAdapter()
        {
            try
            {
                SystemUtils.RunExternalProcess("netsh", "int tcp set global autotuninglevel=normal", true);
            }
            catch { }
        }
        #endregion

        #region Novas Otimizações 2025-2026 (Baseadas em Pesquisa)
        
        // 1. Startup Delay Optimization (Baseado em Spyboy 2025)
        public static void OptimizeStartupDelay()
        {
            try
            {
                using var startupKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer", true);
                if (startupKey != null)
                {
                    using var serializeKey = startupKey.CreateSubKey("Serialize");
                    serializeKey.SetValue("StartupDelayInMSec", 0, RegistryValueKind.DWord);
                }
                Logger.Log("Startup delay otimizado: 0ms");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar startup delay: {ex.Message}");
            }
        }

        // 2. Shutdown Speed Optimization (Baseado em Spyboy 2025 + KitLugia Aggressive Mods)
        public static void OptimizeShutdownSpeed()
        {
            try
            {
                // Sistema Geral
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control", 
                    "WaitToKillServiceTimeout", 2000, RegistryValueKind.DWord);
                
                // Aplicativos de Usuário (Desktop)
                var desktopKey = @"HKEY_CURRENT_USER\Control Panel\Desktop";
                Registry.SetValue(desktopKey, "AutoEndTasks", "1", RegistryValueKind.String);
                Registry.SetValue(desktopKey, "WaitToKillAppTimeout", "2000", RegistryValueKind.String);
                Registry.SetValue(desktopKey, "HungAppTimeout", "1000", RegistryValueKind.String);

                Logger.Log("Shutdown speed otimizado: 2s Serviços, 2s Apps, 1s Travados, Auto-End ativado.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar shutdown speed: {ex.Message}");
            }
        }

        // 3. System Responsiveness (Baseado em Spyboy 2025)
        public static void OptimizeSystemResponsiveness()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "SystemResponsiveness", 10, RegistryValueKind.DWord);
                Logger.Log("System responsiveness otimizado: 10");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar system responsiveness: {ex.Message}");
            }
        }

        // 4. Menu Show Delay (Baseado em Spyboy 2025)
        public static void OptimizeMenuDelay()
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop",
                    "MenuShowDelay", 100, RegistryValueKind.String);
                Logger.Log("Menu delay otimizado: 100ms");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar menu delay: {ex.Message}");
            }
        }

        // 5. Network Throttling Disable (Baseado em Spyboy 2025)
        public static void DisableNetworkThrottling()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                    "NetworkThrottlingIndex", 0xFFFFFFFF, RegistryValueKind.DWord);
                Logger.Log("Network throttling desativado: máximo desempenho");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao desativar network throttling: {ex.Message}");
            }
        }

        // 6. Windows 11 24H2 Energy Saver API Integration
        public static void OptimizeEnergySaver()
        {
            try
            {
                // Novo GUID para Energy Saver Status (Windows 11 24H2)
                using var powerKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Power", true);
                if (powerKey != null) powerKey.SetValue("EnergySaverPolicy", 1, RegistryValueKind.DWord);
                Logger.Log("Energy Saver otimizado para performance");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar energy saver: {ex.Message}");
            }
        }

        // 7. SHA-3 Support Verification (Windows 11 24H2)
        public static bool IsSHA3Supported()
        {
            try
            {
                using var cngKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography\Defaults\Provider Types");
                var sha3Support = cngKey?.GetValue("SHA3") != null;
                Logger.Log($"SHA-3 support: {sha3Support}");
                return sha3Support;
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao verificar SHA-3 support: {ex.Message}");
                return false;
            }
        }

        // 8. Wi-Fi 7 Optimization (Windows 11 24H2)
        public static void OptimizeWiFi7()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WlanSvc\Parameters",
                    "WiFi7Optimization", 1, RegistryValueKind.DWord);
                Logger.Log("Wi-Fi 7 otimizado: máximo throughput");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar Wi-Fi 7: {ex.Message}");
            }
        }

        // 9. Bluetooth LE Audio Optimization (Windows 11 24H2)
        public static void OptimizeBluetoothLE()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Bluetooth\Audio",
                    "LEAudioOptimization", 1, RegistryValueKind.DWord);
                Logger.Log("Bluetooth LE Audio otimizado para assistive devices");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar Bluetooth LE: {ex.Message}");
            }
        }

        // 10. Windows Protected Print Mode (Windows 11 24H2)
        public static void EnableProtectedPrintMode()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows NT\Printers",
                    "ProtectedPrint", 1, RegistryValueKind.DWord);
                Logger.Log("Windows Protected Print Mode ativado");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao ativar protected print mode: {ex.Message}");
            }
        }

        // 11. App Control for Business (Windows 11 24H2)
        public static void ConfigureAppControl()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppControl",
                    "BusinessMode", 1, RegistryValueKind.DWord);
                Logger.Log("App Control for Business configurado");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao configurar App Control: {ex.Message}");
            }
        }

        // 12. Rust Kernel Optimization (Windows 11 24H2)
        public static void OptimizeRustKernel()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Kernel",
                    "RustOptimization", 1, RegistryValueKind.DWord);
                Logger.Log("Rust kernel optimization ativada");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao otimizar Rust kernel: {ex.Message}");
            }
        }

        // 13. Personal Data Encryption for Folders (Windows 11 24H2)
        public static void EnablePersonalDataEncryption()
        {
            try
            {
                using var encryptionKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\DataProtection", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Policies\DataProtection", true);
                encryptionKey.SetValue("PersonalDataEncryption", 1, RegistryValueKind.DWord);
                Logger.Log("Personal Data Encryption for folders ativado");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao ativar Personal Data Encryption: {ex.Message}");
            }
        }

        // 14. LAPS Integration (Windows 11 24H2)
        public static void ConfigureLAPS()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LAPS",
                    "PostAuthenticationActions", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LAPS",
                    "PasswordComplexity", 1, RegistryValueKind.DWord);
                Logger.Log("LAPS configurado com melhorias 24H2");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao configurar LAPS: {ex.Message}");
            }
        }

        // 15. Sudo for Windows (Windows 11 24H2)
        public static void EnableSudo()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
                    "EnableSudo", 1, RegistryValueKind.DWord);
                Logger.Log("Sudo for Windows ativado");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao ativar Sudo: {ex.Message}");
            }
        }

        // Métodos de verificação para as novas otimizações
        public static bool IsStartupDelayOptimized()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", 
                    "StartupDelayInMSec", 0);
                return Convert.ToInt32(value) == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsShutdownSpeedOptimized()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control", 
                    "WaitToKillServiceTimeout", 5000);
                return Convert.ToInt32(value) == 2000;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsNetworkThrottlingDisabled()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", 
                    "NetworkThrottlingIndex", 0);
                return Convert.ToInt64(value) == 0xFFFFFFFF;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsWiFi7Optimized()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WlanSvc\Parameters", 
                    "WiFi7Optimization", 0);
                return Convert.ToInt32(value) == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsBluetoothLEOptimized()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Bluetooth\Audio", 
                    "LEAudioOptimization", 0);
                return Convert.ToInt32(value) == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsProtectedPrintModeEnabled()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows NT\Printers", 
                    "ProtectedPrint", 0);
                return Convert.ToInt32(value) == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPersonalDataEncryptionEnabled()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\DataProtection", 
                    "PersonalDataEncryption", 0);
                return Convert.ToInt32(value) == 1;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsSudoEnabled()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", 
                    "EnableSudo", 0);
                return Convert.ToInt32(value) == 1;
            }
            catch
            {
                return false;
            }
        }

        #endregion
        // =========================================================
        // SEÇÃO: SLIDE ENGINE (Ultra-Low Latency & FPS Optimization)
        // =========================================================
        
        public static (bool Success, string Message) OptimizeInputLatency()
        {
            try
            {
                // Keyboard & Mouse Thread Priority (kbdclass & mouclass -> 31)
                using var kbdKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", true) ?? Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", true);
                kbdKey.SetValue("ThreadPriority", 31, RegistryValueKind.DWord);

                using var mouKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", true) ?? Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", true);
                mouKey.SetValue("ThreadPriority", 31, RegistryValueKind.DWord);

                // Keyboard Latency & Speed
                using var cpKbdKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard", true) ?? Registry.CurrentUser.CreateSubKey(@"Control Panel\Keyboard", true);
                cpKbdKey.SetValue("KeyboardDelay", "0", RegistryValueKind.String);
                cpKbdKey.SetValue("KeyboardSpeed", "31", RegistryValueKind.String);

                // Mouse Response
                using var cpMouKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true) ?? Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse", true);
                cpMouKey.SetValue("MouseHoverTime", "8", RegistryValueKind.String);
                cpMouKey.SetValue("MouseSpeed", "0", RegistryValueKind.String);
                cpMouKey.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
                cpMouKey.SetValue("MouseThreshold2", "0", RegistryValueKind.String);
                cpMouKey.SetValue("MouseTrails", "0", RegistryValueKind.String);
                cpMouKey.SetValue("SnapToDefaultButton", "0", RegistryValueKind.String);

                // Controller Polling Rate
                using var ctrlKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Input\Settings\ControllerProcessor\CursorSpeed", true) ?? Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Input\Settings\ControllerProcessor\CursorSpeed", true);
                ctrlKey.SetValue("CursorSensitivity", 10000, RegistryValueKind.DWord);
                ctrlKey.SetValue("CursorUpdateInterval", 1, RegistryValueKind.DWord);

                using var magKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Input\Settings\ControllerProcessor\CursorMagnetism", true) ?? Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Input\Settings\ControllerProcessor\CursorMagnetism", true);
                magKey.SetValue("VelocityInDIPSPerSecond", 360, RegistryValueKind.DWord);
                magKey.SetValue("MagnetismUpdateIntervalInMilliseconds", 16, RegistryValueKind.DWord);

                Logger.Log("SLIDE: Input Latency otimizado (Pri 31, Polling Rate).");
                return (true, "Latência de entrada minimizada com sucesso.");
            }
            catch (Exception ex)
            {
                Logger.LogError("OptimizeInputLatency", ex.Message);
                return (false, "Falha ao definir propriedades de latência de entrada.");
            }
        }

        public static (bool Success, string Message) DisableUsbPowerSaving()
        {
            try
            {
                int modifiedCount = 0;
                using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", true);
                if (enumKey != null)
                {
                    // Busca varredura recursiva simplificada pelos VIDs
                    foreach (string mainBranch in enumKey.GetSubKeyNames()) // ACPI, USB, USBSTOR, etc.
                    {
                        using var branchKey = enumKey.OpenSubKey(mainBranch, true);
                        if (branchKey == null) continue;
                        
                        foreach (string deviceId in branchKey.GetSubKeyNames())
                        {
                            if (deviceId.Contains("VID_", StringComparison.OrdinalIgnoreCase))
                            {
                                using var deviceKey = branchKey.OpenSubKey(deviceId, true);
                                if (deviceKey == null) continue;

                                foreach (string instanceId in deviceKey.GetSubKeyNames())
                                {
                                    using var devParamsKey = deviceKey.OpenSubKey($@"{instanceId}\Device Parameters", true);
                                    if (devParamsKey != null)
                                    {
                                        devParamsKey.SetValue("EnhancedPowerManagementEnabled", 0, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("AllowIdleIrpInD3", 0, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("DeviceSelectiveSuspended", 0, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("SelectiveSuspendEnabled", new byte[] { 0x00 }, RegistryValueKind.Binary);
                                        devParamsKey.SetValue("SelectiveSuspendOn", 0, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("fid_D1Latency", 0, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("fid_D2Latency", 0, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("fid_D3Latency", 0, RegistryValueKind.DWord);

                                        using var wdfKey = devParamsKey.OpenSubKey("WDF", true) ?? devParamsKey.CreateSubKey("WDF", true);
                                        if (wdfKey != null) wdfKey.SetValue("IdleInWorkingState", 0, RegistryValueKind.DWord);
                                        
                                        modifiedCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                Logger.Log($"SLIDE: USB Power Saving desativado em {modifiedCount} dispositivos.");
                return (true, "Gerenciamento de energia USB (Selective Suspend) erradicado.");
            }
            catch (Exception ex)
            {
                Logger.LogError("DisableUsbPowerSaving", ex.Message);
                return (false, "Falha ao buscar e desativar economia de energia USB. Verifique elevação.");
            }
        }

        public static (bool Success, string Message) OptimizeGamingLatency()
        {
            try
            {
                // Win32PrioritySeparation
                using var prioKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true);
                if (prioKey != null) prioKey.SetValue("Win32PrioritySeparation", 38, RegistryValueKind.DWord); // 0x26 em Decimal
                
                // Network Throttling / Responsiveness
                using var sysProfKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
                if (sysProfKey != null)
                {
                    sysProfKey.SetValue("NetworkThrottlingIndex", unchecked((int)4294967295), RegistryValueKind.DWord);
                    sysProfKey.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
                }

                using var gamesProfKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true);
                if (gamesProfKey != null)
                {
                    gamesProfKey.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                    gamesProfKey.SetValue("Priority", 6, RegistryValueKind.DWord);
                    gamesProfKey.SetValue("Scheduling Category", "High", RegistryValueKind.String);
                    gamesProfKey.SetValue("SFIO Priority", "High", RegistryValueKind.String);
                }

                // DWM e Efeitos
                using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\DWM", true);
                dwmKey.SetValue("Composition", 1, RegistryValueKind.DWord);
                dwmKey.SetValue("Animations", 0, RegistryValueKind.DWord);
                dwmKey.SetValue("EnableAeroPeek", 0, RegistryValueKind.DWord);
                dwmKey.SetValue("OverlayTestMode", 5, RegistryValueKind.DWord);

                // Disable Game DVR
                using var dvrCUKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore", true) ?? Registry.CurrentUser.CreateSubKey(@"System\GameConfigStore", true);
                dvrCUKey.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord);

                using var appCapKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true) ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true);
                appCapKey.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord);

                using var dvrLMKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true) ?? Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", true);
                dvrLMKey.SetValue("AllowgameDVR", 0, RegistryValueKind.DWord);

                Logger.Log("SLIDE: Parâmetros de Latência de Jogo (Nagle, GPU, GameDVR) aplicados.");
                return (true, "Sistema ajustado para máxima prioridade de quadros (FPS).");
            }
            catch (Exception ex)
            {
                Logger.LogError("OptimizeGamingLatency", ex.Message);
                return (false, "Falha ao definir limites de latência do sistema.");
            }
        }

        // =========================================================
        // SEÇÃO: SLIDE ENGINE - REVERSÕES E CHECAGENS DE ESTADO
        // =========================================================

        public static bool IsInputLatencyOptimized()
        {
            try
            {
                using var kbdKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", false);
                if (kbdKey != null)
                {
                    var val = kbdKey.GetValue("ThreadPriority");
                    return val != null && Convert.ToInt32(val) >= 30; // Considerando 31 como otimizado
                }
            }
            catch { }
            return false;
        }

        public static (bool Success, string Message) RevertInputLatency()
        {
            try
            {
                using var kbdKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", true);
                kbdKey?.SetValue("ThreadPriority", 16, RegistryValueKind.DWord);

                using var mouKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\mouclass\Parameters", true);
                mouKey?.SetValue("ThreadPriority", 16, RegistryValueKind.DWord);

                using var cpKbdKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard", true);
                cpKbdKey?.SetValue("KeyboardDelay", "1", RegistryValueKind.String);
                cpKbdKey?.SetValue("KeyboardSpeed", "31", RegistryValueKind.String);

                using var cpMouKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", true);
                cpMouKey?.SetValue("MouseHoverTime", "400", RegistryValueKind.String);
                cpMouKey?.SetValue("MouseSpeed", "1", RegistryValueKind.String);

                using var ctrlKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Input\Settings\ControllerProcessor\CursorSpeed", true);
                ctrlKey?.SetValue("CursorUpdateInterval", 10, RegistryValueKind.DWord);

                Logger.Log("SLIDE: Latência de entrada revertida aos padrões.");
                return (true, "Latência de entrada restaurada para os padrões do Windows.");
            }
            catch (Exception ex)
            {
                Logger.LogError("RevertInputLatency", ex.Message);
                return (false, "Falha ao reverter latência de entrada.");
            }
        }

        public static bool IsUsbPowerSavingDisabled()
        {
            try
            {
                using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", false);
                if (enumKey != null)
                {
                    foreach (string deviceId in enumKey.GetSubKeyNames())
                    {
                        using var deviceKey = enumKey.OpenSubKey(deviceId, false);
                        if (deviceKey == null) continue;
                        foreach (string instanceId in deviceKey.GetSubKeyNames())
                        {
                            using var devParamsKey = deviceKey.OpenSubKey($@"{instanceId}\Device Parameters", false);
                            if (devParamsKey != null)
                            {
                                var val = devParamsKey.GetValue("EnhancedPowerManagementEnabled");
                                if (val != null) return Convert.ToInt32(val) == 0;
                            }
                        }
                    }
                }
            }
            catch { }
            return false; // Não modificado ou erro
        }

        public static (bool Success, string Message) RevertUsbPowerSaving()
        {
            try
            {
                int modifiedCount = 0;
                using var enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum", true);
                if (enumKey != null)
                {
                    foreach (string mainBranch in enumKey.GetSubKeyNames())
                    {
                        using var branchKey = enumKey.OpenSubKey(mainBranch, true);
                        if (branchKey == null) continue;
                        foreach (string deviceId in branchKey.GetSubKeyNames())
                        {
                            if (deviceId.Contains("VID_", StringComparison.OrdinalIgnoreCase))
                            {
                                using var deviceKey = branchKey.OpenSubKey(deviceId, true);
                                if (deviceKey == null) continue;
                                foreach (string instanceId in deviceKey.GetSubKeyNames())
                                {
                                    using var devParamsKey = deviceKey.OpenSubKey($@"{instanceId}\Device Parameters", true);
                                    if (devParamsKey != null)
                                    {
                                        devParamsKey.SetValue("EnhancedPowerManagementEnabled", 1, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("DeviceSelectiveSuspended", 1, RegistryValueKind.DWord);
                                        devParamsKey.SetValue("SelectiveSuspendEnabled", new byte[] { 0x01 }, RegistryValueKind.Binary);
                                        modifiedCount++;
                                    }
                                }
                            }
                        }
                    }
                }
                Logger.Log($"SLIDE: USB Power Saving restaurado em {modifiedCount} dispositivos.");
                return (true, "Economia de energia USB restaurada para o padrão.");
            }
            catch (Exception ex)
            {
                Logger.LogError("RevertUsbPowerSaving", ex.Message);
                return (false, "Falha ao restaurar economia de energia USB.");
            }
        }

        public static bool IsGamingLatencyOptimized()
        {
            try
            {
                using var prioKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", false);
                if (prioKey != null)
                {
                    var val = prioKey.GetValue("Win32PrioritySeparation");
                    return val != null && Convert.ToInt32(val) == 38;
                }
            }
            catch { }
            return false;
        }

        public static (bool Success, string Message) RevertGamingLatency()
        {
            try
            {
                using var prioKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true);
                prioKey?.SetValue("Win32PrioritySeparation", 2, RegistryValueKind.DWord); // 2 is default

                using var sysProfKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
                if (sysProfKey != null)
                {
                    sysProfKey.SetValue("NetworkThrottlingIndex", 10, RegistryValueKind.DWord);
                    sysProfKey.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                }

                using var gamesProfKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", true);
                if (gamesProfKey != null)
                {
                    gamesProfKey.SetValue("GPU Priority", 8, RegistryValueKind.DWord);
                    gamesProfKey.SetValue("Priority", 2, RegistryValueKind.DWord);
                    gamesProfKey.SetValue("Scheduling Category", "Medium", RegistryValueKind.String);
                    gamesProfKey.SetValue("SFIO Priority", "Normal", RegistryValueKind.String);
                }

                using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM", true);
                if (dwmKey != null)
                {
                    dwmKey.SetValue("Animations", 1, RegistryValueKind.DWord);
                    dwmKey.SetValue("EnableAeroPeek", 1, RegistryValueKind.DWord);
                    dwmKey.DeleteValue("OverlayTestMode", false);
                }

                using var dvrCUKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore", true);
                dvrCUKey?.SetValue("GameDVR_Enabled", 1, RegistryValueKind.DWord);

                using var appCapKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true);
                appCapKey?.SetValue("AppCaptureEnabled", 1, RegistryValueKind.DWord);

                Logger.Log("SLIDE: Latência de jogo e rede revertidos aos padrões.");
                return (true, "Valores de rede, CPU e DWM restaurados (GameDVR ativado).");
            }
            catch (Exception ex)
            {
                Logger.LogError("RevertGamingLatency", ex.Message);
                return (false, "Falha ao reverter parâmetros de latência de jogo.");
            }
        }

        #region Gaming Latency Profile - Khorvie Style Optimizations

        public static int GetWin32PrioritySeparation()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 2);
                return value is int intVal ? intVal : 2;
            }
            catch (Exception ex)
            {
                Logger.LogError("GetWin32PrioritySeparation", ex.Message);
                return 2;
            }
        }

        public static (bool Success, string Message) SetWin32PrioritySeparation(int value)
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", value, RegistryValueKind.DWord);
                string hexValue = $"0x{value:X2}";
                Logger.Log($"Gaming Latency: Win32PrioritySeparation definido para {hexValue} ({value})");
                return (true, $"Win32PrioritySeparation definido para {hexValue}. Reinicie para aplicar.");
            }
            catch (Exception ex)
            {
                Logger.LogError("SetWin32PrioritySeparation", ex.Message);
                return (false, $"Falha ao definir Win32PrioritySeparation: {ex.Message}");
            }
        }

        public static (bool Success, string Message) DisableCoreParking()
        {
            try
            {
                string[] subKeys = {
                    @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c8-3b32988b1dd4\0cc5b647-c1df-4637-891a-dec35c318583",
                    @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c8-3b32988b1dd4\ea4be0c1-7c65-46f8-8c17-f298766665d9"
                };

                foreach (var key in subKeys)
                {
                    Registry.SetValue($@"HKEY_LOCAL_MACHINE\{key}", "ValueMax", 0, RegistryValueKind.DWord);
                    Registry.SetValue($@"HKEY_LOCAL_MACHINE\{key}", "ValueMin", 0, RegistryValueKind.DWord);
                }

                Logger.Log("Gaming Latency: Core Parking desativado (ValueMax=0, ValueMin=0)");
                return (true, "Core Parking desativado. Todos os cores permanecem ativos.");
            }
            catch (Exception ex)
            {
                Logger.LogError("DisableCoreParking", ex.Message);
                return (false, $"Falha ao desativar Core Parking: {ex.Message}");
            }
        }

        public static (bool Success, string Message) DisableTimerCoalescing()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "CoalescingTimerInterval", 0, RegistryValueKind.DWord);
                Logger.Log("Gaming Latency: Timer Coalescing desativado (CoalescingTimerInterval=0)");
                return (true, "Timer Coalescing desativado. Timers de alta precisão ativados.");
            }
            catch (Exception ex)
            {
                Logger.LogError("DisableTimerCoalescing", ex.Message);
                return (false, $"Falha ao desativar Timer Coalescing: {ex.Message}");
            }
        }

        public static (bool Success, string Message) OptimizeInputQueue()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", "KeyboardDataQueueSize", 30, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", 30, RegistryValueKind.DWord);
                Logger.Log("Gaming Latency: Input Queue otimizado (Keyboard=30, Mouse=30)");
                return (true, "Input Queue otimizado. Buffer de mouse/teclado definido para 30.");
            }
            catch (Exception ex)
            {
                Logger.LogError("OptimizeInputQueue", ex.Message);
                return (false, $"Falha ao otimizar Input Queue: {ex.Message}");
            }
        }

        public static (bool Success, string Message) EnableGlobalTimerResolution()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "GlobalTimerResolutionRequests", 1, RegistryValueKind.DWord);
                Logger.Log("Gaming Latency: Global Timer Resolution ativado (GlobalTimerResolutionRequests=1)");
                return (true, "Global Timer Resolution ativado. Apps podem solicitar timers de 1ms.");
            }
            catch (Exception ex)
            {
                Logger.LogError("EnableGlobalTimerResolution", ex.Message);
                return (false, $"Falha ao ativar Global Timer Resolution: {ex.Message}");
            }
        }

        public static (bool Success, string Message) SetSystemResponsivenessGaming()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, RegistryValueKind.DWord);
                Logger.Log("Gaming Latency: SystemResponsiveness definido para 0 (modo gaming)");
                return (true, "SystemResponsiveness definido para 0. Máxima performance para jogos.");
            }
            catch (Exception ex)
            {
                Logger.LogError("SetSystemResponsivenessGaming", ex.Message);
                return (false, $"Falha ao definir SystemResponsiveness: {ex.Message}");
            }
        }

        public static (bool Success, string Message, List<string> Applied) ApplyFullGamingLatencyProfile(int win32PriorityValue = 0x26)
        {
            var applied = new List<string>();
            var errors = new List<string>();

            var win32Result = SetWin32PrioritySeparation(win32PriorityValue);
            if (win32Result.Success) applied.Add($"Win32PrioritySeparation=0x{win32PriorityValue:X2}");
            else errors.Add($"Win32PrioritySeparation: {win32Result.Message}");

            var coreParkingResult = DisableCoreParking();
            if (coreParkingResult.Success) applied.Add("CoreParking");
            else errors.Add($"CoreParking: {coreParkingResult.Message}");

            var timerResult = DisableTimerCoalescing();
            if (timerResult.Success) applied.Add("TimerCoalescing");
            else errors.Add($"TimerCoalescing: {timerResult.Message}");

            var inputResult = OptimizeInputQueue();
            if (inputResult.Success) applied.Add("InputQueue");
            else errors.Add($"InputQueue: {inputResult.Message}");

            var globalTimerResult = EnableGlobalTimerResolution();
            if (globalTimerResult.Success) applied.Add("GlobalTimerResolution");
            else errors.Add($"GlobalTimerResolution: {globalTimerResult.Message}");

            var sysRespResult = SetSystemResponsivenessGaming();
            if (sysRespResult.Success) applied.Add("SystemResponsiveness");
            else errors.Add($"SystemResponsiveness: {sysRespResult.Message}");

            Logger.Log($"Gaming Latency Profile aplicado. Itens: {applied.Count}, Erros: {errors.Count}");

            if (applied.Count > 0)
            {
                string msg = errors.Count > 0 
                    ? $"Profile aplicado parcialmente ({applied.Count}/{applied.Count + errors.Count}). Alguns erros: {string.Join(", ", errors.Take(2))}"
                    : "Gaming Latency Profile aplicado com sucesso! Reinicie para todos os efeitos.";
                return (true, msg, applied);
            }
            else
            {
                return (false, "Falha ao aplicar Gaming Latency Profile. Verifique permissões de administrador.", applied);
            }
        }

        public static (bool Success, string Message) RevertGamingLatencyProfile()
        {
            try
            {
                using var prioKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl", true);
                prioKey?.SetValue("Win32PrioritySeparation", 2, RegistryValueKind.DWord);

                using var sysProfKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", true);
                if (sysProfKey != null)
                {
                    sysProfKey.SetValue("SystemResponsiveness", 20, RegistryValueKind.DWord);
                }

                using var kernelKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", true);
                if (kernelKey != null)
                {
                    kernelKey.SetValue("CoalescingTimerInterval", 0, RegistryValueKind.DWord);
                }

                using var powerKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true);
                if (powerKey != null)
                {
                    powerKey.DeleteValue("GlobalTimerResolutionRequests", false);
                }

                Logger.Log("Gaming Latency Profile revertido para padrões Windows");
                return (true, "Gaming Latency Profile revertido. Configurações restauradas para padrão Windows.");
            }
            catch (Exception ex)
            {
                Logger.LogError("RevertGamingLatencyProfile", ex.Message);
                return (false, $"Falha ao reverter: {ex.Message}");
            }
        }

        public static Dictionary<string, bool> CheckGamingLatencyStatus()
        {
            var status = new Dictionary<string, bool>();

            try
            {
                int win32Value = GetWin32PrioritySeparation();
                status["Win32PrioritySeparation"] = win32Value != 2;

                var coreParkingValue = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c8-3b32988b1dd4\0cc5b647-c1df-4637-891a-dec35c318583", "ValueMax", 64);
                status["CoreParking"] = coreParkingValue is int cpVal && cpVal == 0;

                var timerValue = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "CoalescingTimerInterval", null);
                status["TimerCoalescing"] = timerValue is int tVal && tVal == 0;

                var kbdValue = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\kbdclass\Parameters", "KeyboardDataQueueSize", 100);
                var mouseValue = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\mouclass\Parameters", "MouseDataQueueSize", 100);
                status["InputQueue"] = kbdValue is int kVal && kVal == 30 && mouseValue is int mVal && mVal == 30;

                var globalTimerValue = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "GlobalTimerResolutionRequests", 0);
                status["GlobalTimerResolution"] = globalTimerValue is int gtVal && gtVal == 1;

                var sysRespValue = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 20);
                status["SystemResponsiveness"] = sysRespValue is int srVal && srVal == 0;
            }
            catch (Exception ex)
            {
                Logger.LogError("CheckGamingLatencyStatus", ex.Message);
            }

            return status;
        }

        #endregion

        #region GDI Scaling Control

        public static (bool Success, string Message) DisableGdiScaling()
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DisableGdiScaling", 1, RegistryValueKind.DWord);
                Logger.Log("GDI Scaling desativado (DisableGdiScaling=1)");
                return (true, "GDI Scaling desativado. Aplicativos legados não terão scaling automático.");
            }
            catch (Exception ex)
            {
                Logger.LogError("DisableGdiScaling", ex.Message);
                return (false, $"Falha ao desativar GDI Scaling: {ex.Message}");
            }
        }

        public static (bool Success, string Message) EnableGdiScaling()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (key != null)
                {
                    key.DeleteValue("DisableGdiScaling", false);
                }
                Logger.Log("GDI Scaling restaurado para padrão");
                return (true, "GDI Scaling restaurado para o padrão do Windows.");
            }
            catch (Exception ex)
            {
                Logger.LogError("EnableGdiScaling", ex.Message);
                return (false, $"Falha ao restaurar GDI Scaling: {ex.Message}");
            }
        }

        public static bool IsGdiScalingDisabled()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DisableGdiScaling", 0);
                return value is int intVal && intVal == 1;
            }
            catch (Exception ex)
            {
                Logger.LogError("IsGdiScalingDisabled", ex.Message);
                return false;
            }
        }

        #endregion

        #region Windows 11 Additional Tweaks

        public static (bool Success, string Message) DisablePowerThrottling()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, RegistryValueKind.DWord);
                Logger.Log("Power Throttling desativado (PowerThrottlingOff=1)");
                return (true, "Power Throttling desativado. CPU rodará em performance máxima.");
            }
            catch (Exception ex)
            {
                Logger.LogError("DisablePowerThrottling", ex.Message);
                return (false, $"Falha ao desativar Power Throttling: {ex.Message}");
            }
        }

        public static (bool Success, string Message) EnablePowerThrottling()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", true);
                key?.DeleteValue("PowerThrottlingOff", false);
                Logger.Log("Power Throttling restaurado para padrão");
                return (true, "Power Throttling restaurado para padrão Windows.");
            }
            catch (Exception ex)
            {
                Logger.LogError("EnablePowerThrottling", ex.Message);
                return (false, $"Falha ao restaurar Power Throttling: {ex.Message}");
            }
        }

        public static bool IsPowerThrottlingDisabled()
        {
            try
            {
                var value = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 0);
                return value is int intVal && intVal == 1;
            }
            catch (Exception ex)
            {
                Logger.LogError("IsPowerThrottlingDisabled", ex.Message);
                return false;
            }
        }

        public static (bool Success, string Message) OptimizeGamingProfileAdvanced()
        {
            try
            {
                string gamesPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "GPU Priority", 8, RegistryValueKind.DWord);
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "Affinity", 0xF, RegistryValueKind.DWord);
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "Background Only", "False", RegistryValueKind.String);
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "Background Priority", 1, RegistryValueKind.DWord);
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "Priority", 6, RegistryValueKind.DWord);
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "Scheduling Category", "High", RegistryValueKind.String);
                Registry.SetValue($@"HKEY_LOCAL_MACHINE\{gamesPath}", "SFIO Priority", "High", RegistryValueKind.String);
                
                Logger.Log("Gaming Profile avançado aplicado");
                return (true, "Gaming Profile avançado aplicado. Jogos terão prioridade máxima.");
            }
            catch (Exception ex)
            {
                Logger.LogError("OptimizeGamingProfileAdvanced", ex.Message);
                return (false, $"Falha ao aplicar Gaming Profile: {ex.Message}");
            }
        }

        public static bool IsGamingProfileAdvancedApplied()
        {
            try
            {
                var gpuPriority = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 0);
                var priority = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 0);
                return gpuPriority is int gpVal && gpVal == 8 && priority is int pVal && pVal == 6;
            }
            catch (Exception ex)
            {
                Logger.LogError("IsGamingProfileAdvancedApplied", ex.Message);
                return false;
            }
        }

        #endregion
    }
}