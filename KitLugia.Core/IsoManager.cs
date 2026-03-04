using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace KitLugia.Core
{
    public static class IsoManager
    {
        public static async Task<(bool Success, string Message, string DriveLetter)> MountIso(string isoPath)
        {
            if (!File.Exists(isoPath)) return (false, "Arquivo ISO não encontrado.", "");

            return await Task.Run(() =>
            {
                try
                {
                    // Comando PowerShell corrigido e robusto para obter a letra da unidade
                    string psCommand = $"$m = Mount-DiskImage -ImagePath '{isoPath}' -PassThru; ($m | Get-Volume).DriveLetter";
                    
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    string output = process.StandardOutput?.ReadToEnd()?.Trim() ?? "";
                    string error = process.StandardError?.ReadToEnd() ?? "";
                    process.WaitForExit();

                    // Validação robusta da saída com regex
                    if (!string.IsNullOrEmpty(output) && Regex.IsMatch(output, "^[A-Za-z]$"))
                    {
                        return (true, "ISO montada com sucesso.", output + ":\\");
                    }
                    else
                    {
                        Logger.Log($"Falha ao montar ISO: {error}");
                        return (false, $"Falha ao montar. Erro: {error}", "");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Erro ao montar ISO: {ex.Message}");
                    return (false, $"Erro crítico: {ex.Message}", "");
                }
            });
        }

        public static async Task<(bool Success, string Message)> DismountIso(string isoPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string psCommand = $"Dismount-DiskImage -ImagePath '{isoPath}'";
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = Process.Start(psi);
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return (true, "ISO desmontada com sucesso.");
                    else
                        return (false, "Erro ao desmontar ISO. Verifique se o caminho está correto.");
                }
                catch (Exception ex) { return (false, $"Exceção ao desmontar ISO: {ex.Message}"); }
            });
        }

        // Métodos para compatibilidade com código existente
        public static async Task<(bool Success, string Message)> MountWim(string wimPath, string mountDir)
        {
            return await Task.Run(() => 
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = $"/Mount-Image /ImageFile:\"{wimPath}\" /Index:1 /MountDir:\"{mountDir}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return (true, "Imagem montada com sucesso.");
                    else
                        return (false, $"Erro ao montar imagem: {process.StandardError.ReadToEnd()}");
                }
                catch (Exception ex)
                {
                    return (false, $"Exceção ao montar imagem: {ex.Message}");
                }
            });
        }

        public static async Task<(bool Success, string Message)> InjectDrivers(string mountDir, string driversPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = $"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{driversPath}\" /Recurse /ForceUnsigned",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return (true, "Drivers injetados com sucesso.");
                    else
                        return (false, $"Erro ao injetar drivers: {process.StandardError.ReadToEnd()}");
                }
                catch (Exception ex)
                {
                    return (false, $"Exceção ao injetar drivers: {ex.Message}");
                }
            });
        }

        public static async Task<(bool Success, string Message)> UnmountAndCommit(string mountDir)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = $"/Unmount-Image /MountDir:\"{mountDir}\" /Commit",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return (true, "Imagem salva e desmontada com sucesso.");
                    else
                        return (false, $"Erro ao desmontar imagem: {process.StandardError.ReadToEnd()}");
                }
                catch (Exception ex)
                {
                    return (false, $"Exceção ao desmontar imagem: {ex.Message}");
                }
            });
        }

        public static async Task<(bool Success, string Message)> CreateIso(string sourceDir, string targetIso)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string oscdimgPath = FindOscdimg();
                    if (string.IsNullOrEmpty(oscdimgPath))
                    {
                        return CreateIsoWithPowerShell(sourceDir, targetIso).Result;
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = oscdimgPath,
                        Arguments = $"-m -o -u2 -udfver102 -bootdata:2#p0,e,b\"{sourceDir}\\boot\\etfsboot.com\"#pEF,e,b\"{sourceDir}\\efi\\microsoft\\boot\\efisys.bin\" \"{sourceDir}\" \"{targetIso}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return (true, "ISO criada com sucesso.");
                    else
                        return (false, $"Erro ao criar ISO: {process.StandardError.ReadToEnd()}");
                }
                catch (Exception ex)
                {
                    return (false, $"Exceção ao criar ISO: {ex.Message}");
                }
            });
        }

        private static async Task<(bool Success, string Message)> CreateIsoWithPowerShell(string sourceDir, string targetIso)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"New-ISOFile -Source '{sourceDir}' -Destination '{targetIso}' -Bootable\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                        return (true, "ISO criada com PowerShell.");
                    else
                        return (false, $"Erro ao criar ISO com PowerShell: {process.StandardError.ReadToEnd()}");
                }
                catch (Exception ex)
                {
                    return (false, $"Exceção ao criar ISO com PowerShell: {ex.Message}");
                }
            });
        }

        private static string? FindOscdimg()
        {
            string[] adkPaths = {
                @"C:\Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe",
                @"C:\Program Files\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\oscdimg.exe"
            };

            foreach (string path in adkPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }
    }
}
