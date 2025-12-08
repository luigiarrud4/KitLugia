using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        /// <summary>
        /// Instala o Editor de Política de Grupo (gpedit.msc) em edições do Windows que não o incluem (como Home).
        /// </summary>
        /// <returns>Uma tupla com o resultado da operação.</returns>
        public static (bool Success, string Message) EnableGroupPolicyEditor()
        {
            // Este script em lote usa o DISM para instalar os pacotes do GPEDIT que já existem no sistema, mas não estão ativos.
            string batchContent = @"
@echo off
pushd ""%~dp0""
dir /b %SystemRoot%\servicing\Packages\Microsoft-Windows-GroupPolicy-ClientExtensions-Package~3*.mum >List.txt
dir /b %SystemRoot%\servicing\Packages\Microsoft-Windows-GroupPolicy-ClientTools-Package~3*.mum >>List.txt
for /f %%i in ('findstr /i . List.txt 2^>nul') do dism /online /norestart /add-package:""%SystemRoot%\servicing\Packages\%%i""
del List.txt
";
            string tempFile = Path.Combine(Path.GetTempPath(), "gpedit_enabler.bat");
            try
            {
                File.WriteAllText(tempFile, batchContent);
                // Executa em uma nova janela para que o usuário possa acompanhar o progresso do DISM.
                SystemUtils.RunExternalProcess(tempFile, "", hidden: false, waitForExit: false);
                return (true, "O processo de ativação do GPEDIT foi iniciado em uma nova janela. Aguarde a conclusão.");
            }
            catch (Exception ex)
            {
                return (false, $"ERRO ao criar ou executar o script: {ex.Message}");
            }
            finally
            {
                // Garante que o arquivo temporário seja deletado, se possível.
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { /* Ignora erro de arquivo em uso */ }
            }
        }

        /// <summary>
        /// Cria ou remove o atalho "God Mode" na Área de Trabalho.
        /// O God Mode é uma pasta que agrupa centenas de configurações do Windows em um único lugar.
        /// </summary>
        /// <returns>Uma tupla com o resultado da operação.</returns>
        public static (bool Success, string Message) ToggleGodMode()
        {
            // O GUID {ED7BA470-8E54-465E-825C-99712043E01C} é um identificador especial do Windows
            // que transforma uma pasta simples em um painel de controle mestre.
            string godModePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}");

            try
            {
                if (Directory.Exists(godModePath))
                {
                    Directory.Delete(godModePath, true);
                    return (true, "Atalho 'God Mode' removido da Área de Trabalho.");
                }
                else
                {
                    Directory.CreateDirectory(godModePath);
                    // Aplica o atributo de sistema para garantir que o Windows o reconheça corretamente.
                    File.SetAttributes(godModePath, FileAttributes.Directory | FileAttributes.System);
                    return (true, "Atalho 'God Mode' criado na Área de Trabalho.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"ERRO ao gerenciar o atalho: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpa a fila de impressão do Windows, útil quando documentos ficam "presos".
        /// </summary>
        /// <returns>Uma tupla com o resultado da operação.</returns>
        public static (bool Success, string Message) ClearPrintSpooler()
        {
            try
            {
                // Reutiliza o método 'ManageService' do DiagnosticsManager para parar o serviço de impressão.
                var stopResult = ManageService("Spooler", "stop");
                if (!stopResult.Success)
                {
                    return (false, "Falha ao parar o serviço de Spooler de Impressão. Tente novamente como Administrador.");
                }

                // Reutiliza o método 'CleanDirectory' do CleanupManager para limpar a pasta da fila.
                string spoolPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "PRINTERS");
                var cleanupResult = CleanDirectory(spoolPath, "Fila de Impressão");

                // Reutiliza o método 'ManageService' para reiniciar o serviço.
                var startResult = ManageService("Spooler", "start");
                if (!startResult.Success)
                {
                    return (false, "A fila de impressão foi limpa, mas o serviço de Spooler não pôde ser reiniciado.");
                }

                return (true, $"Fila de impressão limpa com sucesso. {cleanupResult.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Erro inesperado ao limpar a fila de impressão: {ex.Message}");
            }
        }
    }
}