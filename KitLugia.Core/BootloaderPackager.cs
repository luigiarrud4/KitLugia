using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class BootloaderPackager
    {
        private const string CloverDownloadsPath = @"C:\Users\Lugia\Downloads\CLOVER";

        /// <summary>
        /// Copia os arquivos necessários do Clover para a partição de boot.
        /// </summary>
        public static (bool Success, string Message) DeployCloverToPartition(string targetDrive)
        {
            try
            {
                if (!Directory.Exists(CloverDownloadsPath))
                    return (false, "Pasta do Clover não encontrada nos Downloads.");

                string efiFile = Path.Combine(CloverDownloadsPath, "CLOVERX64.efi");
                if (!File.Exists(efiFile))
                    return (false, "Arquivo CLOVERX64.efi não encontrado.");

                Logger.Log($"[BOOT] Implantando Clover EFI em {targetDrive}...");

                // 1. Criar estrutura de pastas EFI
                string efiPath = Path.Combine(targetDrive, "EFI", "BOOT");
                Directory.CreateDirectory(efiPath);

                // 2. Copiar o bootloader (renomeando para o padrão UEFI)
                File.Copy(efiFile, Path.Combine(efiPath, "BOOTX64.EFI"), true);
                
                Logger.Log("[BOOT] Clover implantado com sucesso!");
                return (true, "Bootloader Clover configurado.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
    }
}
