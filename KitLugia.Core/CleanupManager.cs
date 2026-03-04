using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        public static (long TotalBytesFreed, List<string> Log) CleanTemporaryFiles()
        {
            Logger.Log("Iniciando varredura de arquivos temporários...");
            long totalBytes = 0;
            var log = new List<string>();

            var resultUser = CleanDirectory(Path.GetTempPath(), "Temp do Usuário");
            totalBytes += resultUser.BytesFreed;
            log.Add(resultUser.Message);

            var resultWin = CleanDirectory(@"C:\Windows\Temp", "Temp do Windows");
            totalBytes += resultWin.BytesFreed;
            log.Add(resultWin.Message);

            return (totalBytes, log);
        }

        public static (long TotalBytesFreed, List<string> Log) CleanWindowsUpdateCache()
        {
            Logger.Log("Verificando cache do Windows Update...");
            var log = new List<string>();
            string wuCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");

            var result = CleanDirectory(wuCachePath, "Cache do Windows Update");
            log.Add(result.Message);

            return (result.BytesFreed, log);
        }

        public static (long TotalBytesFreed, List<string> Log) CleanShaderCaches()
        {
            Logger.Log("Verificando caches de shader (GPU)...");
            long totalBytes = 0;
            var log = new List<string>();

            var resultNvidia = CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NVIDIA\GLCache"), "Cache de Shader NVIDIA");
            totalBytes += resultNvidia.BytesFreed;
            log.Add(resultNvidia.Message);

            var resultAmdDx = CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"AMD\DxCache"), "Cache de Shader AMD (DX)");
            totalBytes += resultAmdDx.BytesFreed;
            log.Add(resultAmdDx.Message);

            var resultAmdGl = CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"AMD\GLCache"), "Cache de Shader AMD (OpenGL)");
            totalBytes += resultAmdGl.BytesFreed;
            log.Add(resultAmdGl.Message);

            return (totalBytes, log);
        }

        public static (long TotalBytesFreed, List<string> Log) RunFullCleanup()
        {
            Logger.Log("=== INICIANDO LIMPEZA COMPLETA DO SISTEMA ===");
            long totalBytes = 0;
            var fullLog = new List<string>();

            var tempResult = CleanTemporaryFiles();
            totalBytes += tempResult.TotalBytesFreed;
            fullLog.AddRange(tempResult.Log);

            var updateResult = CleanWindowsUpdateCache();
            totalBytes += updateResult.TotalBytesFreed;
            fullLog.AddRange(updateResult.Log);

            var shaderResult = CleanShaderCaches();
            totalBytes += shaderResult.TotalBytesFreed;
            fullLog.AddRange(shaderResult.Log);

            string mbFreed = (totalBytes / 1024.0 / 1024.0).ToString("N2");
            Logger.Log($"[RESUMO] Limpeza finalizada. Total liberado: {mbFreed} MB");

            return (totalBytes, fullLog);
        }

        public static void CompactOS()
        {
            Logger.Log("Iniciando CompactOS (Compressão do Sistema)...");
            Logger.LogProcess("compact.exe", "/CompactOS:always");
            // Abre janela externa pois demora muito
            SystemUtils.RunExternalProcess("cmd.exe", "/c compact.exe /CompactOS:always & pause", hidden: false, waitForExit: false);
        }

        /// <summary>
        /// Helper interno para limpar diretórios.
        /// </summary>
        internal static (long BytesFreed, int FilesDeleted, string Message) CleanDirectory(string path, string name)
        {
            if (!Directory.Exists(path))
            {
                return (0, 0, $"'{name}': Não encontrado. Pulando.");
            }

            long totalSize = 0;
            int fileCount = 0;

            // Função local recursiva para lidar com UnauthorizedAccessException pasta a pasta
            void Traverse(DirectoryInfo dir)
            {
                try
                {
                    foreach (var file in dir.GetFiles())
                    {
                        try
                        {
                            totalSize += file.Length;
                            file.Delete();
                            fileCount++;
                        }
                        catch { /* Ignora arquivos em uso/bloqueados */ }
                    }

                    foreach (var subDir in dir.GetDirectories())
                    {
                        Traverse(subDir);
                        try { subDir.Delete(); } catch { /* Ignora pastas que ainda têm coisas bloqueadas */ }
                    }
                }
                catch { /* Ignora pastas que negam acesso de leitura (ex: subpastas no Windows Temp) */ }
            }

            try
            {
                Traverse(new DirectoryInfo(path));
            }
            catch (Exception ex)
            {
                Logger.LogError("Limpeza", $"Acesso negado raiz em '{name}': {ex.Message}");
            }

            string msg;
            if (fileCount > 0)
            {
                string sizeMb = (totalSize / (1024.0 * 1024.0)).ToString("N2");
                msg = $"'{name}': {fileCount} arquivos removidos ({sizeMb} MB liberados).";
                // Envia para o console preto
                Logger.Log($"[LIMPEZA] {msg}");
            }
            else
            {
                msg = $"'{name}': Nenhuma limpeza necessária.";
                Logger.Log($"[LIMPEZA] {name}: Nada a limpar.");
            }

            return (totalSize, fileCount, msg);
        }
    }
}