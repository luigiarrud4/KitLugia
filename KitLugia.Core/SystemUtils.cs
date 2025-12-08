using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class SystemUtils
    {
        #region Informações do Sistema

        public static string? GetServiceStartMode(string serviceName)
        {
            try
            {
                using var s = new ManagementObject($"Win32_Service.Name='{serviceName}'");
                s.Get();
                return s["StartMode"]?.ToString();
            }
            catch { return null; }
        }

        public static double GetTotalSystemRamGB()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
                var mem = searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["TotalVisibleMemorySize"];
                if (mem != null)
                {
                    ulong totalRamKB = Convert.ToUInt64(mem);
                    return totalRamKB / 1048576.0;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Obtém o tempo que o sistema está ligado (uptime).
        /// </summary>
        public static TimeSpan GetSystemUptime()
        {
            return TimeSpan.FromMilliseconds(Environment.TickCount64);
        }

        #endregion

        #region Execução de Processos

        public static string RunExternalProcess(string fileName, string arguments, bool hidden = false, bool waitForExit = true)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                CreateNoWindow = hidden,
                WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal,
                Verb = "runas"
            };

            if (waitForExit)
            {
                psi.RedirectStandardOutput = true;
                psi.UseShellExecute = false;
                psi.StandardOutputEncoding = Encoding.UTF8;
            }
            else
            {
                psi.UseShellExecute = true;
            }

            try
            {
                using var process = Process.Start(psi);
                if (process == null) return string.Empty;
                if (waitForExit)
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    return output;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "Processo cancelado pelo usuário.";
            }
            catch (Exception ex)
            {
                return $"Erro ao executar processo: {ex.Message}";
            }
            return string.Empty;
        }

        #endregion

        #region Utilitários de Sistema

        public static (bool Success, string Message) CreateRestorePoint()
        {
            string cmd = "try { Checkpoint-Computer -Description 'KitLUGIA_RestorePoint' -RestorePointType 'MODIFY_SETTINGS' } catch { Write-Host $_.Exception.Message }";
            string result = RunExternalProcess("powershell", $"-ExecutionPolicy Bypass -Command \"{cmd}\"", hidden: true);

            if (string.IsNullOrWhiteSpace(result) || !result.Contains("Exception"))
            {
                return (true, "Ponto de restauração criado com sucesso.");
            }
            else
            {
                return (false, $"Falha ao criar ponto de restauração: {result.Trim()}");
            }
        }

        public static bool IsAdmin()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static List<string> RunPreflightCheck()
        {
            var errors = new List<string>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem");
                if (!searcher.Get().Cast<ManagementObject>().Any()) errors.Add("- WMI não está retornando dados.");
            }
            catch { errors.Add("- Falha crítica ao acessar o WMI."); }

            try
            {
                const string testKey = @"Software\KitLUGIA_Test";
                Registry.CurrentUser.CreateSubKey(testKey)?.Close();
                Registry.CurrentUser.DeleteSubKey(testKey);
            }
            catch { errors.Add("- Falha crítica de acesso ao Registro."); }

            string[] requiredTools = { "sc.exe", "ipconfig.exe", "bcdedit.exe", "powershell.exe", "sfc.exe", "dism.exe", "powercfg.exe", "compact.exe" };
            foreach (var tool in requiredTools)
            {
                if (!CommandExists(tool)) errors.Add($"- Ferramenta essencial '{tool}' não encontrada no PATH.");
            }
            return errors;
        }

        private static bool CommandExists(string command)
        {
            var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';');
            return pathDirs.Any(dir => File.Exists(Path.Combine(dir.Trim(), command)));
        }

        #endregion
    }
}