using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using KitLugia.Core;
using Microsoft.Win32.TaskScheduler; // 🔥 Adicionar Task Scheduler
using System.IO; // 🔥 Adicionar Path
using Application = System.Windows.Application;
using Timer = System.Windows.Threading.DispatcherTimer;

namespace KitLugia.GUI.Services
{
    internal static class Win32Api
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        public const uint GW_OWNER = 4;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint WM_CLOSE = 0x0010;
    }

    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _trayIcon;
        private DispatcherTimer _monitorTimer;
        private Icon? _currentIcon;
        private class ProcessProfile
        {
            public string Name { get; set; } = "";
            public int TotalCyclesVisible { get; set; } = 0;
            public int CyclesForeground { get; set; } = 0;
            public bool IsVip { get; set; } = false;
            public DateTime LastTrimTime { get; set; } = DateTime.MinValue;
            public long LastKnownWs { get; set; } = 0;
        }

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ProcessProfile> _processProfiles = new();

        // Settings
        public bool AutoCleanEnabled { get; set; } = true;
        public int AutoCleanThresholdPercent { get; set; } = 80;
        private int _monitorIntervalSeconds = 30;
        public int MonitorIntervalSeconds 
        { 
            get => _monitorIntervalSeconds;
            set
            {
                _monitorIntervalSeconds = value;
                if (_monitorTimer != null)
                {
                    _monitorTimer.Interval = TimeSpan.FromSeconds(value);
                }
            }
        }
        public MemoryOptimizer.CleaningMode SelectedCleaningMode { get; set; } = MemoryOptimizer.CleaningMode.Normal;

        // Background Features
        public bool GamePriorityEnabled { get; set; } = false;
        public bool StandbyCleanEnabled { get; set; } = true;
        public bool MemoryLeakDetectionEnabled { get; set; } = false;
        public bool DpcMonitorEnabled { get; set; } = false;
        public bool FocusAssistEnabled { get; set; } = false;
        public bool TurboBootEnabled 
        { 
            get => SystemTweaks.IsTurboBootEnabled();
            set => SystemTweaks.ToggleTurboBoot(value);
        }
        public bool TurboShutdownEnabled
        {
            get => SystemTweaks.IsFastShutdownEnabled();
            set => SystemTweaks.ToggleFastShutdown();
        }

        // Tray active state
        public bool IsTrayEnabled { get; set; } = false;

        public static bool IsTrayEnabledStatic()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\KitLugia\TraySettings");
                return (int)(key?.GetValue("IsTrayEnabled", 0) ?? 0) == 1;
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(path)) return;

                // 🔥 CORREÇÃO: Usar Task Scheduler com privilégios admin em vez de Registry
                try
                {
                    using (var ts = new TaskService())
                    {
                        if (enable)
                        {
                            // Remover entrada antiga do Registry se existir
                            try
                            {
                                using var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                                if (regKey?.GetValue("KitLugia") != null)
                                {
                                    KitLugia.Core.Logger.Log("Removendo entrada antiga do Registry...");
                                    regKey.DeleteValue("KitLugia", false);
                                }
                            }
                            catch { }

                            // Verificar se tarefa já existe
                            var existingTask = ts.GetTask("KitLugia");
                            if (existingTask != null)
                            {
                                KitLugia.Core.Logger.Log("Tarefa do KitLugia já existe, atualizando...");
                                ts.RootFolder.DeleteTask("KitLugia");
                            }

                            // Criar nova tarefa com privilégios admin
                            var td = ts.NewTask();
                            td.RegistrationInfo.Description = "KitLugia Auto-Startup (Admin Mode)";
                            td.Principal.RunLevel = TaskRunLevel.Highest; // 🔥 ADMIN PRIVILEGES
                            td.Settings.DisallowStartIfOnBatteries = false;
                            td.Settings.StopIfGoingOnBatteries = false;
                            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                            td.Settings.StartWhenAvailable = true;
                            td.Settings.AllowHardTerminate = false;

                            // Trigger: Logon imediato para inicialização rápida
                            var trigger = new LogonTrigger
                            {
                                Delay = TimeSpan.Zero, // 🔥 INICIALIZAÇÃO IMEDIATA
                                Enabled = true
                            };
                            td.Triggers.Add(trigger);

                            // Action: Executar com --tray
                            td.Actions.Add(new ExecAction(path, "--tray", Path.GetDirectoryName(path)));

                            // Registrar tarefa
                            ts.RootFolder.RegisterTaskDefinition("KitLugia", td);
                            KitLugia.Core.Logger.Log("✅ Tarefa agendada com privilégios admin criada: " + path);
                        }
                        else
                        {
                            // Remover tarefa
                            var task = ts.GetTask("KitLugia");
                            if (task != null)
                            {
                                ts.RootFolder.DeleteTask("KitLugia");
                                KitLugia.Core.Logger.Log("✅ Tarefa agendada removida");
                            }
                        }
                    }
                }
                catch (Exception taskEx)
                {
                    KitLugia.Core.Logger.Log($"ERRO no Task Scheduler: {taskEx.Message}");
                    
                    // Fallback para Registry (sem privilégios admin)
                    KitLugia.Core.Logger.Log("Usando fallback Registry Run (sem privilégios admin)...");
                    using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    if (key != null)
                    {
                        if (enable)
                        {
                            key.SetValue("KitLugia", $"\"{path}\" --tray");
                        }
                        else
                        {
                            key.DeleteValue("KitLugia", false);
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                KitLugia.Core.Logger.Log($"SetAutoStart ERROR: {ex.Message}"); 
            }
        }

        // Adaptive Data
        private readonly string[] _vipProcesses = { "opera", "discord", "taskmgr", "devenv", "kitlugia", "steam", "riotclient" };
        private long _stutterBackoffCycles = 0;
        private long _lastCleanDurationMs = 0;
        private string _logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KitLugia", "ram_stats.csv");

        public event System.Action? OnOpenMainWindow;
        public event System.Action? OnOpenSettings;

        public TrayIconService()
        {
            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(MonitorIntervalSeconds)
            };
            _monitorTimer.Tick += MonitorTick;
        }

        public void Initialize()
        {
            LoadSettings();

            // 🔥 CHECK 1: Verificação se NotifyIcon pode ser criado
            try
            {
                _trayIcon = new NotifyIcon
                {
                    Text = "KitLugia RAM Monitor",
                    Visible = false // Inicia oculto para verificação
                };
                KitLugia.Core.Logger.Log("NotifyIcon criado com sucesso");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"ERRO ao criar NotifyIcon: {ex.Message}");
                return;
            }

            // 🔥 CHECK 2: Verificação se o sistema suporta tray icons
            try
            {
                // Testa se consegue criar um NotifyIcon temporário
                using (var testIcon = new NotifyIcon())
                {
                    KitLugia.Core.Logger.Log("Sistema suporta NotifyIcon");
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"AVISO: Sistema pode não suportar NotifyIcon - {ex.Message}");
            }

            // Generate the initial icon
            UpdateTrayIcon(0);

            // Context Menu
            var menu = new ContextMenuStrip();

            var itemClean = new ToolStripMenuItem("🧹 Limpar RAM Agora");
            itemClean.Click += (s, e) => CleanRamNow();
            menu.Items.Add(itemClean);

            menu.Items.Add(new ToolStripSeparator());

            var itemAutoClean = new ToolStripMenuItem($"⚡ Auto-Limpeza ({AutoCleanThresholdPercent}%)");
            itemAutoClean.Checked = AutoCleanEnabled;
            itemAutoClean.Click += (s, e) =>
            {
                AutoCleanEnabled = !AutoCleanEnabled;
                itemAutoClean.Checked = AutoCleanEnabled;
                SaveSettings();
            };
            menu.Items.Add(itemAutoClean);

            menu.Items.Add(new ToolStripSeparator());

            var itemSettings = new ToolStripMenuItem("⚙ Configurações");
            itemSettings.Click += (s, e) => OnOpenSettings?.Invoke();
            menu.Items.Add(itemSettings);

            var itemOpen = new ToolStripMenuItem("🚀 Abrir KitLugia");
            itemOpen.Font = new Font(itemOpen.Font, FontStyle.Bold);
            itemOpen.Click += (s, e) => OnOpenMainWindow?.Invoke();
            menu.Items.Add(itemOpen);

            var itemExit = new ToolStripMenuItem("❌ Sair Completamente");
            itemExit.Click += (s, e) =>
            {
                Dispose();
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            };
            menu.Items.Add(itemExit);

            _trayIcon.ContextMenuStrip = menu;

            // Double-click to open main window
            _trayIcon.DoubleClick += (s, e) => OnOpenMainWindow?.Invoke();

            // 🔥 CHECK 3: Verificação final antes de tornar visível
            if (IsTrayEnabled && _trayIcon != null)
            {
                try
                {
                    // Força atualização do ícone antes de tornar visível
                    UpdateTrayIcon(GetMemoryUsagePercent());
                    
                    // Torna visível e verifica
                    _trayIcon.Visible = true;
                    
                    // 🔥 CHECK 4: Verificação se realmente ficou visível
                    if (_trayIcon.Visible)
                    {
                        KitLugia.Core.Logger.Log("✅ Tray Icon ativado com sucesso");
                        
                        // Start monitoring if enabled
                        _monitorTimer.Start();
                        // Run an initial Safety Profiler
                        Application.Current.Dispatcher.BeginInvoke(new System.Action(RunSafetyProfiler), DispatcherPriority.Background);
                        // First tick immediately
                        MonitorTick(null, EventArgs.Empty);
                    }
                    else
                    {
                        KitLugia.Core.Logger.Log("❌ ERRO: Tray Icon não ficou visível após tentativa");
                    }
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"❌ ERRO ao ativar Tray Icon: {ex.Message}");
                }
            }
            else
            {
                KitLugia.Core.Logger.Log($"Tray Icon desativado ou nulo. Enabled: {IsTrayEnabled}, Icon: {_trayIcon != null}");
            }

            // Register for Shutdown events
            Microsoft.Win32.SystemEvents.SessionEnding += (s, e) => ShutdownTurboCharge();
        }

        public void ShutdownTurboCharge()
        {
            try
            {
                // Active Shutdown Charge (WM_CLOSE broadcast)
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (proc.Id == Process.GetCurrentProcess().Id) continue;
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            Win32Api.SendMessage(proc.MainWindowHandle, Win32Api.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\KitLugia\TraySettings");
                key.SetValue("IsTrayEnabled", IsTrayEnabled ? 1 : 0);
                key.SetValue("AutoCleanEnabled", AutoCleanEnabled ? 1 : 0);
                key.SetValue("Threshold", AutoCleanThresholdPercent);
                key.SetValue("Interval", MonitorIntervalSeconds);
                key.SetValue("CleaningMode", (int)SelectedCleaningMode);
                key.SetValue("GamePriority", GamePriorityEnabled ? 1 : 0);
                key.SetValue("StandbyClean", StandbyCleanEnabled ? 1 : 0);
                key.SetValue("AntiLeak", MemoryLeakDetectionEnabled ? 1 : 0);
                key.SetValue("FocusAssist", FocusAssistEnabled ? 1 : 0);
                key.SetValue("TurboBoot", TurboBootEnabled ? 1 : 0);
                key.SetValue("TurboShutdown", TurboShutdownEnabled ? 1 : 0);
            }
            catch { }
        }

        public void LoadSettings()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\KitLugia\TraySettings");
                if (key == null)
                {
                    IsTrayEnabled = false; // Default on first run
                    return;
                }

                IsTrayEnabled = (int)key.GetValue("IsTrayEnabled", 0) == 1;
                AutoCleanEnabled = (int)key.GetValue("AutoCleanEnabled", 1) == 1;
                AutoCleanThresholdPercent = (int)key.GetValue("Threshold", 80);
                MonitorIntervalSeconds = (int)key.GetValue("Interval", 30);
                SelectedCleaningMode = (MemoryOptimizer.CleaningMode)(int)key.GetValue("CleaningMode", (int)MemoryOptimizer.CleaningMode.Normal);
                GamePriorityEnabled = (int)key.GetValue("GamePriority", 0) == 1;
                StandbyCleanEnabled = (int)key.GetValue("StandbyClean", 1) == 1;
                MemoryLeakDetectionEnabled = (int)key.GetValue("AntiLeak", 0) == 1;
                FocusAssistEnabled = (int)key.GetValue("FocusAssist", 0) == 1;

                _monitorTimer.Interval = TimeSpan.FromSeconds(MonitorIntervalSeconds);
            }
            catch { }
        }

        private void RunSafetyProfiler()
        {
            try
            {
                // Baseline: How long does a 'Leve' clean take on this system?
                Stopwatch sw = Stopwatch.StartNew();
                MemoryOptimizer.Optimize(MemoryOptimizer.CleaningMode.Leve);
                sw.Stop();
                
                _lastCleanDurationMs = sw.ElapsedMilliseconds;
                // If it takes > 300ms just for a Leve clean, this system is slow/busy
                if (_lastCleanDurationMs > 300)
                {
                    _stutterBackoffCycles = 1; // Start with caution
                }
            }
            catch { }
        }

        public void SetTrayEnabled(bool enabled)
        {
            IsTrayEnabled = enabled;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = enabled;
            }

            if (enabled)
            {
                if (!_monitorTimer.IsEnabled) _monitorTimer.Start();
            }
            else
            {
                _monitorTimer.Stop();
            }
        }

        private void MonitorTick(object? sender, EventArgs e)
        {
            try
            {
                // 1. Refresh System Stats
                var stats = MemoryOptimizer.GetMemoryStats();
                int usedPercent = stats.Percent;
                UpdateTrayIcon(usedPercent);

                if (_trayIcon != null)
                    _trayIcon.Text = $"KitLugia - RAM: {usedPercent}% em uso";

                // 2. Auto-clean logic (Manual/Threshold)
                if (AutoCleanEnabled && usedPercent >= AutoCleanThresholdPercent)
                {
                    CleanRamNow();
                }

                // 3. Game Priority Boost
                if (GamePriorityEnabled)
                {
                    OptimizeForegroundProcess();
                }

                // 4. Standby List Cleaning (be gentle)
                if (StandbyCleanEnabled)
                {
                    CheckAndCleanStandby(usedPercent);
                }

                // 5. Memory Leak Mitigation (Anti-Leak) - Targeted and Smart
                if (MemoryLeakDetectionEnabled)
                {
                    DetectAndTrimLeaks(usedPercent, stats);
                }

                // 6. Focus Assist (Quiet Hours)
                if (FocusAssistEnabled)
                {
                    ManageFocusAssist();
                }

                // 7. Dynamic Intelligence (V2) - Tracker & Firemin-Optimized Trim
                UpdateProcessProfiles(stats);
                ApplyFireminOptimizations();

                // 8. Auto-Log Stats
                LogStats(stats);
            }
            catch
            {
                // Silently ignore monitoring errors
            }
        }

        private void UpdateProcessProfiles(MemoryOptimizer.MemoryInfo stats)
        {
            try
            {
                IntPtr foregroundHwnd = Win32Api.GetForegroundWindow();
                uint foregroundPid = 0;
                if (foregroundHwnd != IntPtr.Zero) Win32Api.GetWindowThreadProcessId(foregroundHwnd, out foregroundPid);

                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        string name = proc.ProcessName.ToLower();
                        if (name == "explorer" || name == "dwm" || name == "lsass" || name == "csrss") continue;

                        // Only track user-facing apps for VIP promotion
                        if (proc.MainWindowHandle == IntPtr.Zero || !IsTaskbarWindow(proc.MainWindowHandle)) continue;

                        var profile = _processProfiles.GetOrAdd(name, _ => new ProcessProfile { Name = name });

                        profile.TotalCyclesVisible++;
                        if (proc.Id == foregroundPid) profile.CyclesForeground++;
                        profile.LastKnownWs = proc.WorkingSet64;

                        // Promotion logic:
                        // 1. Known browsers/apps
                        if (!profile.IsVip)
                        {
                            bool isKnownVip = _vipProcesses.Any(v => name.Contains(v)) || name.Contains("chrome") || name.Contains("msedge") || name.Contains("brave") || name.Contains("vivaldi");
                            // 2. Used heavily (long cycles visible)
                            bool isHeavyUse = profile.TotalCyclesVisible > 5;

                            if (isKnownVip || isHeavyUse)
                            {
                                profile.IsVip = true;
                                // Log promotion event indirectly via CSV later or debug
                            }
                        }
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
        }

        private void ApplyFireminOptimizations()
        {
            try
            {
                // Firemin logic: targeted frequent but ultra-gentle trim for VIPs
                foreach (var profile in _processProfiles.Values)
                {
                    if (!profile.IsVip) continue;
                    
                    // Rate Limit: 10 seconds between trims for the same process
                    if ((DateTime.Now - profile.LastTrimTime).TotalSeconds < 10) continue;

                    // Threshold: only trim if it exceeds 300MB
                    if (profile.LastKnownWs < 300L * 1024 * 1024) continue;

                    try
                    {
                        // Find all instances of this VIP process
                        foreach (var proc in Process.GetProcessesByName(profile.Name))
                        {
                            try
                            {
                                MemoryOptimizer.EmptyProcessWorkingSet(proc.Id);
                            }
                            catch { }
                            finally { proc.Dispose(); }
                        }
                        profile.LastTrimTime = DateTime.Now;
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void LogStats(MemoryOptimizer.MemoryInfo stats)
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(_logPath)!;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

                bool exists = System.IO.File.Exists(_logPath);
                using var sw = new System.IO.StreamWriter(_logPath, true);
                if (!exists) sw.WriteLine("Timestamp,UsedPercent,UsedGB,FreeGB,LastDurationMs,StutterCycles");

                sw.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{stats.Percent},{stats.UsedGB:F2},{stats.FreeGB:F2},{_lastCleanDurationMs},{_stutterBackoffCycles}");
            }
            catch { }
        }

        private bool _lastFocusState = false;
        private void ManageFocusAssist()
        {
            try
            {
                // Check if a game/foreground app is likely active
                IntPtr hwnd = Win32Api.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;
                Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
                
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName.ToLower();
                
                // If it's not a system/shell process, assume we want focus
                bool shouldFocus = (name != "explorer" && name != "dwm" && name != "shellexperiencehost" && name != "searchhost");

                if (shouldFocus != _lastFocusState)
                {
                    SetWindowsFocusAssist(shouldFocus);
                    _lastFocusState = shouldFocus;
                }
            }
            catch { }
        }

        private void SetWindowsFocusAssist(bool enable)
        {
            try
            {
                // Registry key for Focus Assist (Quiet Hours) - simplified approach
                // 0 = Off, 1 = Priority Only, 2 = Alarms Only
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", true);
                if (key != null)
                {
                    key.SetValue("NOC_GLOBAL_SETTING_TOASTS_ENABLED", enable ? 0 : 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        private void DetectAndTrimLeaks(int systemUsagePercent, MemoryOptimizer.MemoryInfo stats)
        {
            try
            {
                // Only act if system RAM usage is starting to get high
                if (systemUsagePercent < 65) return;

                // Threshold: 15% of total RAM or at least 2GB
                ulong standardThreshold = (ulong)(stats.TotalBytes * 0.15);
                if (standardThreshold < 2000UL * 1024 * 1024) standardThreshold = 2000UL * 1024 * 1024;

                // VIP Threshold: 25% of total RAM (much more tolerant)
                ulong vipThreshold = (ulong)(stats.TotalBytes * 0.25);

                IntPtr foregroundHwnd = Win32Api.GetForegroundWindow();
                uint foregroundPid = 0;
                if (foregroundHwnd != IntPtr.Zero) Win32Api.GetWindowThreadProcessId(foregroundHwnd, out foregroundPid);

                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        // Don't touch ourselves or the user's active game/app
                        if (proc.Id == Process.GetCurrentProcess().Id || proc.Id == foregroundPid) continue;

                        string name = proc.ProcessName.ToLower();
                        bool isVip = _vipProcesses.Any(v => name.Contains(v));

                        // Essential system processes to ignore
                        if (name == "explorer" || name == "dwm" || name == "lsass" || name == "csrss" || name == "searchindexer") continue;

                        ulong currentWs = (ulong)proc.WorkingSet64;
                        ulong activeThreshold = isVip ? vipThreshold : standardThreshold;

                        if (currentWs > activeThreshold)
                        {
                            // Target ONLY this leaky process
                            // If it's a VIP, we ONLY do a Leve trim to prevent lag
                            MemoryOptimizer.EmptyProcessWorkingSet(proc.Id);
                        }
                    }
                    catch { }
                    finally { proc.Dispose(); }
                }
            }
            catch { }
        }

        private uint _lastBoostedPid = 0;
        private ProcessPriorityClass _lastOriginalPriority = ProcessPriorityClass.Normal;

        private void OptimizeForegroundProcess()
        {
            try
            {
                IntPtr hwnd = Win32Api.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0 || pid == Process.GetCurrentProcess().Id) return;

                // Se o foco não mudou, não faz nada
                if (pid == _lastBoostedPid) return;

                // 1. Reverter o aplicativo anterior
                if (_lastBoostedPid != 0)
                {
                    try
                    {
                        using var oldProc = Process.GetProcessById((int)_lastBoostedPid);
                        // Restaura CPUPriority
                        if (oldProc.PriorityClass != _lastOriginalPriority)
                            oldProc.PriorityClass = _lastOriginalPriority;

                        // Restaura I/O Normal (2) e Page Priority Default (5)
                        Win32Api.SetProcessIoPriority(oldProc.Handle, 2);
                        Win32Api.SetProcessPagePriority(oldProc.Handle, 5);
                    }
                    catch { } // Processo pode ter sido fechado
                }

                _lastBoostedPid = pid;

                // 2. Aplicar Boost no novo aplicativo
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName.ToLower();

                // Ignorar processos vitais do Windows
                if (name == "explorer" || name == "dwm" || name == "shellexperiencehost" || name == "taskmgr" || name == "searchindexer")
                {
                    _lastOriginalPriority = ProcessPriorityClass.Normal; // Assume normal pra não corromper sistema
                    return;
                }

                _lastOriginalPriority = proc.PriorityClass;

                // Tweak 1: CPU Priority para High ou AboveNormal
                if (proc.PriorityClass != ProcessPriorityClass.High && proc.PriorityClass != ProcessPriorityClass.RealTime)
                {
                    proc.PriorityClass = ProcessPriorityClass.High;
                }

                // Tweak 2 & 3: I/O Priority High (3) e Page Priority Máxima (5)
                Win32Api.SetProcessIoPriority(proc.Handle, 3);
                Win32Api.SetProcessPagePriority(proc.Handle, 5);
            }
            catch { }
        }

        private void CheckAndCleanStandby(int systemUsagePercent)
        {
            try
            {
                // On high RAM systems (32GB+), standby list is actually good for performance
                // We only clear it if RAM is truly crowded (> 80%)
                if (systemUsagePercent > 80)
                {
                    // Use Normal instead of Alta to avoid heavy purging of useful cache
                    MemoryOptimizer.Optimize(MemoryOptimizer.CleaningMode.Normal);
                }
            }
            catch { }
        }

        private bool IsTaskbarWindow(IntPtr hwnd)
        {
            if (!Win32Api.IsWindowVisible(hwnd)) return false;
            
            IntPtr owner = Win32Api.GetWindow(hwnd, Win32Api.GW_OWNER);
            int exStyle = Win32Api.GetWindowLong(hwnd, Win32Api.GWL_EXSTYLE);

            // A window is on the taskbar if:
            // 1. It is visible (checked above)
            // 2. It has no owner AND is not a tool window
            // 3. OR it has the explicit WS_EX_APPWINDOW style
            bool isToolWindow = (exStyle & Win32Api.WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & Win32Api.WS_EX_APPWINDOW) != 0;

            if (owner == IntPtr.Zero && !isToolWindow) return true;
            if (isAppWindow) return true;

            return false;
        }

        private void CleanRamNow()
        {
            try
            {
                // Handle stutter backoff
                if (_stutterBackoffCycles > 0)
                {
                    _stutterBackoffCycles--;
                    return;
                }

                // 🔥 CORREÇÃO: Executar em Task separada para não congelar UI
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        Stopwatch sw = Stopwatch.StartNew();
                        int before = GetMemoryUsagePercent();
                        var result = MemoryOptimizer.Optimize(SelectedCleaningMode);
                        int after = GetMemoryUsagePercent();
                        sw.Stop();

                        _lastCleanDurationMs = sw.ElapsedMilliseconds;

                        // If cleaning takes too long (> 800ms on a 32GB system), it might cause stutter
                        // Adaptive learning: wait more cycles before next auto-clean
                        if (_lastCleanDurationMs > 800)
                        {
                            _stutterBackoffCycles = 3; // Skip next 3 cycles (~1.5 min)
                        }

                        int freed = before - after;
                        string msg = freed > 0
                            ? $"RAM liberada! {before}% → {after}% ({freed}% liberado) [{_lastCleanDurationMs}ms]"
                            : $"Limpeza concluída. RAM: {after}%";

                        // Atualizar UI na thread principal
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            UpdateTrayIcon(after);

                            _trayIcon?.ShowBalloonTip(
                                3000,
                                "KitLugia RAM Booster",
                                msg,
                                ToolTipIcon.Info
                            );
                        });
                    }
                    catch
                    {
                        // Silently ignore clean errors
                    }
                });
            }
            catch
            {
                // Silently ignore clean errors
            }
        }

        private int GetMemoryUsagePercent()
        {
            return MemoryOptimizer.GetMemoryStats().Percent;
        }

        private void UpdateTrayIcon(int percent)
        {
            try
            {
                if (_trayIcon == null) return;

                // Determine color based on usage
                Color bgColor;
                if (percent >= 90) bgColor = Color.FromArgb(220, 53, 69);      // Red
                else if (percent >= 70) bgColor = Color.FromArgb(255, 193, 7);  // Yellow
                else bgColor = Color.FromArgb(40, 167, 69);                     // Green

                // Create a 16x16 icon with the percentage text
                var bmp = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(bgColor);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    string text = percent.ToString();
                    float fontSize = text.Length > 2 ? 6.5f : 8f;
                    using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
                    using var brush = new SolidBrush(Color.White);

                    var size = g.MeasureString(text, font);
                    float x = (16 - size.Width) / 2;
                    float y = (16 - size.Height) / 2;
                    g.DrawString(text, font, brush, x, y);
                }

                var newIcon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                var oldIcon = _currentIcon;
                _trayIcon.Icon = newIcon;
                _currentIcon = newIcon;

                // Cleanup old icon
                if (oldIcon != null)
                {
                    try { Win32Api.DestroyIcon(oldIcon.Handle); } catch { }
                }

                bmp.Dispose();
            }
            catch
            {
                // Fallback: use app icon if available
            }
        }

        public void ShowMinimizedNotification()
        {
            _trayIcon?.ShowBalloonTip(
                2000,
                "KitLugia",
                "Monitorando RAM em segundo plano. Clique duas vezes para abrir.",
                ToolTipIcon.Info
            );
        }

        // 🔥 MÉTODO DE VERIFICAÇÃO DE SAÚDE DO TRAY ICON
        public bool IsTrayIconHealthy()
        {
            try
            {
                if (_trayIcon == null)
                {
                    KitLugia.Core.Logger.Log("❌ Tray Icon é null");
                    return false;
                }

                if (!_trayIcon.Visible)
                {
                    KitLugia.Core.Logger.Log("❌ Tray Icon não está visível");
                    return false;
                }

                if (string.IsNullOrEmpty(_trayIcon.Text))
                {
                    KitLugia.Core.Logger.Log("❌ Tray Icon Text está vazio");
                    return false;
                }

                if (_trayIcon.ContextMenuStrip == null)
                {
                    KitLugia.Core.Logger.Log("❌ Tray Icon ContextMenu é null");
                    return false;
                }

                // Testa se consegue atualizar o ícone
                UpdateTrayIcon(GetMemoryUsagePercent());
                
                KitLugia.Core.Logger.Log("✅ Tray Icon está saudável");
                return true;
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ Erro na verificação de saúde do Tray Icon: {ex.Message}");
                return false;
            }
        }

        // 🔥 MÉTODO DE RECUPERAÇÃO DO TRAY ICON
        public bool RecoverTrayIcon()
        {
            try
            {
                KitLugia.Core.Logger.Log("🔄 Tentando recuperar Tray Icon...");
                
                // Dispose do antigo
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }

                // Recria completamente
                _trayIcon = new NotifyIcon
                {
                    Text = "KitLugia RAM Monitor",
                    Visible = false
                };

                // Recria menu
                var menu = new ContextMenuStrip();
                var itemClean = new ToolStripMenuItem("🧹 Limpar RAM Agora");
                itemClean.Click += (s, e) => CleanRamNow();
                menu.Items.Add(itemClean);
                
                var itemOpen = new ToolStripMenuItem("🚀 Abrir KitLugia");
                itemOpen.Click += (s, e) => OnOpenMainWindow?.Invoke();
                menu.Items.Add(itemOpen);
                
                var itemExit = new ToolStripMenuItem("❌ Sair");
                itemExit.Click += (s, e) =>
                {
                    Dispose();
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                };
                menu.Items.Add(itemExit);

                _trayIcon.ContextMenuStrip = menu;
                _trayIcon.DoubleClick += (s, e) => OnOpenMainWindow?.Invoke();

                // Ativa
                UpdateTrayIcon(GetMemoryUsagePercent());
                _trayIcon.Visible = true;

                bool success = _trayIcon.Visible;
                KitLugia.Core.Logger.Log(success ? "✅ Tray Icon recuperado com sucesso" : "❌ Falha na recuperação do Tray Icon");
                
                return success;
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"❌ Erro na recuperação do Tray Icon: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            _monitorTimer.Stop();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_currentIcon != null)
            {
                try { Win32Api.DestroyIcon(_currentIcon.Handle); } catch { }
                _currentIcon = null;
            }
        }
        internal static class Win32Api
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public const uint GW_OWNER = 4;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const uint WM_CLOSE = 0x0010;

        // --- Extensões para Multi-Layer Accelerator ---
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref int processInformation,
            int processInformationLength);

        private const int ProcessIoPriority = 33;
        private const int ProcessPagePriority = 39;

        public static void SetProcessIoPriority(IntPtr handle, int priorityHint)
        {
            try
            {
                int pInfo = priorityHint; // 0=VeryLow, 1=Low, 2=Normal, 3=High, 4=Critical
                NtSetInformationProcess(handle, ProcessIoPriority, ref pInfo, sizeof(int));
            }
            catch { }
        }

        public static void SetProcessPagePriority(IntPtr handle, int pagePriority)
        {
            try
            {
                int pInfo = pagePriority; // 1-5 (5 is default/highest)
                NtSetInformationProcess(handle, ProcessPagePriority, ref pInfo, sizeof(int));
            }
            catch { }
        }
    }
}
}
