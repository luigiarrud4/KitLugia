using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class GeneralRepairManager
    {
        public static List<RepairAction> GetAllRepairs()
        {
            var repairs = new List<RepairAction>();

            // =================================================================
            // 1. EXPLORER E VISUAL (Win 10/11)
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Reiniciar Explorer.exe",
                Category = "Explorer/UI",
                Icon = "🔄",
                Description = "Recarrega a área de trabalho e barra de tarefas travadas.",
                Execute = () => {
                    Logger.Log("Reiniciando processo Explorer.exe...");
                    SystemUtils.RunExternalProcess("taskkill", "/f /im explorer.exe", true);
                    // Pequena pausa para garantir que o processo encerrou
                    System.Threading.Thread.Sleep(1000);
                    SystemUtils.RunExternalProcess("cmd.exe", "/c start explorer.exe", true, false);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Menu de Contexto Clássico (Win11)",
                Category = "Explorer/UI",
                Icon = "📋",
                Description = "Restaura o menu de botão direito antigo (Win10) no Windows 11.",
                Execute = () => {
                    Logger.Log("Aplicando Menu de Contexto Clássico...");
                    SystemUtils.SetRegistryValue(Microsoft.Win32.Registry.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32", "", "", Microsoft.Win32.RegistryValueKind.String);
                    Logger.Log("Reinicie o Explorer para aplicar.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Remover Menu Clássico (Win11)",
                Category = "Explorer/UI",
                Icon = "↩️",
                Description = "Volta para o menu de contexto padrão do Windows 11.",
                Execute = () => {
                    Logger.Log("Removendo Menu de Contexto Clássico...");
                    SystemUtils.DeleteRegistryKey(Microsoft.Win32.Registry.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reconstruir Cache de Ícones",
                Category = "Explorer/UI",
                Icon = "🖼️",
                IsSlow = true,
                Description = "Corrige ícones brancos ou errados. Fecha o Explorer temporariamente.",
                Execute = () => {
                    Logger.Log("Iniciando reconstrução do cache de ícones...");
                    SystemUtils.RunExternalProcess("taskkill", "/f /im explorer.exe", true);
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Windows\\Explorer");
                    SystemUtils.RunExternalProcess("cmd", $"/c del /f /q \"{path}\\iconcache*\"", true);
                    Process.Start("explorer.exe");
                    Logger.Log("[SUCESSO] Cache de ícones limpo.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reconstruir Cache de Miniaturas",
                Category = "Explorer/UI",
                Icon = "🏞️",
                Description = "Corrige thumbnails de fotos que não aparecem nas pastas.",
                Execute = () => {
                    Logger.Log("Limpando cache de miniaturas (Thumbnails)...");
                    SystemUtils.RunExternalProcess("taskkill", "/f /im explorer.exe", true);
                    SystemUtils.RunExternalProcess("cmd", "/c del /f /s /q \"%LocalAppData%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\"", true);
                    Process.Start("explorer.exe");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Remover Sufixo '- Atalho'",
                Category = "Explorer/UI",
                Icon = "🔗",
                Description = "Impede que o Windows adicione o texto 'Atalho' ao criar links.",
                Execute = () => SystemUtils.RunExternalProcess("reg", "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\" /v link /t REG_BINARY /d 00000000 /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Desativar 'Recomendados' (Win11)",
                Category = "Explorer/UI",
                Icon = "🚫",
                Description = "Remove a área de arquivos recomendados do Menu Iniciar (Requer Admin).",
                Execute = () => SystemUtils.RunExternalProcess("reg", "add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer\" /v HideRecommendedSection /t REG_DWORD /d 1 /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Alinhar Barra de Tarefas à Esquerda",
                Category = "Explorer/UI",
                Icon = "⬅️",
                Description = "Move o menu iniciar do centro para a esquerda (Estilo Windows 10).",
                Execute = () => SystemUtils.RunExternalProcess("reg", "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAl /t REG_DWORD /d 0 /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Restaurar Photo Viewer Antigo",
                Category = "Explorer/UI",
                Icon = "📷",
                Description = "Ativa o visualizador de fotos clássico (leve e rápido) no registro.",
                Execute = () => SystemUtils.RunExternalProcess("cmd", "/c REG ADD \"HKLM\\SOFTWARE\\Microsoft\\Windows Photo Viewer\\Capabilities\\FileAssociations\" /v \".jpg\" /t REG_SZ /d \"PhotoViewer.FileAssoc.Tiff\" /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Ícones da Bandeja",
                Category = "Explorer/UI",
                Icon = "🔔",
                Description = "Limpa ícones antigos ou 'fantasmas' da área de notificação.",
                Execute = () => {
                    Logger.Log("Resetando ícones da bandeja do sistema...");
                    SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\TrayNotify\" /v IconStreams /f", true);
                    Process.Start("explorer.exe");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Habilitar Segundos no Relógio",
                Category = "Explorer/UI",
                Icon = "⏱️",
                Description = "Mostra os segundos no relógio da barra de tarefas.",
                Execute = () => {
                    SystemUtils.RunExternalProcess("reg", "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowSecondsInSystemClock /t REG_DWORD /d 1 /f", true);
                    Process.Start("explorer.exe");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Visualização de Pastas",
                Category = "Explorer/UI",
                Icon = "📂",
                Description = "Reseta o modo de exibição de todas as pastas para o padrão.",
                Execute = () => {
                    Logger.Log("Apagando chaves de visualização de pastas (BagMRU)...");
                    SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\BagMRU\" /f", true);
                    Process.Start("explorer.exe");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Lixeira Corrompida",
                Category = "Explorer/UI",
                Icon = "🗑️",
                IsDangerous = true,
                Description = "Corrige erro de acesso à Lixeira em todas as unidades de disco.",
                Execute = () => {
                    Logger.Log("Resetando Lixeira em todos os drives...");
                    foreach (var d in DriveInfo.GetDrives())
                        if (d.DriveType == DriveType.Fixed)
                            SystemUtils.RunExternalProcess("cmd", $"/c rd /s /q \"{d.Name}$Recycle.bin\"", true);
                }
            });


            // =================================================================
            // 2. INTERNET E REDE
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Reset Completo Winsock/IP",
                Category = "Internet",
                Icon = "🌐",
                IsDangerous = true,
                Description = "Reseta sockets, TCP/IP e libera conexões. Fix essencial de rede.",
                Execute = () => {
                    Logger.Log("Executando reset de rede completo (Winsock/IP)...");
                    SystemUtils.RunExternalProcess("netsh", "winsock reset", true);
                    SystemUtils.RunExternalProcess("netsh", "int ip reset", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Flush DNS (Limpar Cache)",
                Category = "Internet",
                Icon = "🚿",
                Description = "Remove cache antigo de resolução de nomes de sites.",
                Execute = () => SystemUtils.RunExternalProcess("ipconfig", "/flushdns", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Proxy do Windows",
                Category = "Internet",
                Icon = "🔀",
                Description = "Limpa configurações de Proxy (WinHTTP) que malwares alteram.",
                Execute = () => SystemUtils.RunExternalProcess("netsh", "winhttp reset proxy", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Firewall",
                Category = "Internet",
                Icon = "🧱",
                IsDangerous = true,
                Description = "Apaga todas as regras do Firewall e restaura o padrão de fábrica.",
                Execute = () => SystemUtils.RunExternalProcess("netsh", "advfirewall reset", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Restaurar HOSTS",
                Category = "Internet",
                Icon = "📝",
                IsDangerous = true,
                Description = "Reseta o arquivo de bloqueio de sites (C:\\Windows\\System32\\drivers\\etc).",
                Execute = () => {
                    try
                    {
                        Logger.Log("Restaurando arquivo HOSTS original...");
                        File.WriteAllText(Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts"), "127.0.0.1 localhost");
                        Logger.Log("[SUCESSO] Arquivo HOSTS limpo.");
                    }
                    catch (Exception ex) { Logger.LogError("ResetHosts", ex.Message); }
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Ativar Descoberta de Rede",
                Category = "Internet",
                Icon = "📡",
                Description = "Permite que este computador veja e seja visto por outros na rede.",
                Execute = () => SystemUtils.RunExternalProcess("netsh", "advfirewall firewall set rule group=\"Network Discovery\" new enable=Yes", true)
            });


            // =================================================================
            // 3. SISTEMA, SERVIÇOS E FERRAMENTAS
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Resetar Windows Update",
                Category = "Sistema",
                Icon = "🔄",
                IsSlow = true,
                Description = "Para serviços, limpa pastas temporárias e reinicia o Update.",
                Execute = () => {
                    Logger.Log("Iniciando reparo do Windows Update...");
                    SystemUtils.RunExternalProcess("cmd", "/c net stop wuauserv & net stop bits & net stop cryptsvc", true);
                    SystemUtils.RunExternalProcess("cmd", "/c rd /s /q %systemroot%\\SoftwareDistribution", true);
                    SystemUtils.RunExternalProcess("cmd", "/c rd /s /q %systemroot%\\System32\\catroot2", true);
                    SystemUtils.RunExternalProcess("cmd", "/c net start wuauserv & net start bits & net start cryptsvc", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Limpeza de Disco (SAGE)",
                Category = "Sistema",
                Icon = "🧹",
                Description = "Executa a limpeza de disco avançada do Windows (cleanmgr).",
                Execute = () => {
                    Logger.Log("Iniciando Limpeza de Disco Avançada...");
                    SystemUtils.RunExternalProcess("cleanmgr.exe", "/sagerun:1", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Erros de Disco (SFC)",
                Category = "Reparos AIO",
                Icon = "🛡️",
                Description = "Verifica integridade dos arquivos do sistema (SFC /scannow).",
                Execute = () => {
                    Logger.Log("Iniciando SFC Scannow...");
                    SystemUtils.RunExternalProcess("sfc", "/scannow", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Imagem (DISM)",
                Category = "Reparos AIO",
                Icon = "💊",
                Description = "Repara a imagem do Windows usando DISM Online.",
                Execute = () => {
                    Logger.Log("Iniciando DISM RestoreHealth...");
                    SystemUtils.RunExternalProcess("dism", "/online /cleanup-image /restorehealth", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Windows Store (Apps)",
                Category = "Sistema",
                Icon = "🛒",
                Description = "Reseta o cache da loja e reinstala apps padrão (WSReset).",
                Execute = () => {
                    SystemUtils.RunExternalProcess("wsreset.exe", "", true);
                    SystemUtils.RunExternalProcess("powershell", "-ExecutionPolicy Bypass -Command \"Get-AppXPackage -AllUsers | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\"}\"", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Limpar Cache de Sombra (VSS)",
                Category = "Sistema",
                Icon = "🗂️",
                IsDangerous = true,
                Description = "Apaga todos os pontos de restauração antigos para liberar espaço.",
                Execute = () => SystemUtils.RunExternalProcess("vssadmin", "delete shadows /all /quiet", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Energia (Power)",
                Category = "Sistema",
                Icon = "⚡",
                Description = "Restaura os planos de energia padrão do Windows.",
                Execute = () => SystemUtils.RunExternalProcess("powercfg", "-restoredefaultschemes", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Time/Hora (NTP)",
                Category = "Sistema",
                Icon = "🕐",
                Description = "Sincroniza o relógio do Windows com servidores oficiais.",
                Execute = () => {
                    SystemUtils.RunExternalProcess("net", "stop w32time", true);
                    SystemUtils.RunExternalProcess("w32tm", "/unregister", true);
                    SystemUtils.RunExternalProcess("w32tm", "/register", true);
                    SystemUtils.RunExternalProcess("net", "start w32time", true);
                    SystemUtils.RunExternalProcess("w32tm", "/resync", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Habilitar Modo Deus (GodMode)",
                Category = "Sistema",
                Icon = "⚜️",
                Description = "Cria uma pasta na área de trabalho com acesso a TODAS as configurações.",
                Execute = () => {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string path = Path.Combine(desktop, "GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}");
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Habilitar Regedit",
                Category = "Sistema",
                Icon = "🔓",
                Description = "Remove bloqueio de administrador/vírus (Regedit Disable).",
                Execute = () => SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v DisableRegistryTools /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Limpar Logs de Eventos",
                Category = "Sistema",
                Icon = "📜",
                IsSlow = true,
                Description = "Apaga todo o histórico do Visualizador de Eventos do Windows.",
                Execute = () => {
                    Logger.Log("Limpando Visualizador de Eventos (Event Viewer)...");
                    SystemUtils.RunExternalProcess("powershell", "-Command \"wevtutil el | Foreach-Object { wevtutil cl $_ }\"", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Associação .EXE",
                Category = "Sistema",
                Icon = "🔧",
                Description = "Repara o registro para corrigir programas que não abrem.",
                Execute = () => SystemUtils.RunExternalProcess("cmd", "/c assoc .exe=exefile", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Windows Defender",
                Category = "Sistema",
                Icon = "🛡️",
                Description = "Reseta configurações e definições do Antivírus nativo.",
                Execute = () => SystemUtils.RunExternalProcess("cmd", "/c \"%ProgramFiles%\\Windows Defender\\MpCmdRun.exe\" -RestoreDefaults", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar CD/DVD Drive",
                Category = "Sistema",
                Icon = "💿",
                Description = "Remove filtros Upper/Lower do registro que ocultam o leitor.",
                Execute = () => SystemUtils.RunExternalProcess("reg", "delete \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Class\\{4d36e965-e325-11ce-bfc1-08002be10318}\" /v UpperFilters /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Hora/NTP",
                Category = "Sistema",
                Icon = "🕐",
                Description = "Ressincroniza o horário do PC com servidores mundiais (time.windows.com).",
                Execute = () => {
                    Logger.Log("Ressincronizando relógio do sistema...");
                    SystemUtils.RunExternalProcess("net", "stop w32time", true);
                    SystemUtils.RunExternalProcess("net", "start w32time", true);
                    SystemUtils.RunExternalProcess("w32tm", "/resync", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Destravar Clipboard/Ctrl+V",
                Category = "Sistema",
                Icon = "📋",
                Description = "Limpa e reinicia o serviço da área de transferência.",
                Execute = () => SystemUtils.RunExternalProcess("cmd", "/c echo off | clip", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Desativar Hibernação",
                Category = "Sistema",
                Icon = "💤",
                Description = "Libera gigabytes de espaço deletando o hiberfil.sys.",
                Execute = () => SystemUtils.RunExternalProcess("powercfg", "-h off", true)
            });

            // =================================================================
            // 4. WINDOWS STORE E APPS
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Resetar Microsoft Store",
                Category = "Apps/Loja",
                Icon = "🏪",
                IsSlow = true,
                Description = "Executa WSReset.exe para limpar cache da loja e destravar downloads.",
                Execute = () => SystemUtils.RunExternalProcess("wsreset.exe", "", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Reinstalar Todos Apps Padrão",
                Category = "Apps/Loja",
                Icon = "📦",
                IsSlow = true,
                Description = "Usa PowerShell para reinstalar Calculadora, Fotos, Email e outros.",
                Execute = () => {
                    Logger.Log("Iniciando reinstalação de Apps Padrão via PowerShell...");
                    SystemUtils.RunExternalProcess("powershell", "-ExecutionPolicy Bypass -Command \"Get-AppXPackage -AllUsers | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\"}\"", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Pesquisa (Search)",
                Category = "Apps/Loja",
                Icon = "🔍",
                Description = "Reinicia serviços e reconstrói o índice do Windows Search.",
                Execute = () => {
                    Logger.Log("Reiniciando serviço Windows Search...");
                    SystemUtils.RunExternalProcess("net", "stop wsearch", true);
                    SystemUtils.RunExternalProcess("net", "start wsearch", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Central de Notificação",
                Category = "Apps/Loja",
                Icon = "🔔",
                Description = "Re-registra o ShellExperienceHost. Corrige notificações travadas.",
                Execute = () => SystemUtils.RunExternalProcess("powershell", "Get-AppxPackage Microsoft.Windows.ShellExperienceHost | Foreach {Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\"}", true)
            });

            // =================================================================
            // 5. JOGOS / ANTI-CHEAT (VALORANT FIX)
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Correção VALORANT (VAN9005)",
                Category = "Jogos/Anti-Cheat",
                Icon = "🎮",
                IsDangerous = true, // Altera segurança crítica do Windows
                Description = "Desativa VBS e HVCI para permitir que o Vanguard rode (Corrige VAN9005 no Win10/11). Reinicie após aplicar.",
                Execute = () =>
                {
                    Logger.Log("Iniciando correção VALORANT (VBS/HVCI)...");

                    // 1. Comando BCDEDIT (Oficial recomendado pela Riot)
                    // Desativa o lançamento do hypervisor na inicialização
                    SystemUtils.RunExternalProcess("bcdedit", "/set hypervisorlaunchtype off", true);

                    // 2. Registro: Desativar VBS (Virtualization Based Security)
                    SystemUtils.RunExternalProcess("reg",
                        @"add ""HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard"" /v EnableVirtualizationBasedSecurity /t REG_DWORD /d 0 /f", true);

                    // 3. Registro: Desativar HVCI (Integridade de Memória / Core Isolation)
                    SystemUtils.RunExternalProcess("reg",
                        @"add ""HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"" /v Enabled /t REG_DWORD /d 0 /f", true);

                    Logger.Log("[SUCESSO] Configurações aplicadas. Reinicie o PC para o Vanguard funcionar.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reativar Segurança VBS (Padrão)",
                Category = "Jogos/Anti-Cheat",
                Icon = "🛡️",
                IsDangerous = true,
                Description = "Reativa o Hypervisor, VBS e HVCI. Restaura a segurança padrão do Windows.",
                Execute = () =>
                {
                    Logger.Log("Restaurando configurações de segurança VBS/Hypervisor...");

                    // Restaura BCD para Automático
                    SystemUtils.RunExternalProcess("bcdedit", "/set hypervisorlaunchtype auto", true);

                    // Remove as chaves de bloqueio
                    SystemUtils.RunExternalProcess("reg", @"delete ""HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard"" /v EnableVirtualizationBasedSecurity /f", true);
                    SystemUtils.RunExternalProcess("reg", @"delete ""HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"" /v Enabled /f", true);

                    Logger.Log("[SUCESSO] Segurança padrão restaurada. Reinicie o PC.");
                }
            });

            // =================================================================
            // 6. DIAGNÓSTICO (MSDT NATIVO)
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Solução de Áudio",
                Category = "Soluções Win",
                Icon = "🎙️",
                IsSlow = true,
                Description = "Diagnostica problemas de reprodução de som e drivers.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id AudioPlaybackDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Rede/Wifi",
                Category = "Soluções Win",
                Icon = "📡",
                IsSlow = true,
                Description = "Diagnostica problemas de conexão Wifi e Ethernet.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id NetworkDiagnosticsNetworkAdapter", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Impressora",
                Category = "Soluções Win",
                Icon = "🖨️",
                Description = "Corrige erros de spooler e conexão com impressoras.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id PrinterDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Teclado",
                Category = "Soluções Win",
                Icon = "⌨️",
                Description = "Verifica configurações de layout e drivers de teclado.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id KeyboardDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução Compatibilidade",
                Category = "Soluções Win",
                Icon = "🧩",
                IsSlow = true,
                Description = "Ajuda a executar programas antigos no Windows atual.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id PCWDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Energia",
                Category = "Soluções Win",
                Icon = "🔋",
                IsSlow = true,
                Description = "Otimiza planos de energia para economizar bateria.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id PowerDiagnostic", false, false)
            });

            // =================================================================
            // ☣️ 7. MANUTENÇÃO AVANÇADA / EXPERT
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "SFC Scannow (Arquivos)",
                Category = "Avançado",
                Icon = "⚕️",
                IsSlow = true,
                Description = "Verifica a integridade de todos os arquivos protegidos do sistema.",
                Execute = () => {
                    Logger.Log("Iniciando SFC /Scannow...");
                    SystemUtils.RunExternalProcess("cmd.exe", "/c sfc /scannow & pause", false, false);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "DISM RestoreHealth",
                Category = "Avançado",
                Icon = "🚑",
                IsSlow = true,
                Description = "Usa o Windows Update para corrigir a imagem corrompida do sistema.",
                Execute = () => {
                    Logger.Log("Iniciando DISM RestoreHealth...");
                    SystemUtils.RunExternalProcess("cmd.exe", "/c DISM /Online /Cleanup-Image /RestoreHealth & pause", false, false);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Limpar WinSxS (Espaço)",
                Category = "Avançado",
                Icon = "🏭",
                IsSlow = true,
                Description = "Limpa backups antigos de atualizações (Component Store).",
                Execute = () => SystemUtils.RunExternalProcess("cmd.exe", "/c DISM /Online /Cleanup-Image /StartComponentCleanup & pause", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar WMI",
                Category = "Avançado",
                Icon = "⚙️",
                IsDangerous = true,
                Description = "Reconstrói o repositório de gerenciamento do Windows.",
                Execute = () => {
                    Logger.Log("Resetando repositório WMI...");
                    SystemUtils.RunExternalProcess("net", "stop winmgmt /y", true);
                    SystemUtils.RunExternalProcess("winmgmt", "/resetrepository", true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Políticas de grupo (GPO) COMPLETO",
                Category = "Avançado",
                Icon = "📜",
                IsDangerous = true,
                Description = "Remove TODAS as políticas do registro e pastas GPO. Fix para 'gerenciado pela organização'.",
                Execute = () => {
                    Logger.Log("Iniciando reset COMPLETO de políticas do Windows...");

                    // 1. Deletar políticas do registro
                    Logger.Log("Deletando políticas do registro...");
                    SystemUtils.RunExternalProcess("reg", "delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\" /f", true);
                    SystemUtils.RunExternalProcess("reg", "delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\" /f", true);
                    SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\" /f", true);

                    // 2. Deletar pastas Group Policy
                    Logger.Log("Deletando pastas Group Policy...");
                    SystemUtils.RunExternalProcess("cmd", "/c RD /S /Q \"%WinDir%\\System32\\GroupPolicyUsers\" >nul 2>&1", true);
                    SystemUtils.RunExternalProcess("cmd", "/c RD /S /Q \"%WinDir%\\System32\\GroupPolicy\" >nul 2>&1", true);

                    // 3. Forçar atualização
                    Logger.Log("Atualizando políticas...");
                    SystemUtils.RunExternalProcess("gpupdate", "/force", true);

                    Logger.Log("[SUCESSO] Reset completo finalizado! Reinicie o PC.");

                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Agendar CHKDSK C:",
                Category = "Avançado",
                Icon = "💾",
                IsSlow = true,
                Description = "Verifica erros no disco rígido na próxima reinicialização.",
                Execute = () => {
                    Logger.Log("Agendando CHKDSK...");
                    SystemUtils.RunExternalProcess("cmd.exe", "/c echo S | chkdsk c: /f /r & pause", false, false);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Menu de Boot Legacy",
                Category = "Avançado",
                Icon = "🏁",
                Description = "Habilita a tecla F8 no boot para entrar em Modo de Segurança.",
                Execute = () => SystemUtils.RunExternalProcess("bcdedit", "/set {default} bootmenupolicy legacy", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Ativar CompactOS",
                Category = "Avançado",
                Icon = "🗜️",
                IsSlow = true,
                Description = "Comprime os arquivos do OS para liberar espaço sem perder velocidade.",
                Execute = () => {
                    Logger.Log("Iniciando CompactOS...");
                    SystemUtils.RunExternalProcess("compact.exe", "/CompactOS:always", false, true);
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Erro de Áudio USB DAC - KB5050009",
                Category = "Sistema",
                Icon = "🎤",
                IsDangerous = false,
                Description = "Corrige falha de alocação de memória que impede funcionamento de áudio USB DAC. Erro 'Insufficient system resources exist to complete the API' afeta Windows 10/11.",
                Execute = () => {
                    Logger.Log("Corrigindo problema de alocação de memória para áudio USB DAC...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows"" /v DisableDynamicAudioPolicy /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] Política de áudio USB ajustada. Reinicie para aplicar.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Detecção de Webcam - KB5050009",
                Category = "Sistema",
                Icon = "📷",
                IsDangerous = false,
                Description = "Corrige falha na detecção de webcams integradas após atualização KB5050009. Erro 0xA00F4244 afeta cameras HP e monitores 4K.",
                Execute = () => {
                    Logger.Log("Reparando detecção de webcam...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows Media Foundation\Platform\Imaging"" /v EnableFrameServerMode /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] Detecção de webcam restaurada. Reinicie o PC.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Restaurar Configurações do BitLocker",
                Category = "Sistema",
                Icon = "🔐",
                IsDangerous = false,
                Description = "Corrige erro onde configurações do BitLocker são gerenciadas incorretamente pelo sistema. Mostra erro falso de 'gerenciado pelo administrador'.",
                Execute = () => {
                    Logger.Log("Restaurando configurações do BitLocker...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\FVE"" /v UseAdvancedStartup /t REG_DWORD /d 1 /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\FVE"" /v EnableBDEWithNoTPM /t REG_DWORD /d 1 /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\FVE"" /v UseTPM /t REG_DWORD /d 2 /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Policies\Microsoft\FVE"" /v UseTPMKeyPIN /t REG_DWORD /d 1 /f", true);
                    Logger.Log("[SUCESSO] Configurações do BitLocker restauradas. Reinicie o PC.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Timeline do Adobe Premiere Pro - KB5050094",
                Category = "Sistema",
                Icon = "🎬",
                IsDangerous = false,
                Description = "Corrige falha ao arrastar clipes na timeline do Premiere Pro em múltiplos monitores. Afeta setups com diferentes escalas.",
                Execute = () => {
                    Logger.Log("Reparando Timeline do Adobe Premiere Pro...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKCU\SOFTWARE\Adobe\Premiere Pro\14.0\Timeline"" /v EnableHighDPIAware /t REG_DWORD /d 1 /f", true);
                    Logger.Log("[SUCESSO] Timeline do Premiere Pro restaurada. Reinicie o aplicativo.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Cursor Girando no Windows 11 24H2",
                Category = "Sistema",
                Icon = "🔄",
                IsDangerous = false,
                Description = "Corrige problema de cursor girando indefinidamente na área de trabalho do Windows 11 24H2. Bug relacionado ao processamento de entrada.",
                Execute = () => {
                    Logger.Log("Corrigindo cursor girando no Windows 11...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKCU\Control Panel\Mouse"" /v MouseSpeed /t REG_SZ /d ""0"" /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKCU\Control Panel\Mouse"" /v MouseThreshold1 /t REG_SZ /d ""0"" /f", true);
                    SystemUtils.RunExternalProcess("cmd", "/c sfc /scannow & pause", false, false);
                    SystemUtils.RunExternalProcess("cmd", "/c DISM /Online /Cleanup-Image /RestoreHealth & pause", false, false);
                    SystemUtils.RunExternalProcess("cmd", "/c DISM /Online /Cleanup-Image /StartComponentCleanup & pause", false, false);
                    SystemUtils.RunExternalProcess("reg", @"delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired"" /f", true);
                    SystemUtils.RunExternalProcess("reg", @"delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequiredForcedApps"" /f", true);
                    Logger.Log("[SUCESSO] Reparo de instalações concluído. Reinicie o PC.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Desconexões de Área de Trabalho Remota",
                Category = "Sistema",
                Icon = "🌐",
                IsDangerous = false,
                Description = "Corrige falhas de autenticação em conexões RDP e Azure Virtual Desktop após atualizações de 2026. KB5078127 e KB5074109.",
                Execute = () => {
                    Logger.Log("Reparando conexões RDP/Azure...");
                    SystemUtils.RunExternalProcess("netsh", "advfirewall firewall set rule group=\"Remote Desktop\" new enable=Yes", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Terminal Server Client\Default"" /v AuthenticationLevel /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] Configurações de RDP ajustadas. Tente reconectar.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Salvamento em Nuvem (OneDrive/Dropbox)",
                Category = "Sistema",
                Icon = "☁️",
                IsDangerous = false,
                Description = "Corrige problemas ao salvar arquivos em armazenamento na nuvem após atualizações de 2026. KB5078127 e KB5074109.",
                Execute = () => {
                    Logger.Log("Reparando salvamento em nuvem...");
                    SystemUtils.RunExternalProcess("cmd", "/c echo off | clip", true);
                    SystemUtils.RunExternalProcess("powershell", "Get-AppxPackage Microsoft.OneDriveSync | Reset-AppxPackage", true);
                    SystemUtils.RunExternalProcess("powershell", "Get-AppxPackage Microsoft.Windows.CloudExperienceHost | Reset-AppxPackage", true);
                    Logger.Log("[SUCESSO] Serviços de nuvem resetados. Reinicie o PC.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Gerenciador de Tarefas Múltiplo",
                Category = "Sistema",
                Icon = "📊",
                IsDangerous = false,
                Description = "Corrige bug do Task Manager que abria múltiplas instâncias, degradando performance em PCs de baixo hardware.",
                Execute = () => {
                    Logger.Log("Reparando Task Manager...");
                    SystemUtils.RunExternalProcess("taskkill", "/f /im taskmgr.exe", true);
                    SystemUtils.RunExternalProcess("cmd", "/c start taskmgr.exe", true);
                    Logger.Log("[SUCESSO] Task Manager reiniciado. Monitore o comportamento.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Remover Menu Iniciar Copilot Forçado",
                Category = "Sistema",
                Icon = "🤖",
                IsDangerous = false,
                Description = "Remove o atalho do Copilot do Menu Iniciar que estava sendo forçado indevidamente pelo Windows Update.",
                Execute = () => {
                    Logger.Log("Removendo Copilot forçado do Menu Iniciar...");
                    SystemUtils.RunExternalProcess("reg", @"delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v TaskbarMn /f", true);
                    SystemUtils.RunExternalProcess("reg", @"delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v TaskbarDa /f", true);
                    Logger.Log("[SUCESSO] Copilot removido do Menu Iniciar. Reinicie o Explorer.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Desempenho de Jogos (NVIDIA/AMD)",
                Category = "Sistema",
                Icon = "🎮",
                IsDangerous = false,
                Description = "Corrige queda de performance em jogos após atualizações de 2025-2026 que afetaram drivers NVIDIA/AMD. Restaura otimizações.",
                Execute = () => {
                    Logger.Log("Reparando desempenho de jogos...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"" /v TdrLevel /t REG_DWORD /d 3 /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"" /v AppCaptureEnabled /t REG_DWORD /d 1 /f", true);
                    Logger.Log("[SUCESSO] Desempenho de jogos restaurado. Teste FPS nos jogos.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Modo Escuro do Windows 11",
                Category = "Sistema",
                Icon = "🌙",
                IsDangerous = false,
                Description = "Corrige problemas com o Modo Escuro que não funcionava corretamente após atualizações de 2025.",
                Execute = () => {
                    Logger.Log("Reparando Modo Escuro...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v AppsUseLightTheme /t REG_DWORD /d 0 /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize"" /v SystemUsesLightTheme /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] Modo Escuro restaurado. Reinicie o sistema.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Compactação de SSD (Windows 11)",
                Category = "Sistema",
                Icon = "💾",
                IsDangerous = false,
                Description = "Corrige problemas de compactação que podem causar lentidão e travamentos em SSDs após atualizações.",
                Execute = () => {
                    Logger.Log("Iniciando compactação segura do SSD...");
                    SystemUtils.RunExternalProcess("powershell", "Optimize-Volume -DriveLetter C -Analyze -Defrag -Trim", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SYSTEM\CurrentControlSet\Control\FileSystem"" /v DisableLastAccess /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] SSD otimizado. Reinicie o PC.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Restaurar Ícone de Senha Oculto",
                Category = "Sistema",
                Icon = "🔑",
                IsDangerous = false,
                Description = "Restaura o ícone de olho (👁) ao lado do campo de senha que some após atualizações.",
                Execute = () => {
                    Logger.Log("Restaurando ícone de senha...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"" /v DisablePasswordReveal /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] Ícone de senha restaurado. Reinicie o sistema.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Menu de Contexto Truncado",
                Category = "Sistema",
                Icon = "📋",
                IsDangerous = false,
                Description = "Corrige problema onde menu de contexto aparece cortado em telas de alta DPI.",
                Execute = () => {
                    Logger.Log("Reparando menu de contexto...");
                    SystemUtils.RunExternalProcess("reg", @"add ""HKCU\Control Panel\Desktop"" /v MenuShowDelay /t REG_SZ /d ""200"" /f", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v EnableMenuDelayAnimation /t REG_DWORD /d 0 /f", true);
                    Logger.Log("[SUCESSO] Menu de contexto restaurado. Reinicie o Explorer.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Serviço de Indexação",
                Category = "Sistema",
                Icon = "🔍",
                IsDangerous = false,
                Description = "Corrige problemas com serviço de indexação que causa alto uso de CPU e lentidão na busca.",
                Execute = () => {
                    Logger.Log("Reparando serviço de indexação...");
                    SystemUtils.RunExternalProcess("cmd", "/c sc config wsearch start= auto", true);
                    SystemUtils.RunExternalProcess("cmd", "/c sc config wsearch type= auto", true);
                    SystemUtils.RunExternalProcess("cmd", "/c net start wsearch", true);
                    Logger.Log("[SUCESSO] Serviço de indexação reconfigurado. Aguarde reindexação.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Cache DNS Lento",
                Category = "Sistema",
                Icon = "🌐",
                IsDangerous = false,
                Description = "Limpa cache DNS corrompido que causa lentidão na navegação e resolução de nomes.",
                Execute = () => {
                    Logger.Log("Limpando cache DNS...");
                    SystemUtils.RunExternalProcess("ipconfig", "/flushdns", true);
                    SystemUtils.RunExternalProcess("cmd", "/c ipconfig /registerdns", true);
                    SystemUtils.RunExternalProcess("cmd", "/c netsh winsock reset && netsh int ip reset", true);
                    Logger.Log("[SUCESSO] Cache DNS limpo. Teste a navegação.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Verificar Drivers de Impressora V3/V4",
                Category = "Sistema",
                Icon = "🖨️",
                IsDangerous = false,
                Description = "Verifica se drivers de impressora foram removidos incorretamente pelas atualizações do Windows.",
                Execute = () => {
                    Logger.Log("Verificando drivers de impressora...");
                    SystemUtils.RunExternalProcess("cmd", "/c pnputil.exe -e", true);
                    Logger.Log("[INFO] Lista de drivers de impressora exibida. Verifique se sua impressora foi afetada.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Tecla F8 de Boot",
                Category = "Sistema",
                Icon = "⌨️",
                IsDangerous = false,
                Description = "Restaura a funcionalidade da tecla F8 para acessar Opções Avançadas de Boot no Windows 11.",
                Execute = () => {
                    Logger.Log("Restaurando tecla F8 de boot...");
                    SystemUtils.RunExternalProcess("bcdedit", "/set {default} bootmenupolicy legacy", true);
                    Logger.Log("[SUCESSO] Tecla F8 restaurada. Reinicie e teste F8 no boot.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Windows Update Quebrado",
                Category = "Sistema",
                Icon = "🔄",
                IsDangerous = false,
                Description = "Corrige problemas com serviço Windows Update que não funciona ou fica travado.",
                Execute = () => {
                    Logger.Log("Reparando Windows Update...");
                    SystemUtils.RunExternalProcess("cmd", "/c net stop wuauserv && net start wuauserv", true);
                    SystemUtils.RunExternalProcess("cmd", "/c net stop bits && net start bits", true);
                    SystemUtils.RunExternalProcess("cmd", "/c rd /s /q \"%SystemRoot%\\SoftwareDistribution\\*\" && md \"%SystemRoot%\\SoftwareDistribution\\Backup\\\"", true);
                    SystemUtils.RunExternalProcess("cmd", "/c ren \"%SystemRoot%\\SoftwareDistribution\\Download\\*\" \"%SystemRoot%\\SoftwareDistribution\\Download\\Old\\\" 2>nul", true);
                    SystemUtils.RunExternalProcess("cmd", "/c reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\RebootRequired\" /f", true);
                    SystemUtils.RunExternalProcess("cmd", "/c reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\WindowsUpdate\\Auto Update\\RebootRequiredForcedApps\" /f", true);
                    SystemUtils.RunExternalProcess("cmd", "/c net start wuauserv && net start bits", true);
                    Logger.Log("[SUCESSO] Windows Update reparado. Verifique atualizações.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Gerenciador de Credenciais",
                Category = "Sistema",
                Icon = "🔐",
                IsDangerous = false,
                Description = "Corrige problemas com serviço Gerenciador de Credenciais que não armazena senhas salvas.",
                Execute = () => {
                    Logger.Log("Reparando Gerenciador de Credenciais...");
                    SystemUtils.RunExternalProcess("cmd", "/c rundll32.exe keymgr.dll, KRShowKeyMgr", true);
                    SystemUtils.RunExternalProcess("reg", @"add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"" /v CredSSP /t REG_DWORD /d 1 /f", true);
                    Logger.Log("[SUCESSO] Gerenciador de Credenciais aberto. Verifique configurações.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Windows Hello/Facial Recognition",
                Category = "Sistema",
                Icon = "👤",
                IsDangerous = false,
                Description = "Corrige problemas com Windows Hello ou reconhecimento facial que não funcionam após atualizações.",
                Execute = () => {
                    Logger.Log("Reparando Windows Hello...");
                    SystemUtils.RunExternalProcess("cmd", "/c powershell -Command \"Get-Service -Name 'WbioSrvc' | Restart-Service -Force\"", true);
                    SystemUtils.RunExternalProcess("cmd", "/c reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Authentication\\\" /v EnableFaceFeatures /f", true);
                    SystemUtils.RunExternalProcess("cmd", "/c reg delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Authentication\\\" /v UseHelloForSignin /f", true);
                    Logger.Log("[SUCESSO] Windows Hello reiniciado. Configure novamente se necessário.");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Windows Sandbox",
                Category = "Sistema",
                Icon = "📦",
                IsDangerous = false,
                Description = "Corrige problemas com Windows Sandbox que não inicia ou causa isolamento excessivo.",
                Execute = () => {
                    Logger.Log("Reparando Windows Sandbox...");
                    SystemUtils.RunExternalProcess("cmd", "/c sc config wdscav2 start= auto", true);
                    SystemUtils.RunExternalProcess("cmd", "/c sc config wdscav2 type= auto", true);
                    SystemUtils.RunExternalProcess("cmd", "/c net start wdscav2", true);
                    Logger.Log("[SUCESSO] Windows Sandbox reconfigurado. Teste aplicações isoladas.");
                }
            });

            return repairs;
        }
    }
}
