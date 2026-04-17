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
        /// Corrige HD em 100% de uso limpando arquivos de sistema, logs e caches
        /// </summary>
        public static (long TotalBytesFreed, List<string> Log) FixDiskFullUsage()
        {
            Logger.Log("=== INICIANDO CORREÇÃO DE HD EM 100% DE USO ===");
            long totalBytes = 0;
            var fullLog = new List<string>();

            // 1. Limpar arquivos temporários
            var tempResult = CleanTemporaryFiles();
            totalBytes += tempResult.TotalBytesFreed;
            fullLog.AddRange(tempResult.Log);

            // 2. Limpar cache do Windows Update
            var updateResult = CleanWindowsUpdateCache();
            totalBytes += updateResult.TotalBytesFreed;
            fullLog.AddRange(updateResult.Log);

            // 3. Limpar caches de shader
            var shaderResult = CleanShaderCaches();
            totalBytes += shaderResult.TotalBytesFreed;
            fullLog.AddRange(shaderResult.Log);

            // 4. Limpar logs do Windows (CBS, DISM, etc.)
            var logResult = CleanWindowsLogs();
            totalBytes += logResult.BytesFreed;
            fullLog.Add(logResult.Message);

            // 5. Limpar cache de thumbnails
            var thumbResult = CleanThumbnailCache();
            totalBytes += thumbResult.BytesFreed;
            fullLog.Add(thumbResult.Message);

            // 6. Limpar cache de DNS
            string dnsResult = CleanDnsCache();
            fullLog.Add(dnsResult);

            // 7. Limpar Prefetch
            var prefetchResult = CleanPrefetch();
            totalBytes += prefetchResult.BytesFreed;
            fullLog.Add(prefetchResult.Message);

            // 8. Limpar Recycle Bin
            var recycleResult = CleanRecycleBin();
            totalBytes += recycleResult.BytesFreed;
            fullLog.Add(recycleResult.Message);

            string gbFreed = (totalBytes / 1024.0 / 1024.0 / 1024.0).ToString("N2");
            Logger.Log($"[RESUMO] Correção de HD finalizada. Total liberado: {gbFreed} GB");

            return (totalBytes, fullLog);
        }

        private static (long BytesFreed, string Message) CleanWindowsLogs()
        {
            long totalBytes = 0;
            var logPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs", "CBS"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs", "DISM"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Panther")
            };

            foreach (var path in logPaths)
            {
                var result = CleanDirectory(path, $"Logs Windows ({Path.GetFileName(path)})");
                totalBytes += result.BytesFreed;
            }

            string sizeMb = (totalBytes / (1024.0 * 1024.0)).ToString("N2");
            return (totalBytes, $"Logs do Windows: {sizeMb} MB liberados");
        }

        private static (long BytesFreed, string Message) CleanThumbnailCache()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
            var result = CleanDirectory(path, "Cache de Thumbnails");
            string sizeMb = (result.BytesFreed / (1024.0 * 1024.0)).ToString("N2");
            return (result.BytesFreed, $"Cache de Thumbnails: {sizeMb} MB liberados");
        }

        private static string CleanDnsCache()
        {
            try
            {
                SystemUtils.RunExternalProcess("ipconfig.exe", "/flushdns", true);
                return "Cache DNS limpo com sucesso";
            }
            catch (Exception ex)
            {
                return $"Erro ao limpar DNS: {ex.Message}";
            }
        }

        private static (long BytesFreed, string Message) CleanPrefetch()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch");
            var result = CleanDirectory(path, "Prefetch");
            string sizeMb = (result.BytesFreed / (1024.0 * 1024.0)).ToString("N2");
            return (result.BytesFreed, $"Prefetch: {sizeMb} MB liberados");
        }

        private static (long BytesFreed, string Message) CleanRecycleBin()
        {
            try
            {
                long totalBytes = 0;
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType == DriveType.Fixed)
                    {
                        var recyclePath = Path.Combine(drive.Name, "$Recycle.Bin");
                        if (Directory.Exists(recyclePath))
                        {
                            var result = CleanDirectory(recyclePath, $"Lixeira ({drive.Name})");
                            totalBytes += result.BytesFreed;
                        }
                    }
                }
                string sizeMb = (totalBytes / (1024.0 * 1024.0)).ToString("N2");
                return (totalBytes, $"Lixeira: {sizeMb} MB liberados");
            }
            catch (Exception ex)
            {
                return (0, $"Erro ao limpar lixeira: {ex.Message}");
            }
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