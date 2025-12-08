using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        // ... (outros métodos do CleanupManager permanecem iguais) ...

        public static (long TotalBytesFreed, List<string> Log) CleanTemporaryFiles()
        {
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
            var log = new List<string>();
            string wuCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");

            var result = CleanDirectory(wuCachePath, "Cache do Windows Update");
            log.Add(result.Message);

            return (result.BytesFreed, log);
        }

        public static (long TotalBytesFreed, List<string> Log) CleanShaderCaches()
        {
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

            return (totalBytes, fullLog);
        }

        public static void CompactOS()
        {
            SystemUtils.RunExternalProcess("cmd.exe", "/c compact.exe /CompactOS:always & pause", hidden: false, waitForExit: false);
        }


        /// <summary>
        /// Helper interno para limpar todos os arquivos e subpastas de um diretório.
        /// CORREÇÃO: Alterado de 'private' para 'internal' para ser acessível por outros managers.
        /// </summary>
        internal static (long BytesFreed, int FilesDeleted, string Message) CleanDirectory(string path, string name)
        {
            if (!Directory.Exists(path))
            {
                return (0, 0, $"'{name}': Não encontrado. Pulando.");
            }

            long totalSize = 0;
            int fileCount = 0;
            var dirInfo = new DirectoryInfo(path);

            try
            {
                var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        totalSize += file.Length;
                        file.Delete();
                        fileCount++;
                    }
                    catch { /* Ignora arquivos em uso. */ }
                }

                var dirs = dirInfo.GetDirectories();
                foreach (var dir in dirs)
                {
                    try { dir.Delete(true); }
                    catch { /* Ignora pastas em uso. */ }
                }
            }
            catch (UnauthorizedAccessException)
            {
                return (0, 0, $"'{name}': Acesso negado. Tente executar como Administrador.");
            }

            if (fileCount > 0)
                return (totalSize, fileCount, $"'{name}': {fileCount} arquivos removidos ({totalSize / (1024.0 * 1024.0):N2} MB liberados).");
            else
                return (0, 0, $"'{name}': Nenhuma limpeza necessária.");
        }
    }
}