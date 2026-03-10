using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KitLugia.Core;
using KitLugia.GUI.Services;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;
using Application = System.Windows.Application;
using Microsoft.Win32.TaskScheduler; // 🔥 Adicionar Task Scheduler
using Task = System.Threading.Tasks.Task; // 🔥 Corrigir ambiguidade

namespace KitLugia.GUI.Pages
{
    public partial class TraySettingsPage : Page
    {
        private DispatcherTimer _refreshTimer;
        private const string AutoStartRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AutoStartValueName = "KitLugia";

        public TraySettingsPage()
        {
            InitializeComponent();
            LoadSettings();
            StartRamRefresh();
        }

        private void LoadSettings()
        {
            var tray = GetTrayService();
            if (tray == null) return;

            ChkEnableTray.IsChecked = tray.IsTrayEnabled;
            ChkAutoClean.IsChecked = tray.AutoCleanEnabled;
            SliderThreshold.Value = tray.AutoCleanThresholdPercent;
            SliderInterval.Value = tray.MonitorIntervalSeconds;
            TxtThreshold.Text = $"{tray.AutoCleanThresholdPercent}%";
            TxtInterval.Text = FormatInterval((int)SliderInterval.Value);

            // Select active mode
            switch (tray.SelectedCleaningMode)
            {
                case MemoryOptimizer.CleaningMode.Leve: ModeLeve.IsChecked = true; break;
                case MemoryOptimizer.CleaningMode.Normal: ModeNormal.IsChecked = true; break;
                case MemoryOptimizer.CleaningMode.Alta: ModeAlta.IsChecked = true; break;
                case MemoryOptimizer.CleaningMode.Bruta: ModeBruta.IsChecked = true; break;
            }

            // Background Features
            ChkGamePriority.IsChecked = tray.GamePriorityEnabled;
            ChkStandbyClean.IsChecked = tray.StandbyCleanEnabled;
            ChkAntiLeak.IsChecked = tray.MemoryLeakDetectionEnabled;
            ChkFocusAssist.IsChecked = tray.FocusAssistEnabled;
            ChkTurboBoot.IsChecked = tray.TurboBootEnabled;
            ChkTurboShutdown.IsChecked = tray.TurboShutdownEnabled;

            // Auto-start
            try
            {
                // 🔥 CORREÇÃO: Verificar Task Scheduler em vez de Registry
                using (var ts = new TaskService())
                {
                    var task = ts.GetTask("KitLugia");
                    ChkAutoStart.IsChecked = task?.Enabled == true;
                    
                    // Se não encontrou tarefa, verificar fallback no Registry
                    if (task == null)
                    {
                        using var regKey = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, false);
                        ChkAutoStart.IsChecked = regKey?.GetValue(AutoStartValueName) != null;
                    }
                }
            }
            catch 
            { 
                // Fallback para verificação de Registry
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, false);
                    ChkAutoStart.IsChecked = key?.GetValue(AutoStartValueName) != null;
                }
                catch 
                { 
                    ChkAutoStart.IsChecked = false; 
                }
            }

            LoadTurboApps();
        }

        private void LoadTurboApps()
        {
            try
            {
                var apps = StartupManager.GetStartupAppsWithDetails(true)
                    .Where(a => a.Location == "Turbo Boot (KitLugia)")
                    .ToList();

                ListTurboApps.ItemsSource = apps;
                TxtEmptyTurbo.Visibility = apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        private void StartRamRefresh()
        {
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (s, e) => RefreshRamDisplay();
            _refreshTimer.Start();
            RefreshRamDisplay();
        }

        private void RefreshRamDisplay()
        {
            try
            {
                var mem = GetMemoryStats();
                TxtPercentMain.Text = $"{mem.Percent}%";
                TxtTotalRam.Text = $"{mem.TotalGB:F1} GB";
                TxtUsedRam.Text = $"{mem.UsedGB:F1} GB";
                TxtFreeRam.Text = $"{mem.FreeGB:F1} GB";

                // Color coding
                if (mem.Percent >= 90) TxtPercentMain.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69));
                else if (mem.Percent >= 70) TxtPercentMain.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                else TxtPercentMain.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 167, 69));

                // Label
                if (mem.Percent >= 90) TxtStatusText.Text = "ESTADO: CRÍTICO";
                else if (mem.Percent >= 70) TxtStatusText.Text = "ESTADO: ALTO";
                else TxtStatusText.Text = "ESTADO: NORMAL";

                TxtStatusText.Foreground = TxtPercentMain.Foreground;
            }
            catch { }
        }

        // --- EVENT HANDLERS ---

        private async void BtnCleanNow_Click(object sender, RoutedEventArgs e)
        {
            var tray = GetTrayService();
            if (tray == null) return;

            if (Application.Current.MainWindow is MainWindow mw)
            {
                await mw.ExecuteWithLoadingAsync($"Limpando RAM (Modo: {tray.SelectedCleaningMode})...", () =>
                {
                    var memBefore = GetMemoryStats();
                    var result = MemoryOptimizer.Optimize(tray.SelectedCleaningMode);
                    var memAfter = GetMemoryStats();

                    int freedPercent = memBefore.Percent - memAfter.Percent;
                    double freedGB = memAfter.FreeGB - memBefore.FreeGB;

                    // Atualizar UI na thread principal
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TxtFreedLast.Text = $"Última limpeza: {DateTime.Now:HH:mm} ({(freedGB > 0 ? freedGB : 0):F2} GB)";
                        RefreshRamDisplay();
                        mw.ShowSuccess("Otimizado", result.Message);
                    });

                    return result;
                });
            }
        }

        private void Mode_Click(object sender, RoutedEventArgs e)
        {
            var tray = GetTrayService();
            if (tray == null || !(sender is System.Windows.Controls.RadioButton rb)) return;

            string modeStr = rb.Tag?.ToString() ?? "Normal";
            Enum.TryParse(modeStr, out MemoryOptimizer.CleaningMode mode);
            tray.SelectedCleaningMode = mode;
            tray.SaveSettings();
        }

        private void ChkAutoClean_Click(object sender, RoutedEventArgs e)
        {
            var tray = GetTrayService();
            if (tray != null)
            {
                tray.AutoCleanEnabled = ChkAutoClean.IsChecked == true;
                tray.SaveSettings();
            }
        }

        private void ChkEnableTray_Click(object sender, RoutedEventArgs e)
        {
            var tray = GetTrayService();
            if (tray != null)
            {
                tray.SetTrayEnabled(ChkEnableTray.IsChecked == true);
                tray.SaveSettings();
            }
        }

        private void ChkBackgroundFeature_Click(object sender, RoutedEventArgs e)
        {
            var tray = GetTrayService();
            if (tray == null || !(sender is System.Windows.Controls.CheckBox cb)) return;

            if (cb == ChkGamePriority) tray.GamePriorityEnabled = cb.IsChecked == true;
            else if (cb == ChkStandbyClean) tray.StandbyCleanEnabled = cb.IsChecked == true;
            else if (cb == ChkAntiLeak) tray.MemoryLeakDetectionEnabled = cb.IsChecked == true;
            else if (cb == ChkFocusAssist) tray.FocusAssistEnabled = cb.IsChecked == true;
            else if (cb == ChkTurboBoot) tray.TurboBootEnabled = cb.IsChecked == true;
            else if (cb == ChkTurboShutdown) tray.TurboShutdownEnabled = cb.IsChecked == true;

            tray.SaveSettings();
        }

        private void ChkAutoStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔥 CORREÇÃO: Usar Task Scheduler em vez de Registry
                TrayIconService.SetAutoStart(ChkAutoStart.IsChecked == true);
                
                // Verificar se funcionou atualizando o estado
                try
                {
                    using (var ts = new TaskService())
                    {
                        var task = ts.GetTask("KitLugia");
                        bool isTaskEnabled = task?.Enabled == true;
                        
                        if (ChkAutoStart.IsChecked == true && !isTaskEnabled)
                        {
                            // Se não conseguiu criar tarefa, mostrar aviso
                            if (Application.Current.MainWindow is MainWindow mw)
                            {
                                mw.ShowInfo("AVISO", "Tarefa agendada não pode ser criada. Usando fallback sem privilégios admin.");
                            }
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        private void SliderThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtThreshold == null) return;
            int val = (int)SliderThreshold.Value;
            TxtThreshold.Text = $"{val}%";
            var tray = GetTrayService();
            if (tray != null)
            {
                tray.AutoCleanThresholdPercent = val;
                tray.SaveSettings();
            }
        }

        private void SliderInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtInterval == null) return;
            int val = (int)SliderInterval.Value;
            TxtInterval.Text = FormatInterval(val);
            var tray = GetTrayService();
            if (tray != null)
            {
                tray.MonitorIntervalSeconds = val;
                tray.SaveSettings();
            }
        }

        private async void BtnRemoveTurboApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string appName && Application.Current.MainWindow is MainWindow mw)
            {
                if (!await mw.ShowConfirmationDialog($"Remover '{appName}' do Turbo Boot?")) return;

                var result = StartupManager.RemoveFromKitLugia(appName);
                if (result.Success)
                {
                    mw.ShowSuccess("TURBO BOOT", result.Message);
                    LoadTurboApps();
                }
                else mw.ShowError("ERRO", result.Message);
            }
        }

        private async void BtnRestoreTurboApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string appName && Application.Current.MainWindow is MainWindow mw)
            {
                if (!await mw.ShowConfirmationDialog($"Restaurar '{appName}' para a inicialização padrão?")) return;

                var result = await Task.Run(() => StartupManager.RestoreToNormal(appName));
                if (result.Success)
                {
                    mw.ShowSuccess("RESTAURAR", result.Message);
                    LoadTurboApps();
                }
                else mw.ShowError("ERRO", result.Message);
            }
        }

        // --- HELPERS ---

        private static TrayIconService? GetTrayService()
        {
            if (Application.Current.MainWindow is MainWindow mw)
                return mw.TrayService;
            return null;
        }

        private static string FormatInterval(int seconds)
        {
            if (seconds < 60) return $"{seconds}s";
            return $"{seconds / 60}m {seconds % 60}s";
        }

        private struct MemoryInfo
        {
            public int Percent;
            public double TotalGB;
            public double UsedGB;
            public double FreeGB;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private MemoryInfo GetMemoryStats()
        {
            var m = new MEMORYSTATUSEX();
            m.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(m);
            GlobalMemoryStatusEx(ref m);

            return new MemoryInfo
            {
                Percent = (int)m.dwMemoryLoad,
                TotalGB = m.ullTotalPhys / (1024.0 * 1024.0 * 1024.0),
                FreeGB = m.ullAvailPhys / (1024.0 * 1024.0 * 1024.0),
                UsedGB = (m.ullTotalPhys - m.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0)
            };
        }
    }
}
