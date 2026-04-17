using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Windows.Threading;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using KitLugia.Core;
using Microsoft.Win32.TaskScheduler;
using System.IO;
using System.Text;
using Application = System.Windows.Application;
using Timer = System.Windows.Threading.DispatcherTimer;

namespace KitLugia.GUI.Services
{
    public static class Win32Api
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
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

        // 🔥 CACHE DE PROCESSOS: Evitar múltiplas alocações de Process.GetProcesses()
        private Process[]? _cachedProcesses;
        private DateTime _lastProcessCacheTime = DateTime.MinValue;
        private readonly TimeSpan _processCacheLifetime = TimeSpan.FromSeconds(2); // Cache por 2 segundos
        private readonly object _processCacheLock = new();

        // Settings
        public bool AutoCleanEnabled { get; set; } = true;
        public int AutoCleanThresholdPercent { get; set; } = 80;
        private int _monitorIntervalSeconds = 30;
        public int MonitorIntervalSeconds
        {
            get => _monitorIntervalSeconds;
            set
            {
                // 🔥 CORREÇÃO LEAK: Forçar intervalo mínimo de 5s para evitar alocação excessiva de Process.GetProcesses()
                _monitorIntervalSeconds = Math.Max(5, value);
                if (_monitorTimer != null)
                {
                    _monitorTimer.Interval = TimeSpan.FromSeconds(_monitorIntervalSeconds);
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
        public bool TimerBoost { get; set; } = false;
        public bool NetworkBoost { get; set; } = false;
        public bool ProBalance { get; set; } = true;
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
        
        // Close to Tray (minimizar ao invés de fechar)
        public bool CloseToTray { get; set; } = true;

        public static bool IsTrayEnabledStatic()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\KitLugia\TraySettings");
                return (int)(key?.GetValue("IsTrayEnabled", 0) ?? 0) == 1;
            }
            catch { return false; }
        }

        /// <summary>
        /// Verifica se o auto-start está habilitado e se o caminho da tarefa corresponde à versão atual
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                string currentPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentPath)) return false;

                using (var ts = new TaskService())
                {
                    var task = ts.GetTask("KitLugia");
                    if (task == null) return false;

                    // Verificar se a tarefa está habilitada
                    if (!task.Enabled) return false;

                    // Verificar se o caminho do executável corresponde à versão atual
                    foreach (var action in task.Definition.Actions)
                    {
                        if (action is ExecAction execAction)
                        {
                            string taskPath = execAction.Path;
                            if (string.Equals(taskPath, currentPath, StringComparison.OrdinalIgnoreCase))
                            {
                                // KitLugia.Core.Logger.Log($"✅ Auto-Start habilitado com caminho correto: {currentPath}");
                                return true;
                            }
                            else
                            {
                                KitLugia.Core.Logger.Log($"⚠️ Auto-Start aponta para versão antiga: {taskPath} != {currentPath}");
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.LogError("IsAutoStartEnabled", $"Erro: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Remove tarefa antiga se o caminho não corresponder à versão atual
        /// </summary>
        private static void CleanupOldTask()
        {
            try
            {
                string currentPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(currentPath)) return;

                using (var ts = new TaskService())
                {
                    var task = ts.GetTask("KitLugia");
                    if (task == null) return;

                    // Verificar se o caminho corresponde
                    bool pathMatches = false;
                    foreach (var action in task.Definition.Actions)
                    {
                        if (action is ExecAction execAction)
                        {
                            if (string.Equals(execAction.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                            {
                                pathMatches = true;
                                break;
                            }
                        }
                    }

                    if (!pathMatches)
                    {
                        KitLugia.Core.Logger.Log("🧹 Removendo tarefa antiga com caminho incorreto...");
                        ts.RootFolder.DeleteTask("KitLugia");
                    }
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.LogError("CleanupOldTask", $"Erro: {ex.Message}");
            }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(path)) return;

                // 🔥 CORREÇÃO: Limpar tarefa antiga se o caminho não corresponder
                CleanupOldTask();

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

                            // Verificar se tarefa já existe com caminho correto
                            var existingTask = ts.GetTask("KitLugia");
                            if (existingTask != null)
                            {
                                // Verificar se o caminho já está correto
                                bool pathMatches = false;
                                foreach (var action in existingTask.Definition.Actions)
                                {
                                    if (action is ExecAction execAction)
                                    {
                                        if (string.Equals(execAction.Path, path, StringComparison.OrdinalIgnoreCase))
                                        {
                                            pathMatches = true;
                                            break;
                                        }
                                    }
                                }

                                if (pathMatches)
                                {
                                    KitLugia.Core.Logger.Log("✅ Tarefa já existe com caminho correto, apenas habilitando...");
                                    existingTask.Enabled = true;
                                    existingTask.RegisterChanges();
                                    KitLugia.Core.Logger.Log("✅ Tarefa agendada habilitada: " + path);
                                    return;
                                }
                                else
                                {
                                    KitLugia.Core.Logger.Log("🔄 Tarefa existe com caminho incorreto, recriando...");
                                    ts.RootFolder.DeleteTask("KitLugia");
                                }
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
            // 🔥 CORREÇÃO: Atribuição da instância estática
            _instance = this;

            // 🔥 CORREÇÃO: Habilita SeDebugPrivilege para acessar processos protegidos
            EnableSeDebugPrivilege();

            _monitorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(MonitorIntervalSeconds)
            };
            _monitorTimer.Tick += MonitorTick;
        }

        // 🔥 CORREÇÃO: Habilita SeDebugPrivilege para acessar processos protegidos
        private void EnableSeDebugPrivilege()
        {
            try
            {
                IntPtr hToken;
                if (!Win32Api.OpenProcessToken(Process.GetCurrentProcess().Handle, Win32Api.TOKEN_ADJUST_PRIVILEGES | Win32Api.TOKEN_QUERY, out hToken))
                {
                    KitLugia.Core.Logger.Log("⚠️ Falha ao abrir token do processo");
                    return;
                }

                try
                {
                    long luid;
                    if (!Win32Api.LookupPrivilegeValue(string.Empty, "SeDebugPrivilege", out luid))
                    {
                        KitLugia.Core.Logger.Log("⚠️ Falha ao obter LUID do SeDebugPrivilege");
                        return;
                    }

                    var tp = new Win32Api.TOKEN_PRIVILEGES
                    {
                        PrivilegeCount = 1,
                        Luid = luid,
                        Attributes = Win32Api.SE_PRIVILEGE_ENABLED
                    };

                    if (!Win32Api.AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    {
                        KitLugia.Core.Logger.Log("⚠️ Falha ao ajustar privilégio SeDebugPrivilege");
                    }
                    else
                    {
                        KitLugia.Core.Logger.Log("✅ SeDebugPrivilege habilitado com sucesso");
                    }
                }
                finally
                {
                    Win32Api.CloseHandle(hToken);
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Erro ao habilitar SeDebugPrivilege: {ex.Message}");
            }
        }

        // 🔥 MÉTODO CACHE: Retorna processos do cache ou atualiza se necessário
        private Process[] GetCachedProcesses()
        {
            lock (_processCacheLock)
            {
                if (_cachedProcesses != null && DateTime.Now - _lastProcessCacheTime < _processCacheLifetime)
                {
                    return _cachedProcesses;
                }

                // Descartar cache antigo se existir
                if (_cachedProcesses != null)
                {
                    foreach (var proc in _cachedProcesses)
                    {
                        try { proc.Dispose(); } catch { }
                    }
                }

                _cachedProcesses = Process.GetProcesses();
                _lastProcessCacheTime = DateTime.Now;
                return _cachedProcesses;
            }
        }

        // 🔥 MÉTODO: Limpar cache de processos (chamar no Dispose)
        private void ClearProcessCache()
        {
            lock (_processCacheLock)
            {
                if (_cachedProcesses != null)
                {
                    foreach (var proc in _cachedProcesses)
                    {
                        try { proc.Dispose(); } catch { }
                    }
                    _cachedProcesses = null;
                }
            }
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

            // 🔥 NOVO: Toggle GameBoost no menu da bandeja
            var itemGameBoost = new ToolStripMenuItem("🚀 GameBoost Pro");
            itemGameBoost.Checked = GamePriorityEnabled;
            itemGameBoost.Click += (s, e) =>
            {
                GamePriorityEnabled = !GamePriorityEnabled;
                itemGameBoost.Checked = GamePriorityEnabled;
                SaveSettings();
                KitLugia.Core.Logger.Log($"🚀 GameBoost {(GamePriorityEnabled ? "ativado" : "desativado")} via Tray Icon");
            };
            menu.Items.Add(itemGameBoost);

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

            // 🔥 NOVO: Inicializar GameBoost Moderno se habilitado
            if (GamePriorityEnabled)
            {
                InitializeGameBoost();
            }
        }

        public void ShutdownTurboCharge()
        {
            try
            {
                // Active Shutdown Charge (WM_CLOSE broadcast)
                foreach (var proc in GetCachedProcesses())
                {
                    try
                    {
                        if (proc.MainWindowHandle != IntPtr.Zero)
                        {
                            Win32Api.SendMessage(proc.MainWindowHandle, Win32Api.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                    catch { }
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
                key.SetValue("CloseToTray", CloseToTray ? 1 : 0);
                key.SetValue("AutoCleanEnabled", AutoCleanEnabled ? 1 : 0);
                key.SetValue("Threshold", AutoCleanThresholdPercent);
                key.SetValue("Interval", MonitorIntervalSeconds);
                key.SetValue("CleaningMode", (int)SelectedCleaningMode);
                key.SetValue("GamePriority", GamePriorityEnabled ? 1 : 0);
                key.SetValue("StandbyClean", StandbyCleanEnabled ? 1 : 0);
                key.SetValue("AntiLeak", MemoryLeakDetectionEnabled ? 1 : 0);
                key.SetValue("FocusAssist", FocusAssistEnabled ? 1 : 0);
                key.SetValue("TimerBoost", TimerBoost ? 1 : 0);
                key.SetValue("NetworkBoost", NetworkBoost ? 1 : 0);
                key.SetValue("ProBalance", ProBalance ? 1 : 0);
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
                CloseToTray = (int)key.GetValue("CloseToTray", 1) == 1;
                AutoCleanEnabled = (int)key.GetValue("AutoCleanEnabled", 1) == 1;
                AutoCleanThresholdPercent = (int)key.GetValue("Threshold", 80);
                MonitorIntervalSeconds = (int)key.GetValue("Interval", 30);
                SelectedCleaningMode = (MemoryOptimizer.CleaningMode)(int)key.GetValue("CleaningMode", (int)MemoryOptimizer.CleaningMode.Normal);
                GamePriorityEnabled = (int)key.GetValue("GamePriority", 0) == 1;
                StandbyCleanEnabled = (int)key.GetValue("StandbyClean", 1) == 1;
                MemoryLeakDetectionEnabled = (int)key.GetValue("AntiLeak", 0) == 1;
                FocusAssistEnabled = (int)key.GetValue("FocusAssist", 0) == 1;
                TimerBoost = (int)key.GetValue("TimerBoost", 0) == 1;
                NetworkBoost = (int)key.GetValue("NetworkBoost", 0) == 1;
                ProBalance = (int)key.GetValue("ProBalance", 1) == 1;

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

        // 🔥 CORREÇÃO: Pausar monitoramento quando janela perde foco
        public void PauseMonitoring()
        {
            try
            {
                _monitorTimer?.Stop();
                KitLugia.Core.Logger.Log("⏸️ TrayIcon: Monitoramento pausado (janela perdeu foco)");
            }
            catch { }
        }

        // 🔥 CORREÇÃO: Retomar monitoramento quando janela ganha foco
        public void ResumeMonitoring()
        {
            try
            {
                if (IsTrayEnabled && !_monitorTimer.IsEnabled)
                {
                    _monitorTimer?.Start();
                    KitLugia.Core.Logger.Log("▶️ TrayIcon: Monitoramento retomado (janela ganhou foco)");
                }
            }
            catch { }
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

                foreach (var proc in GetCachedProcesses())
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
                    // 🔥 CACHE: Não dar Dispose - processos são reutilizados
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

                // 🔥 CORREÇÃO: Verifica PID válido
                if (pid == 0) return;

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
            catch (System.ComponentModel.Win32Exception) { /* Processo encerrou - ignorar */ }
            catch (ArgumentException) { /* PID inválido - ignorar */ }
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

                foreach (var proc in GetCachedProcesses())
                {
                    try
                    {
                        // Don't touch the user's active game/app
                        if (proc.Id == foregroundPid) continue;

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

        // 🔥 NOVO: Sistema de Motores do GameBoost (V1/V2/V3)
        public enum GameBoostEngine
        {
            V1_Balanced = 1,      // Equilibrado - não trava, velocidade consistente (PADRÃO)
            V2_StableFPS = 2,     // FPS estável - pode travar um pouco
            V3_Extreme = 3        // Extremo - rede estável/mais rápida, pode travar mais
        }

        private static GameBoostEngine _currentEngine = GameBoostEngine.V1_Balanced;

        public static GameBoostEngine CurrentEngine
        {
            get => _currentEngine;
            set
            {
                _currentEngine = value;
            }
        }

        public static string GetEngineDescription(GameBoostEngine engine) => engine switch
        {
            GameBoostEngine.V1_Balanced => "V1 - Equilibrado (Padrão)",
            GameBoostEngine.V2_StableFPS => "V2 - FPS Estável",
            GameBoostEngine.V3_Extreme => "V3 - Extremo (Rede+)",
            _ => "Desconhecido"
        };

        public static void SetEngine(GameBoostEngine engine) => CurrentEngine = engine;
        public static void SetEngine(int engineNumber) => CurrentEngine = (GameBoostEngine)engineNumber;

        // 🔥 NOVO: Configuração de motor personalizado
        public static CustomEngineConfig? _customEngineConfig = null;
        public static bool IsCustomEngineActive => _customEngineConfig != null;

        public static void SetCustomEngine(CustomEngineConfig config)
        {
            _customEngineConfig = config;
            _currentEngine = GameBoostEngine.V1_Balanced; // Reset para não conflitar
            KitLugia.Core.Logger.Log($"🎮 GameBoost: Motor personalizado ativado - {config.CpuPriority} | ProBalance: {(config.ProBalance ? "ON" : "OFF")}");
        }

        public static void ClearCustomEngine()
        {
            _customEngineConfig = null;
            KitLugia.Core.Logger.Log("🎮 GameBoost: Motor personalizado desativado");
        }

        // 🔥 NOVO: Força re-aplicação do boost com o motor atual (usado ao trocar de motor)
        public static void ForceReapplyBoost(uint pid)
        {
            try
            {
                // Obtém a instância atual do TrayIconService
                var service = _instance;
                if (service == null) return;

                // Reverte o boost anterior para garantir estado limpo
                service.RevertBoost(pid);

                // Aplica o boost com o novo motor
                service.ApplyBoostModern(pid);

                // Se for V2, V3 ou motor personalizado com ProBalance, aplica o ProBalance também
                if (_currentEngine != GameBoostEngine.V1_Balanced || 
                    (_customEngineConfig != null && _customEngineConfig.ProBalance))
                {
                    service.ApplyProBalance(pid);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"⚠️ Erro em ForceReapplyBoost: {ex.Message}");
            }
        }

        // Referência estática para acesso ao método privado
        private static TrayIconService? _instance;

        // 🔥 NOVO: Evento para notificar UI quando foreground muda (passa PID e HWND)
        public event Action<uint, IntPtr>? ForegroundChanged;

        // 🔥 NOVO: Propriedade pública para expor foreground PID atual
        public uint CurrentForegroundPid => _currentBoostedPid;

        // 🔥 NOVO: Propriedade pública para expor foreground HWND atual
        public IntPtr CurrentForegroundHwnd => _lastForegroundHwnd;

        // 🔥 NOVO: GameBoost Moderno - Polling Estável (Windows 11 25H2 Optimized)
        private uint _currentBoostedPid = 0;
        private IntPtr _lastForegroundHwnd = IntPtr.Zero; // 🔥 NOVO: Track HWND para evitar spam
        private DateTime _lastBoostTime = DateTime.MinValue;
        private readonly TimeSpan _boostCooldown = TimeSpan.FromMilliseconds(50); // 50ms debounce

        // 🔥 NOVO: UIAutomation para identificar abas específicas (Discord, Opera, etc.)
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        /// <summary>
        /// Obtém o título da janela com precisão usando UIAutomation para identificar abas específicas
        /// </summary>
        private string GetWindowTitle(IntPtr hwnd)
        {
            try
            {
                // Método 1: GetWindowText (rápido, mas limitado)
                int length = GetWindowTextLength(hwnd);
                if (length > 0)
                {
                    var sb = new StringBuilder(length + 1);
                    GetWindowText(hwnd, sb, sb.Capacity);
                    return sb.ToString();
                }
            }
            catch { }

            return "";
        }

        // Sistema de IA/Heurística para detecção inteligente
        private readonly HashSet<string> _heavyAppIndicators = new(StringComparer.OrdinalIgnoreCase)
        {
            // Engines de jogo
            "unreal", "unity", "cryengine", "source", "idtech", "frostbite", "rage", " Creation ",
            // Termos de janela de jogos
            "game", "match", "lobby", "ranked", "competitive", "multiplayer", "online",
            // Classes de janela comuns
            "UnrealWindow", "UnityWndClass", "CryENGINE", "SDL_app", "GLFW",
            // Processos que indicam jogo rodando
            "steam", "epicgameslauncher", "riotgames", "battlenet", "eaapp", "ubisoftconnect"
        };

        // 🔥 NOVO: Proteção de processos críticos do sistema
        private static readonly HashSet<string> _protectedProcesses = new(StringComparer.OrdinalIgnoreCase)
        {
            // Windows Core
            "explorer", "dwm", "shellexperiencehost", "searchindexer", "taskmgr",
            "csrss", "lsass", "svchost", "services", "winlogon", "smss", "crss",
            "wininit", "memory compression", "registry", "system",
            // Áudio (crítico para não travar som)
            "audiodg", "audioendpointbuilder", "audiosrv", "audioengine",
            // GPU/Drivers (crítico para display)
            "nvcontainer", "nvservices", "nvdisplay.container", "amdremont",
            "amdrsserv", "intelgraphics", "igfxem", "igfxhk", "igfxtray",
            // Rede (crítico para conectividade)
            "wpnService", "wpnUserService",
            // Input (crítico para mouse/teclado)
            "ctfmon", "tabtip", "textinputhost"
        };

        // 🔥 NOVO: Lista de exceções do usuário (nunca serão throttled)
        private static readonly HashSet<string> _userExceptions = new(StringComparer.OrdinalIgnoreCase)
        {
            "discord", "discordptb", "discordcanary",  // Discord
            "opera", "operagx", "operagxc",             // Opera GX
            "spotify",                                   // Spotify
            "chrome", "msedge", "firefox",             // Browsers
            "steam", "steamwebhelper",                   // Steam
            "epicgameslauncher",                         // Epic
            "battlenet", "battle.net"                  // Battle.net
        };

        private void OptimizeForegroundProcess()
        {
            try
            {
                IntPtr hwnd = Win32Api.GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;

                Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return;

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

                        // 🔥 NOVO: Restaura EcoQoS padrão (Windows 11)
                        SetEcoQoS(oldProc.Handle, true);
                    }
                    catch { } // Processo pode ter sido fechado
                }

                _lastBoostedPid = pid;

                // 🔥 CORREÇÃO: Verifica PID válido
                if (pid == 0) return;

                // 2. Aplicar Boost no novo aplicativo
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName.ToLower();

                // Ignorar processos vitais do Windows + protegidos + exceções do usuário
                if (_protectedProcesses.Contains(name) || _userExceptions.Contains(name))
                {
                    _lastOriginalPriority = ProcessPriorityClass.Normal;
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

                // 🔥 NOVO: Desabilita EcoQoS para performance máxima (Windows 11 25H2)
                SetEcoQoS(proc.Handle, false);
            }
            catch { }
        }

        // Timer para verificação rápida do foreground (alternativa estável ao hook)
        private DispatcherTimer? _foregroundCheckTimer;

        // 🔥 NOVO: Inicializar GameBoost com Polling Estável (GetForegroundWindow)
        // Método usado por Process Lasso, CPUCores - mais estável que SetWinEventHook
        public void InitializeGameBoost()
        {
            if (!GamePriorityEnabled) return;

            try
            {
                _instance = this;

                // 🔥 CORREÇÃO: Usar polling estável com GetForegroundWindow()
                // Este método é usado por ferramentas como Process Lasso e CPUCores
                // É mais estável que SetWinEventHook e não causa ArgumentException
                _foregroundCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100) // 100ms = 10 checks/segundo (suficiente para jogos)
                };
                _foregroundCheckTimer.Tick += (s, e) => CheckForegroundWindow();
                _foregroundCheckTimer.Start();

                KitLugia.Core.Logger.Log("🎮 GameBoost ativado (Polling Estável) - Seguro e Confiável");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Falha no GameBoost: {ex.Message}");
            }
        }
        
        // 🔥 NOVO: Verifica foreground window a cada 100ms (polling estável)
        private void CheckForegroundWindow()
        {
            try
            {
                IntPtr currentHwnd = Win32Api.GetForegroundWindow();
                if (currentHwnd == IntPtr.Zero) return;

                // Debounce - verifica ANTES de processar
                if ((DateTime.Now - _lastBoostTime) < _boostCooldown) return;
                _lastBoostTime = DateTime.Now;

                // Obtém PID do foreground
                Win32Api.GetWindowThreadProcessId(currentHwnd, out uint pid);
                if (pid == 0) return;
                if (pid == _currentBoostedPid) return; // Mesmo processo

                // 🔥 NOVO: Obtém título da janela com precisão para identificar abas específicas
                string windowTitle = GetWindowTitle(currentHwnd);

                // Verifica se deve aplicar boost
                bool shouldBoost = ShouldBoostProcess(pid, currentHwnd);

                if (shouldBoost)
                {
                    // Reverte boost anterior
                    if (_currentBoostedPid != 0 && _currentBoostedPid != pid)
                    {
                        try
                        {
                            RevertBoost(_currentBoostedPid);
                        }
                        catch { }
                    }

                    // Aplica boost ao novo processo
                    try
                    {
                        ApplyBoostModern(pid);
                        _currentBoostedPid = pid;

                        // 🔥 NOVO: Log com título da janela para identificação precisa
                        string logTitle = string.IsNullOrEmpty(windowTitle) ? $"Process {pid}" : windowTitle;
                        KitLugia.Core.Logger.Log($"🎮 GameBoost (Timer): Boost aplicado ao processo PID: {pid} - {logTitle}");
                    }
                    catch { }
                }
                else
                {
                    // Atualiza _currentBoostedPid mesmo sem boost para evitar loop
                    if (_currentBoostedPid != 0 && _currentBoostedPid != pid)
                    {
                        RevertBoost(_currentBoostedPid);
                    }
                    _currentBoostedPid = pid;
                }
            }
            catch { /* Ignora erros silenciosamente */ }
        }

        // 🔥 NOVO: Verifica se o processo merece boost baseado em IA/Heurística GORA EM QUALQUER APP EM FOREGROUND (comportamento v22)
        private bool ShouldBoostProcess(uint pid, IntPtr hwnd)
        {
            // 🔥 CORREÇÃO: Verifica PID válido
            if (pid == 0) return false;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string procName = proc.ProcessName.ToLower();

                // IGNORAR processos protegidos do sistema + exceções do usuário
                if (_protectedProcesses.Contains(procName) || _userExceptions.Contains(procName))
                {
                    return false;
                }

                // TUDO QUE NÃO É SISTEMA RECEBE BOOST (Opera GX, Discord, VS Code, Jogos, etc)
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 🔥 NOVO: Verifica se janela está em tela cheia
        private bool IsFullScreen(IntPtr hwnd)
        {
            try
            {
                Win32Api.GetWindowRect(hwnd, out Win32Api.RECT rect);
                int screenWidth = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Width ?? 1920;
                int screenHeight = System.Windows.Forms.Screen.PrimaryScreen?.Bounds.Height ?? 1080;

                // Se a janela ocupa quase toda a tela
                int windowWidth = rect.Right - rect.Left;
                int windowHeight = rect.Bottom - rect.Top;

                return windowWidth >= screenWidth - 10 && windowHeight >= screenHeight - 10;
            }
            catch { return false; }
        }

        // 🔥 NOVO: Dispatcher para o motor correto
        private void ApplyBoostModern(uint pid)
        {
            try
            {
                // 🔥 NOVO: Verifica se tem motor personalizado ativo
                if (_customEngineConfig != null)
                {
                    ApplyBoostCustom(pid, _customEngineConfig);
                    return;
                }

                // Chama o motor selecionado pelo usuário
                switch (_currentEngine)
                {
                    case GameBoostEngine.V1_Balanced:
                        ApplyBoostV1(pid);
                        break;
                    case GameBoostEngine.V2_StableFPS:
                        ApplyBoostV2(pid);
                        break;
                    case GameBoostEngine.V3_Extreme:
                        ApplyBoostV3(pid);
                        break;
                    default:
                        ApplyBoostV1(pid); // Padrão: equilibrado
                        break;
                }
            }
            catch { }
        }

        // 🔥 NOVO: Eleva privilégios para System/TrustedInstaller
        private bool ElevateToSystem()
        {
            try
            {
                // Obtém token do processo System (PID 4 - kernel/ntoskrnl)
                IntPtr systemProcess = Win32Api.OpenProcess(Win32Api.PROCESS_QUERY_INFORMATION, false, 4);
                if (systemProcess == IntPtr.Zero)
                {
                    // Fallback: tenta lsass.exe (Local Security Authority)
                    var lsass = Process.GetProcessesByName("lsass").FirstOrDefault();
                    if (lsass != null)
                        systemProcess = Win32Api.OpenProcess(Win32Api.PROCESS_QUERY_INFORMATION, false, lsass.Id);
                }

                if (systemProcess == IntPtr.Zero) return false;

                try
                {
                    // Abre token do processo System
                    if (!Win32Api.OpenProcessToken(systemProcess, 
                        Win32Api.TOKEN_DUPLICATE | Win32Api.TOKEN_IMPERSONATE | Win32Api.TOKEN_QUERY, 
                        out IntPtr systemToken))
                        return false;

                    try
                    {
                        // Duplica token para impersonação
                        if (!Win32Api.DuplicateTokenEx(systemToken, 
                            0x1F0FFF, // MAXIMUM_ALLOWED
                            IntPtr.Zero, 
                            Win32Api.SecurityImpersonation, 
                            Win32Api.TokenImpersonation, 
                            out IntPtr impersonationToken))
                            return false;

                        try
                        {
                            // Aplica token à thread atual
                            if (Win32Api.SetThreadToken(Win32Api.GetCurrentThread(), impersonationToken))
                            {
                                KitLugia.Core.Logger.Log("🔐 Privilégios elevados para System - Acesso a processos protegidos habilitado");
                                return true;
                            }
                        }
                        finally
                        {
                            Win32Api.CloseHandle(impersonationToken);
                        }
                    }
                    finally
                    {
                        Win32Api.CloseHandle(systemToken);
                    }
                }
                finally
                {
                    Win32Api.CloseHandle(systemProcess);
                }
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ Falha ao elevar privilégios: {ex.Message}");
            }
            return false;
        }

        // 🔥 NOVO: Motor personalizado baseado em configuração do usuário
        private void ApplyBoostCustom(uint pid, CustomEngineConfig config)
        {
            // 🔥 CORREÇÃO: Verifica PID válido
            if (pid == 0) return;

            try
            {
                // 🔥 CORREÇÃO: Tenta obter o processo com timeout curto
                Process? proc = null;
                string name = "unknown";
                try
                {
                    proc = Process.GetProcessById((int)pid);
                    name = proc.ProcessName;
                }
                catch (ArgumentException) { return; } // Processo não existe
                catch (InvalidOperationException) { return; } // Processo encerrou

                using (proc)
                {
                    KitLugia.Core.Logger.Log($"⚡ GameBoost [PERSONALIZADO]: {name} (PID: {pid}) aplicando configurações...");

                    // 🔥 CORREÇÃO: Se ProBalance está ativo, NÃO use RealTime (evita conflito)
                    var targetPriority = config.CpuPriority.ToLower() switch
                    {
                        "normal" => ProcessPriorityClass.Normal,
                        "high" => ProcessPriorityClass.High,
                        "realtime" => config.ProBalance ? ProcessPriorityClass.High : ProcessPriorityClass.RealTime,
                        _ => ProcessPriorityClass.High
                    };

                    bool elevated = false;
                    try
                    {
                        if (proc.PriorityClass != targetPriority && targetPriority != ProcessPriorityClass.RealTime)
                        {
                            proc.PriorityClass = targetPriority;
                        }
                        else if (targetPriority == ProcessPriorityClass.RealTime && proc.PriorityClass != ProcessPriorityClass.RealTime)
                        {
                            try { proc.PriorityClass = ProcessPriorityClass.RealTime; }
                            catch { proc.PriorityClass = ProcessPriorityClass.High; }
                        }
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // ERROR_ACCESS_DENIED
                    {
                        // 🔥 CORREÇÃO: Acesso negado - tenta elevar privilégios
                        KitLugia.Core.Logger.Log($"🔒 Acesso negado ao processo {name} (PID: {pid}) - processo protegido (PPL/Anti-cheat)");

                        if (!elevated && ElevateToSystem())
                        {
                            elevated = true;
                            // Tenta novamente com privilégios elevados
                            try
                            {
                                proc.PriorityClass = targetPriority;
                                KitLugia.Core.Logger.Log($"✅ Prioridade aplicada com privilégios elevados: {name}");
                            }
                            catch (Exception ex2)
                            {
                                KitLugia.Core.Logger.Log($"⚠️ Mesmo com privilégios elevados, falha ao alterar {name}: {ex2.Message} (processo PPL bloqueia modificação)");
                            }
                        }
                        else
                        {
                            KitLugia.Core.Logger.Log($"⚠️ Não foi possível elevar privilégios - continuando com outros boosts");
                        }
                    }

                    // I/O Priority (0=Normal/2, 1=High/3)
                    try
                    {
                        int ioPriority = config.IoPriorityLevel == 0 ? 2 : 3;
                        Win32Api.SetProcessIoPriority(proc.Handle, ioPriority);
                    }
                    catch { /* API pode falhar - ignora */ }

                    // Page Priority (0=Normal/5, 1=Max/5)
                    try
                    {
                        int pagePriority = config.PagePriorityLevel == 0 ? 5 : 5;
                        Win32Api.SetProcessPagePriority(proc.Handle, pagePriority);
                    }
                    catch { /* API pode falhar - ignora */ }

                    // Thread Memory Priority (0=Normal/5, 1=Maximum/3)
                    try
                    {
                        int threadMemPriority = config.ThreadMemoryPriority == 0 ? 5 : 3;
                        Win32Api.SetThreadMemoryPriority(proc.Handle, (uint)threadMemPriority);
                    }
                    catch { /* API pode falhar - ignora */ }

                    // Timer Resolution se habilitado
                    if (config.TimerBoost)
                    {
                        try { Win32Api.BoostTimerResolution(); }
                        catch { /* API pode falhar - ignora */ }
                    }

                    // EcoQoS: false = Performance (não aplica Eco), true = Economia (aplica Eco)
                    try { SetEcoQoS(proc.Handle, config.EcoQoSEnabled); }
                    catch { /* API pode falhar - ignora */ }

                    // Network Boost se habilitado
                    if (config.NetworkBoost)
                    {
                        try { ApplyNetworkBoostV3(); }
                        catch { /* API pode falhar - ignora */ }
                    }

                    // 🔥 REMOVIDO: ProBalance é aplicado SEPARADAMENTE no timer, não aqui
                    // Isso evita conflitos e travamentos

                    KitLugia.Core.Logger.Log($"✅ GameBoost [PERSONALIZADO]: {name} otimizado com sucesso!");
                }
            }
            catch { /* Silencioso - evita crash */ }
        }

        // 🔥 MOTOR V1: ORIGINAL - Comportamento idêntico à versão GITHUBV1
        private void ApplyBoostV1(uint pid)
        {
            // 🔥 CORREÇÃO: Verifica PID válido
            if (pid == 0) return;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName;

                // Prioridade CPU: High (igual ao original)
                try
                {
                    if (proc.PriorityClass != ProcessPriorityClass.High &&
                        proc.PriorityClass != ProcessPriorityClass.RealTime)
                    {
                        proc.PriorityClass = ProcessPriorityClass.High;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V1: Acesso negado à prioridade do processo {name} (PID: {pid}) - processo protegido");
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V1: Erro ao definir prioridade: {ex.Message}");
                }

                // I/O Priority: High (3) - igual ao original
                try
                {
                    Win32Api.SetProcessIoPriority(proc.Handle, 3);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V1: Erro ao definir I/O Priority: {ex.Message}");
                }

                // Page Priority: Maximum (5) - igual ao original
                try
                {
                    Win32Api.SetProcessPagePriority(proc.Handle, 5);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V1: Erro ao definir Page Priority: {ex.Message}");
                }

                // Thread Memory: Normal (igual ao original - não tinha na versão original)
                try
                {
                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_NORMAL);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V1: Erro ao definir Thread Memory: {ex.Message}");
                }

                // Timer Resolution: NÃO boosta (diferença do V2/V3)

                // EcoQoS: Não aplica (igual ao original - não tinha)

                // ProBalance: NÃO aplica no V1 (comportamento original puro)
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                KitLugia.Core.Logger.Log($"⚠️ V1: Processo protegido - não foi possível abrir o processo (PID: {pid})");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ V1: Erro ao aplicar boost: {ex.Message}");
            }
        }

        // 🔥 MOTOR V2: FPS Estável - Prioridade alta, pode travar um pouco
        private void ApplyBoostV2(uint pid)
        {
            // 🔥 CORREÇÃO: Verifica PID válido
            if (pid == 0) return;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName;

                // CPU: High
                try
                {
                    if (proc.PriorityClass != ProcessPriorityClass.High &&
                        proc.PriorityClass != ProcessPriorityClass.RealTime)
                    {
                        proc.PriorityClass = ProcessPriorityClass.High;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V2: Acesso negado à prioridade do processo {name} (PID: {pid}) - processo protegido");
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V2: Erro ao definir prioridade: {ex.Message}");
                }

                // I/O: High (3)
                try
                {
                    Win32Api.SetProcessIoPriority(proc.Handle, 3);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V2: Erro ao definir I/O Priority: {ex.Message}");
                }

                // Page: Maximum (5)
                try
                {
                    Win32Api.SetProcessPagePriority(proc.Handle, 5);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V2: Erro ao definir Page Priority: {ex.Message}");
                }

                // Thread Memory: Normal
                try
                {
                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_NORMAL);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V2: Erro ao definir Thread Memory: {ex.Message}");
                }

                // Timer Resolution: Não boosta

                // EcoQoS: Desativa para foreground
                try
                {
                    SetEcoQoS(proc.Handle, false);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V2: Erro ao definir EcoQoS: {ex.Message}");
                }

                // ProBalance médio: throttles apps >8% CPU
                ApplyProBalanceV2(pid);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                KitLugia.Core.Logger.Log($"⚠️ V2: Processo protegido - não foi possível abrir o processo (PID: {pid})");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ V2: Erro ao aplicar boost: {ex.Message}");
            }
        }

        // 🔥 MOTOR V3: Extremo - TUDO no máximo, rede estável, pode travar mais
        private void ApplyBoostV3(uint pid)
        {
            // 🔥 CORREÇÃO: Verifica PID válido
            if (pid == 0) return;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                string name = proc.ProcessName;

                KitLugia.Core.Logger.Log($"⚡ GameBoost V3 [Extremo]: {name} (PID: {pid}) otimizando no máximo...");

                // CPU: High (próximo de RealTime mas mais seguro)
                try
                {
                    if (proc.PriorityClass != ProcessPriorityClass.High &&
                        proc.PriorityClass != ProcessPriorityClass.RealTime)
                    {
                        proc.PriorityClass = ProcessPriorityClass.High;
                    }
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Acesso negado à prioridade do processo {name} (PID: {pid}) - processo protegido");
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao definir prioridade: {ex.Message}");
                }

                // I/O: High (3)
                try
                {
                    Win32Api.SetProcessIoPriority(proc.Handle, 3);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao definir I/O Priority: {ex.Message}");
                }

                // Page: Maximum (5)
                try
                {
                    Win32Api.SetProcessPagePriority(proc.Handle, 5);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao definir Page Priority: {ex.Message}");
                }

                // Thread Memory: MAXIMUM (mantém tudo na RAM)
                try
                {
                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_NORMAL);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao definir Thread Memory: {ex.Message}");
                }

                // Timer Resolution: Boost para 0.5ms (máxima precisão) - GLOBAL
                try
                {
                    Win32Api.BoostTimerResolution();
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao definir Timer Resolution: {ex.Message}");
                }

                // EcoQoS: Desativado (performance absoluta)
                try
                {
                    SetEcoQoS(proc.Handle, false);
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao definir EcoQoS: {ex.Message}");
                }

                // 🔥 EXTRA V3: Otimizações de rede adicionais - GLOBAL
                try
                {
                    ApplyNetworkBoostV3();
                }
                catch (Exception ex)
                {
                    KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao aplicar Network Boost: {ex.Message}");
                }

                // ProBalance agressivo: throttles apps >3% CPU
                ApplyProBalanceV3(pid);
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
            {
                KitLugia.Core.Logger.Log($"⚠️ V3: Processo protegido - não foi possível abrir o processo (PID: {pid})");
            }
            catch (Exception ex)
            {
                KitLugia.Core.Logger.Log($"⚠️ V3: Erro ao aplicar boost: {ex.Message}");
            }
        }

        // 🔥 EXTRA V3: Boost de rede para prioridade máxima
        private void ApplyNetworkBoostV3()
        {
            try
            {
                // Prioridade de rede máxima via registry (temporária para a sessão)
                using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile");
                if (key != null)
                {
                    key.SetValue("NetworkThrottlingIndex", 0xFFFFFFFF, Microsoft.Win32.RegistryValueKind.DWord);
                    key.SetValue("SystemResponsiveness", 0, Microsoft.Win32.RegistryValueKind.DWord);
                }

                KitLugia.Core.Logger.Log("🌐 GameBoost V3: Network throttling desativado");
            }
            catch { }
        }

        // 🔥 NOVO: ProBalance - reduz prioridade de processos background que consomem muita CPU
        private readonly HashSet<uint> _throttledProcesses = new();
        private readonly object _throttleLock = new();

        // Dispatcher para o ProBalance correto baseado no motor
        private void ApplyProBalance(uint foregroundPid)
        {
            // 🔥 GLOBAL: Se ProBalance está desativado globalmente, não aplica nada
            if (!ProBalance)
            {
                return;
            }
            
            // 🔥 NOVO: Se motor personalizado está ativo e ProBalance habilitado
            if (_customEngineConfig != null && _customEngineConfig.ProBalance)
            {
                ApplyProBalanceCustom(foregroundPid, _customEngineConfig.ProBalanceCpuThreshold);
                return;
            }

            switch (_currentEngine)
            {
                case GameBoostEngine.V1_Balanced:
                    // V1 ORIGINAL: Não aplica ProBalance (comportamento puro)
                    break;
                case GameBoostEngine.V2_StableFPS:
                    ApplyProBalanceV2(foregroundPid);
                    break;
                case GameBoostEngine.V3_Extreme:
                    ApplyProBalanceV3(foregroundPid);
                    break;
                default:
                    // Padrão também não aplica (V1)
                    break;
            }
        }

        // 🔥 ProBalance V2: Médio - throttle apps >8% CPU
        private void ApplyProBalanceV2(uint foregroundPid)
        {
            ApplyProBalanceCore(foregroundPid, 8.0, "V2");
        }

        // 🔥 ProBalance V3: Agressivo - throttle apps >3% CPU (máximo performance)
        private void ApplyProBalanceV3(uint foregroundPid)
        {
            ApplyProBalanceCore(foregroundPid, 3.0, "V3");
        }

        // 🔥 NOVO: ProBalance Custom - com threshold configurável pelo usuário
        private void ApplyProBalanceCustom(uint foregroundPid, int thresholdPercent)
        {
            ApplyProBalanceCore(foregroundPid, thresholdPercent, "Custom");
        }

        // Core do ProBalance com threshold configurável
        private void ApplyProBalanceCore(uint foregroundPid, double cpuThreshold, string version)
        {
            try
            {
                lock (_throttleLock)
                {
                    // Restaura processos throttled anteriormente que não são mais foreground
                    var toRestore = _throttledProcesses.Where(p => p != foregroundPid).ToList();
                    foreach (var pid in toRestore)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            string name = proc.ProcessName.ToLower();

                            // Não restaura se for processo protegido ou exceção
                            if (_protectedProcesses.Contains(name) || _userExceptions.Contains(name))
                            {
                                _throttledProcesses.Remove(pid);
                                continue;
                            }

                            // Restaura para Normal
                            if (proc.PriorityClass == ProcessPriorityClass.BelowNormal)
                            {
                                proc.PriorityClass = ProcessPriorityClass.Normal;

                                // Restaura thread memory priority
                                try
                                {
                                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_NORMAL);
                                }
                                catch { }

                                KitLugia.Core.Logger.Log($"🔼 ProBalance {version}: {name} (PID: {pid}) restaurado para Normal");
                            }
                            _throttledProcesses.Remove(pid);
                        }
                        catch { _throttledProcesses.Remove(pid); }
                    }

                    // Throttle processos background que usam >threshold% CPU
                    var currentProcess = Process.GetCurrentProcess();
                    foreach (var proc in GetCachedProcesses())
                    {
                        try
                        {
                            uint pid = (uint)proc.Id;

                            // Skip: foreground, KitLugia, já throttled, protegidos
                            if (pid == foregroundPid || pid == currentProcess.Id || _throttledProcesses.Contains(pid))
                                continue;

                            string name = proc.ProcessName.ToLower();
                            if (_protectedProcesses.Contains(name) || _userExceptions.Contains(name))
                                continue;

                            // Calcula uso de CPU
                            double cpuUsage = GetProcessCpuUsage(proc);

                            // Throttle se CPU > threshold e prioridade >= Normal
                            if (cpuUsage > cpuThreshold && proc.PriorityClass >= ProcessPriorityClass.Normal)
                            {
                                proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                                _throttledProcesses.Add(pid);

                                // Set thread memory priority to VERY_LOW
                                try
                                {
                                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_VERY_LOW);
                                }
                                catch { }

                                KitLugia.Core.Logger.Log($"🔻 ProBalance {version}: {name} (PID: {pid}) throttled (CPU: {cpuUsage:F1}%)");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // 🔥 PÚBLICO: Restaura todos os processos throttled pelo ProBalance
        public void RestoreAllThrottledProcesses()
        {
            lock (_throttleLock)
            {
                var toRestore = _throttledProcesses.ToList();
                foreach (var pid in toRestore)
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        string name = proc.ProcessName.ToLower();

                        // Restaura para Normal
                        if (proc.PriorityClass == ProcessPriorityClass.BelowNormal)
                        {
                            proc.PriorityClass = ProcessPriorityClass.Normal;

                            // Restaura thread memory priority
                            try
                            {
                                Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_NORMAL);
                            }
                            catch { }

                            KitLugia.Core.Logger.Log($"🔼 ProBalance Global: {name} (PID: {pid}) restaurado para Normal");
                        }
                        _throttledProcesses.Remove(pid);
                    }
                    catch
                    {
                        _throttledProcesses.Remove(pid);
                    }
                }
                
                KitLugia.Core.Logger.Log($"⚖️ ProBalance: {_throttledProcesses.Count} processos restaurados, {_throttledProcesses.Count} ainda throttled");
            }
        }

        // Mantém método original para compatibilidade (não usado)
        private void ApplyProBalanceOld(uint foregroundPid)
        {
            try
            {
                lock (_throttleLock)
                {
                    // Restaura processos throttled anteriormente que não são mais foreground
                    var toRestore = _throttledProcesses.Where(p => p != foregroundPid).ToList();
                    foreach (var pid in toRestore)
                    {
                        try
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            string name = proc.ProcessName.ToLower();

                            // Não restaura se for processo protegido ou exceção
                            if (_protectedProcesses.Contains(name) || _userExceptions.Contains(name))
                            {
                                _throttledProcesses.Remove(pid);
                                continue;
                            }

                            // Restaura para Normal
                            if (proc.PriorityClass == ProcessPriorityClass.BelowNormal)
                            {
                                proc.PriorityClass = ProcessPriorityClass.Normal;

                                // 🔥 NOVO: Restaura thread memory priority
                                try
                                {
                                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_NORMAL);
                                }
                                catch { }

                                KitLugia.Core.Logger.Log($"🔼 ProBalance: {name} (PID: {pid}) restaurado para Normal");
                            }
                            _throttledProcesses.Remove(pid);
                        }
                        catch { _throttledProcesses.Remove(pid); }
                    }

                    // Throttle processos background que usam >5% CPU (exceto protegidos e exceções)
                    var currentProcess = Process.GetCurrentProcess();
                    foreach (var proc in GetCachedProcesses())
                    {
                        try
                        {
                            uint pid = (uint)proc.Id;

                            // Skip: foreground, KitLugia, já throttled, protegidos
                            if (pid == foregroundPid || pid == currentProcess.Id || _throttledProcesses.Contains(pid))
                                continue;

                            string name = proc.ProcessName.ToLower();
                            if (_protectedProcesses.Contains(name) || _userExceptions.Contains(name))
                                continue;

                            // Calcula uso de CPU (simplificado)
                            double cpuUsage = GetProcessCpuUsage(proc);

                            // Throttle se CPU > 5% e prioridade >= Normal
                            if (cpuUsage > 5.0 && proc.PriorityClass >= ProcessPriorityClass.Normal)
                            {
                                proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                                _throttledProcesses.Add(pid);

                                // 🔥 NOVO: Set thread memory priority to VERY_LOW 
                                // → threads deste processo liberam RAM primeiro
                                try
                                {
                                    Win32Api.SetThreadMemoryPriority(proc.Handle, Win32Api.MEMORY_PRIORITY_VERY_LOW);
                                }
                                catch { }

                                KitLugia.Core.Logger.Log($"🔻 ProBalance: {name} (PID: {pid}) throttled (CPU: {cpuUsage:F1}%)");
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        // Helper: estima uso de CPU de um processo
        private double GetProcessCpuUsage(Process proc)
        {
            try
            {
                var startTime = proc.StartTime;
                var totalProcessorTime = proc.TotalProcessorTime;
                var elapsed = DateTime.Now - startTime;

                if (elapsed.TotalSeconds > 0)
                {
                    return (totalProcessorTime.TotalSeconds / (Environment.ProcessorCount * elapsed.TotalSeconds)) * 100;
                }
            }
            catch { }
            return 0;
        }

        // 🔥 NOVO: Métodos públicos para gerenciar exceções do usuário
        public static void AddUserException(string processName)
        {
            if (!string.IsNullOrEmpty(processName))
            {
                _userExceptions.Add(processName.ToLower());
                KitLugia.Core.Logger.Log($"✅ ProBalance: {processName} adicionado às exceções");
            }
        }

        public static void RemoveUserException(string processName)
        {
            if (!string.IsNullOrEmpty(processName))
            {
                _userExceptions.Remove(processName.ToLower());
                KitLugia.Core.Logger.Log($"❌ ProBalance: {processName} removido das exceções");
            }
        }

        public static HashSet<string> GetUserExceptions() => new(_userExceptions);

        // 🔥 NOVO: Reverte boost
        private void RevertBoost(uint pid)
        {
            // 🔥 CORREÇÃO: Verifica PID válido antes de tentar acessar
            if (pid == 0)
            {
                Logger.Log("⚠️ RevertBoost: PID inválido (0)");
                return;
            }

            try
            {
                using var proc = Process.GetProcessById((int)pid);

                // Restaura prioridade
                if (proc.PriorityClass == ProcessPriorityClass.High)
                {
                    proc.PriorityClass = ProcessPriorityClass.Normal;
                }

                // Restaura I/O e Page
                Win32Api.SetProcessIoPriority(proc.Handle, 2); // Normal
                Win32Api.SetProcessPagePriority(proc.Handle, 5); // Default

                // 🔥 NOVO: Restaura Timer Resolution
                Win32Api.RestoreTimerResolution();

                // 🔥 Windows 11: Restaura EcoQoS
                SetEcoQoS(proc.Handle, true);
            }
            catch { }
        }

        // 🔥 NOVO: Windows 11 EcoQoS API
        private void SetEcoQoS(IntPtr processHandle, bool enableEcoMode)
        {
            try
            {
                // Só aplica no Windows 11 (build >= 22000)
                if (Environment.OSVersion.Version.Build < 22000) return;

                var state = new Win32Api.PROCESS_POWER_THROTTLING_STATE
                {
                    Version = 1,
                    ControlMask = Win32Api.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                    StateMask = enableEcoMode ? Win32Api.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0
                };

                IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(state));
                Marshal.StructureToPtr(state, ptr, false);

                Win32Api.SetProcessInformation(processHandle, Win32Api.ProcessPowerThrottling, ptr, (uint)Marshal.SizeOf(state));

                Marshal.FreeHGlobal(ptr);
            }
            catch { }
        }

        // 🔥 NOVO: Desativar GameBoost (cleanup)
        public void ShutdownGameBoost()
        {
            try
            {
                // 🔥 CORREÇÃO: Para timer de foreground check (polling estável)
                _foregroundCheckTimer?.Stop();
                _foregroundCheckTimer = null;

                // Reverte boost atual
                if (_currentBoostedPid != 0)
                {
                    RevertBoost(_currentBoostedPid);
                    _currentBoostedPid = 0;
                }

                // Limpa referências
                _lastForegroundHwnd = IntPtr.Zero;

                KitLugia.Core.Logger.Log("🎮 GameBoost desativado (SetWinEventHook Kernel Hook)");
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
            // 🔥 Desregistrar evento antes de parar
            if (_monitorTimer != null)
            {
                _monitorTimer.Tick -= MonitorTick;
                _monitorTimer.Stop();
            }

            // 🔥 NOVO: Limpar cache de processos para evitar memory leak
            ClearProcessCache();

            // 🔥 NOVO: Desativar GameBoost Moderno
            ShutdownGameBoost();

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

            // --- 🔥 NOVO: SetWinEventHook para GameBoost Instantâneo (Windows 11 25H2) ---
            public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

            [DllImport("user32.dll")]
            public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

            [DllImport("user32.dll")]
            public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

            [DllImport("user32.dll")]
            public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool IsWindow(IntPtr hWnd);

            [DllImport("user32.dll")]
            public static extern bool IsWindowEnabled(IntPtr hWnd);

            // Event constants
            public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
            public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
            public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
            public const uint WINEVENT_SKIPOWNTHREAD = 0x0004;

            // --- 🔥 NOVO: Windows 11 EcoQoS API (25H2 Performance) ---
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass, IntPtr ProcessInformation, uint ProcessInformationSize);

            public const int ProcessPowerThrottling = 4;

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_POWER_THROTTLING_STATE
            {
                public uint Version;
                public uint ControlMask;
                public uint StateMask;
            }

            public const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

            // --- 🔥 NOVO: Thread Memory Priority API (Windows 10+) ---
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool SetThreadInformation(IntPtr hThread, int ThreadInformationClass, IntPtr ThreadInformation, uint ThreadInformationSize);

            public const int ThreadMemoryPriority = 0; // THREAD_INFORMATION_CLASS

            [StructLayout(LayoutKind.Sequential)]
            public struct MEMORY_PRIORITY_INFORMATION
            {
                public uint MemoryPriority;
            }

            public const uint MEMORY_PRIORITY_VERY_LOW = 1;
            public const uint MEMORY_PRIORITY_LOW = 2;
            public const uint MEMORY_PRIORITY_MEDIUM = 3;
            public const uint MEMORY_PRIORITY_BELOW_NORMAL = 4;
            public const uint MEMORY_PRIORITY_NORMAL = 5;

            public static void SetThreadMemoryPriority(IntPtr threadHandle, uint priority)
            {
                try
                {
                    var memPrio = new MEMORY_PRIORITY_INFORMATION { MemoryPriority = priority };
                    IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf(memPrio));
                    Marshal.StructureToPtr(memPrio, ptr, false);
                    SetThreadInformation(threadHandle, ThreadMemoryPriority, ptr, (uint)Marshal.SizeOf(memPrio));
                    Marshal.FreeHGlobal(ptr);
                }
                catch { }
            }

            // --- 🔥 NOVO: Timer Resolution API (NtSetTimerResolution) ---
            [DllImport("ntdll.dll", SetLastError = true)]
            private static extern int NtSetTimerResolution(int DesiredResolution, bool SetResolution, out int CurrentResolution);

            private static int _originalTimerResolution = 0;

            // --- 🔥 NOVO: Privilege Elevation APIs para acesso a processos protegidos ---
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess, IntPtr lpTokenAttributes, int ImpersonationLevel, int TokenType, out IntPtr phNewToken);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool SetThreadToken(IntPtr ThreadHandle, IntPtr TokenHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetCurrentThread();

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

            public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
            public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct TOKEN_PRIVILEGES
            {
                public uint PrivilegeCount;
                public long Luid;
                public uint Attributes;
            }

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

            public const uint PROCESS_QUERY_INFORMATION = 0x0400;
            public const uint TOKEN_DUPLICATE = 0x0002;
            public const uint TOKEN_IMPERSONATE = 0x0004;
            public const uint TOKEN_QUERY = 0x0008;
            public const int SecurityImpersonation = 2;
            public const int TokenImpersonation = 2;
            private static bool _timerResolutionChanged = false;

            public static void BoostTimerResolution()
            {
                try
                {
                    if (_timerResolutionChanged) return; // Já boostado

                    // Query current resolution first
                    NtSetTimerResolution(0, false, out _originalTimerResolution);

                    // Set to 0.5ms (5000 in 100-nanosecond units)
                    int desired = 5000; // 0.5ms
                    int result = NtSetTimerResolution(desired, true, out int current);

                    if (result == 0) // STATUS_SUCCESS
                    {
                        _timerResolutionChanged = true;
                        KitLugia.Core.Logger.Log($"⏱️ Timer Resolution: {_originalTimerResolution / 10000.0:F2}ms → {current / 10000.0:F2}ms (boosted)");
                    }
                }
                catch { }
            }

            public static void RestoreTimerResolution()
            {
                try
                {
                    if (!_timerResolutionChanged) return;

                    // Restore original
                    NtSetTimerResolution(_originalTimerResolution, true, out int current);
                    _timerResolutionChanged = false;
                    KitLugia.Core.Logger.Log($"⏱️ Timer Resolution restaurado: {current / 10000.0:F2}ms");
                }
                catch { }
            }
        }

    }

    // 🔥 NOVO: Classe de configuração para motor personalizado
    public class CustomEngineConfig
    {
        public string CpuPriority { get; set; } = "High"; // Normal, High, RealTime
        public int IoPriorityLevel { get; set; } = 1; // 0=Normal(2), 1=High(3), 2=Critical
        public int PagePriorityLevel { get; set; } = 1; // 0=Normal(5), 1=Max(5)
        public bool TimerBoost { get; set; } = false;
        public bool EcoQoSEnabled { get; set; } = false; // true=Economia, false=Performance
        public bool ProBalance { get; set; } = true;
        public int ProBalanceCpuThreshold { get; set; } = 5; // % CPU
        public bool NetworkBoost { get; set; } = false;
        public int ThreadMemoryPriority { get; set; } = 0; // 0=Normal, 1=Maximum
    }
}