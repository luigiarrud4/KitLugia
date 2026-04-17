using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class DriverManager
    {
        private static List<DriverItem> _cachedDrivers = new();

        /// <summary>
        /// Obtém a lista de drivers do sistema via WMI de forma assíncrona.
        /// </summary>
        public static async Task<List<DriverItem>> GetSystemDriversAsync(bool includeMicrosoft = false)
        {
            return await Task.Run(() =>
            {
                var list = new List<DriverItem>();
                try
                {
                    using var searcher = new ManagementObjectSearcher("SELECT DeviceName, DriverProviderName, DriverVersion, DriverDate, InfName, DeviceID FROM Win32_PnPSignedDriver");
                    using var results = searcher.Get();

                    foreach (ManagementObject item in results)
                    {
                        using (item)
                        {
                            string provider = item["DriverProviderName"]?.ToString() ?? "Genérico";
                            string name = item["DeviceName"]?.ToString() ?? "Dispositivo Desconhecido";

                            bool isMicrosoft = provider == "Microsoft" || provider == "Microsoft Corporation";

                            if (!string.IsNullOrEmpty(name) && (includeMicrosoft || !isMicrosoft))
                            {
                                string rawDate = item["DriverDate"]?.ToString() ?? "";
                                string prettyDate = rawDate;

                                if (rawDate.Length >= 8)
                                    prettyDate = $"{rawDate.Substring(6, 2)}/{rawDate.Substring(4, 2)}/{rawDate.Substring(0, 4)}";

                                list.Add(new DriverItem
                                {
                                    DeviceName = name,
                                    Provider = provider,
                                    Version = item["DriverVersion"]?.ToString() ?? "0.0.0.0",
                                    Date = prettyDate,
                                    InfName = item["InfName"]?.ToString() ?? "",
                                    HardwareId = item["DeviceID"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("GetSystemDrivers", ex.Message);
                }

                _cachedDrivers = list.OrderBy(x => x.DeviceName).ToList();
                return _cachedDrivers;
            });
        }

        /// <summary>
        /// Verifica drivers antigos (data > 2 anos).
        /// </summary>
        public static async Task<List<DriverItem>> CheckForOutdatedDrivers()
        {
            Logger.Log("Iniciando verificação de drivers obsoletos...");
            
            var sourceList = _cachedDrivers.Any() ? _cachedDrivers : await GetSystemDriversAsync(false);

            return await Task.Run(() =>
            {
                var outdated = new List<DriverItem>();

                foreach (var driver in sourceList)
                {
                    if (DateTime.TryParse(driver.Date, out DateTime dDate))
                    {
                        if (dDate < DateTime.Now.AddYears(-2)) outdated.Add(driver);
                    }
                }

                Logger.Log($"[SCAN] Análise concluída. {outdated.Count} drivers parecem antigos.");
                return outdated;
            });
        }

        /// <summary>
        /// Instala drivers de arquivos compactados (CAB/ZIP), pastas ou arquivos INF diretamente.
        /// </summary>
        public static async Task<(bool Success, string Message)> SmartInstallDriver(string path)
        {
            Logger.Log($"Iniciando instalação inteligente: {path}");

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Logger.LogError("SmartInstall", "Arquivo não encontrado.");
                return (false, "Arquivo ou pasta não encontrado.");
            }

            // Se for pasta ou INF direto, manda instalar
            if (Directory.Exists(path) || path.EndsWith(".inf", StringComparison.OrdinalIgnoreCase))
            {
                string targetPath = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
                return InstallDriversFromFolder(targetPath);
            }

            // Se for arquivo compactado, extrai
            string ext = Path.GetExtension(path).ToLower();
            string tempFolder = Path.Combine(Path.GetTempPath(), "KitLugia_Driver_" + Guid.NewGuid().ToString().Substring(0, 8));

            try
            {
                Directory.CreateDirectory(tempFolder);
                Logger.Log($"Extraindo pacote para: {tempFolder}...");
                bool extracted = false;

                await Task.Run(() =>
                {
                    if (ext == ".zip")
                    {
                        ZipFile.ExtractToDirectory(path, tempFolder);
                        extracted = true;
                    }
                    else if (ext == ".cab")
                    {
                        // CAB do Windows Update precisa do comando 'expand'
                        string args = $"\"{path}\" -F:* \"{tempFolder}\"";
                        SystemUtils.RunExternalProcess("expand.exe", args, hidden: true);
                        extracted = true;
                    }
                });

                if (!extracted)
                {
                    Logger.LogError("SmartInstall", "Formato não suportado.");
                    return (false, "Formato não suportado. Use .CAB, .ZIP ou uma Pasta.");
                }

                // Instala da pasta temporária
                var result = InstallDriversFromFolder(tempFolder);

                // Limpeza
                try { Directory.Delete(tempFolder, true); } catch { }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError("SmartInstall", ex.Message);
                try { if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true); } catch { }
                return (false, $"Erro na extração: {ex.Message}");
            }
        }

        /// <summary>
        /// Instala todos os drivers de uma pasta (e subpastas) usando pnputil.
        /// </summary>
        public static (bool Success, string Message) InstallDriversFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return (false, "Pasta inválida.");

            try
            {
                Logger.Log($"Executando PnPUtil na pasta: {folderPath}");
                string args = $"/add-driver \"{folderPath}\\*.inf\" /subdirs /install";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                string output = process.StandardOutput?.ReadToEnd() ?? "";
                process.WaitForExit();

                // pnputil retorna 0 em sucesso, ou 259/outro no failed
                if (process.ExitCode == 0 || process.ExitCode == 259)
                {
                    Logger.Log("[SUCESSO] Driver instalado e adicionado ao repositório (ou nenhuma alteração necessária).");
                    return (true, "Instalação concluída com sucesso!");
                }
                else
                {
                    Logger.Log($"[FALHA] PnPUtil Código: {process.ExitCode}. Detalhes: {output}");
                    return (false, "Nenhum driver compatível foi instalado.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("InstallDrivers", ex.Message);
                return (false, ex.Message);
            }
        }

        public static (bool Success, string Message) UninstallDriver(string infName)
        {
            if (string.IsNullOrWhiteSpace(infName)) return (false, "Driver inválido.");

            try
            {
                Logger.Log($"Tentando remover driver: {infName}...");
                string args = $"/delete-driver {infName} /uninstall /force";

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                string output = process.StandardOutput?.ReadToEnd() ?? "";
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Logger.LogError("UninstallDriver", output);
                    if (output.Contains("in use", StringComparison.OrdinalIgnoreCase) || output.Contains("em uso", StringComparison.OrdinalIgnoreCase))
                        return (false, "O driver está em uso. Reinicie e tente novamente.");

                    return (false, $"Falha ao remover. Código: {process.ExitCode}");
                }

                Logger.Log("[SUCESSO] Driver removido.");
                return (true, "Driver removido com sucesso.");
            }
            catch (Exception ex)
            {
                Logger.LogError("UninstallDriver", ex.Message);
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Pesquisa segura no Catálogo Microsoft usando o ID de Hardware.
        /// </summary>
        public static void SearchDriverOnWeb(string deviceName, string hardwareId)
        {
            try
            {
                Logger.Log($"Abrindo navegador para buscar: {deviceName}");
                string url;
                if (!string.IsNullOrEmpty(hardwareId))
                {
                    // Busca precisa no Catálogo
                    string queryId = Uri.EscapeDataString(hardwareId);
                    url = $"https://www.catalog.update.microsoft.com/Search.aspx?q={queryId}";
                }
                else
                {
                    // Fallback
                    string query = $"{deviceName} driver official";
                    url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
                }
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            catch (Exception ex)
            {
                Logger.LogError("WebSearch", ex.Message);
            }
        }

        public static (bool Success, string Message) BackupDrivers(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath)) return (false, "Caminho inválido.");
            try
            {
                Logger.Log($"Iniciando backup de drivers para: {destinationPath}");
                if (!Directory.Exists(destinationPath)) Directory.CreateDirectory(destinationPath);

                string command = $"/c dism.exe /online /export-driver /destination:\"{destinationPath}\"";
                // Roda visível para o usuário acompanhar o DISM
                SystemUtils.RunExternalProcess("cmd.exe", $"{command} & timeout 5", hidden: false, waitForExit: false);

                return (true, "Backup iniciado (janela externa).");
            }
            catch (Exception ex)
            {
                Logger.LogError("BackupDrivers", ex.Message);
                return (false, ex.Message);
            }
        }

        public static void OpenWindowsUpdateSettings()
        {
            try
            {
                Logger.Log("Abrindo configurações do Windows Update...");
                Process.Start(new ProcessStartInfo("ms-settings:windowsupdate-action") { UseShellExecute = true });
            }
            catch { }
        }

        public static void ExportDriverListToTxt(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== RELATÓRIO DE DRIVERS (KIT LUGIA) ===");
                sb.AppendLine($"Data: {DateTime.Now}");
                sb.AppendLine("========================================");
                var list = _cachedDrivers.Any() ? _cachedDrivers : GetSystemDriversAsync(true).GetAwaiter().GetResult();
                foreach (var d in list)
                {
                    sb.AppendLine($"Dispositivo: {d.DeviceName}");
                    sb.AppendLine($"Fabricante:  {d.Provider}");
                    sb.AppendLine($"Versão:      {d.Version}");
                    sb.AppendLine($"Data:        {d.Date}");
                    sb.AppendLine($"INF:         {d.InfName}");
                    sb.AppendLine($"ID:          {d.HardwareId}");
                    sb.AppendLine("----------------------------------------");
                }
                File.WriteAllText(filePath, sb.ToString());
                Logger.Log($"Lista de drivers exportada com sucesso para: {filePath}");
            }
            catch (Exception ex)
            {
                Logger.LogError("ExportTxt", ex.Message);
            }
        }
    }
}