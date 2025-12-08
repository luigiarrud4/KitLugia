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
            // 📂 1. EXPLORER E VISUAL (Win 10/11)
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Reiniciar Explorer.exe",
                Category = "Explorer/UI",
                Icon = "💻",
                Description = "Recarrega a área de trabalho e barra de tarefas travadas.",
                Execute = () => { SystemUtils.RunExternalProcess("taskkill", "/f /im explorer.exe", true); SystemUtils.RunExternalProcess("cmd.exe", "/c start explorer.exe", true, false); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Menu de Contexto Clássico (Win11)",
                Category = "Explorer/UI",
                Icon = "🖱️",
                Description = "Restaura o menu de botão direito antigo (Win10) no Windows 11.",
                Execute = () => { SystemUtils.RunExternalProcess("reg", "add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae252}\\InprocServer32\" /f /ve", true); Toolbox.ManageService("explorer", "stop"); Process.Start("explorer.exe"); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reconstruir Cache de Ícones",
                Category = "Explorer/UI",
                Icon = "🖼️",
                IsSlow = true,
                Description = "Corrige ícones brancos ou errados. Fecha o Explorer temporariamente.",
                Execute = () => {
                    SystemUtils.RunExternalProcess("taskkill", "/f /im explorer.exe", true);
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Windows\\Explorer");
                    SystemUtils.RunExternalProcess("cmd", $"/c del /f /q \"{path}\\iconcache*\"", true);
                    Process.Start("explorer.exe");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reconstruir Cache de Miniaturas",
                Category = "Explorer/UI",
                Icon = "🧱",
                Description = "Corrige thumbnails de fotos que não aparecem nas pastas.",
                Execute = () => {
                    SystemUtils.RunExternalProcess("taskkill", "/f /im explorer.exe", true);
                    SystemUtils.RunExternalProcess("cmd", "/c del /f /s /q \"%LocalAppData%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\"", true);
                    Process.Start("explorer.exe");
                }
            });

            repairs.Add(new RepairAction
            {
                Name = "Remover Sufixo '- Atalho'",
                Category = "Explorer/UI",
                Icon = "✂️",
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
                Icon = "🔽",
                Description = "Limpa ícones antigos ou 'fantasmas' da área de notificação.",
                Execute = () => { SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\CurrentVersion\\TrayNotify\" /v IconStreams /f", true); Process.Start("explorer.exe"); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Habilitar Segundos no Relógio",
                Category = "Explorer/UI",
                Icon = "⏱️",
                Description = "Mostra os segundos no relógio da barra de tarefas.",
                Execute = () => { SystemUtils.RunExternalProcess("reg", "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowSecondsInSystemClock /t REG_DWORD /d 1 /f", true); Process.Start("explorer.exe"); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Visualização de Pastas",
                Category = "Explorer/UI",
                Icon = "📁",
                Description = "Reseta o modo de exibição de todas as pastas para o padrão.",
                Execute = () => { SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\BagMRU\" /f", true); Process.Start("explorer.exe"); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Lixeira Corrompida",
                Category = "Explorer/UI",
                Icon = "🗑️",
                IsDangerous = true,
                Description = "Corrige erro de acesso à Lixeira em todas as unidades de disco.",
                Execute = () => { foreach (var d in DriveInfo.GetDrives()) if (d.DriveType == DriveType.Fixed) SystemUtils.RunExternalProcess("cmd", $"/c rd /s /q \"{d.Name}$Recycle.bin\"", true); }
            });


            // =================================================================
            // 🌐 2. INTERNET E REDE
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Reset Completo Winsock/IP",
                Category = "Internet",
                Icon = "🌐",
                IsDangerous = true,
                Description = "Reseta sockets, TCP/IP e libera conexões. Fix essencial de rede.",
                Execute = () => { SystemUtils.RunExternalProcess("netsh", "winsock reset", true); SystemUtils.RunExternalProcess("netsh", "int ip reset", true); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Flush DNS (Limpar Cache)",
                Category = "Internet",
                Icon = "🚽",
                Description = "Remove cache antigo de resolução de nomes de sites.",
                Execute = () => SystemUtils.RunExternalProcess("ipconfig", "/flushdns", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Proxy do Windows",
                Category = "Internet",
                Icon = "🔌",
                Description = "Limpa configurações de Proxy (WinHTTP) que malwares alteram.",
                Execute = () => SystemUtils.RunExternalProcess("netsh", "winhttp reset proxy", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar Firewall",
                Category = "Internet",
                Icon = "🔥",
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
                Execute = () => { try { File.WriteAllText(Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts"), "127.0.0.1 localhost"); } catch { } }
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
            // ⚙️ 3. SISTEMA, SERVIÇOS E FERRAMENTAS
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Resetar Windows Update",
                Category = "Sistema",
                Icon = "🔄",
                IsDangerous = true,
                IsSlow = true,
                Description = "Para serviços e limpa a pasta SoftwareDistribution (Corrige erro 0x800).",
                Execute = () => Toolbox.ResetWindowsUpdateComponents()
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Windows Installer (MSI)",
                Category = "Sistema",
                Icon = "📦",
                Description = "Corrige erro 'Não foi possível acessar o serviço Windows Installer'.",
                Execute = () => { SystemUtils.RunExternalProcess("msiexec", "/unregister", true); SystemUtils.RunExternalProcess("msiexec", "/regserver", true); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Destravar Spooler (Impressora)",
                Category = "Sistema",
                Icon = "🖨️",
                Description = "Limpa a fila de documentos e reinicia o serviço de impressão.",
                Execute = () => Toolbox.ClearPrintSpooler()
            });

            repairs.Add(new RepairAction
            {
                Name = "Habilitar Gerenciador de Tarefas",
                Category = "Sistema",
                Icon = "📊",
                Description = "Remove bloqueio de administrador/vírus (TaskMgr Disable).",
                Execute = () => SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v DisableTaskMgr /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Habilitar Regedit",
                Category = "Sistema",
                Icon = "🛠️",
                Description = "Remove bloqueio de administrador/vírus (Regedit Disable).",
                Execute = () => SystemUtils.RunExternalProcess("reg", "delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v DisableRegistryTools /f", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Limpar Logs de Eventos",
                Category = "Sistema",
                Icon = "🧹",
                IsSlow = true,
                Description = "Apaga todo o histórico do Visualizador de Eventos do Windows.",
                Execute = () => SystemUtils.RunExternalProcess("powershell", "-Command \"Get-EventLog -LogName * | ForEach { Clear-EventLog $_.Log }\"", true)
            });

            repairs.Add(new RepairAction
            {
                Name = "Corrigir Associação .EXE",
                Category = "Sistema",
                Icon = "🔗",
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
                Icon = "⏰",
                Description = "Ressincroniza o horário do PC com servidores mundiais (time.windows.com).",
                Execute = () => { SystemUtils.RunExternalProcess("net", "stop w32time", true); SystemUtils.RunExternalProcess("net", "start w32time", true); SystemUtils.RunExternalProcess("w32tm", "/resync", true); }
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
                Icon = "🌙",
                Description = "Libera gigabytes de espaço deletando o hiberfil.sys.",
                Execute = () => SystemUtils.RunExternalProcess("powercfg", "-h off", true)
            });

            // =================================================================
            // 🤖 4. WINDOWS STORE E APPS
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Resetar Microsoft Store",
                Category = "Apps/Loja",
                Icon = "🛍️",
                IsSlow = true,
                Description = "Executa WSReset.exe para limpar cache da loja e destravar downloads.",
                Execute = () => Toolbox.ResetStoreCache()
            });

            repairs.Add(new RepairAction
            {
                Name = "Reinstalar Todos Apps Padrão",
                Category = "Apps/Loja",
                Icon = "♻️",
                IsSlow = true,
                Description = "Usa PowerShell para reinstalar Calculadora, Fotos, Email e outros.",
                Execute = () => Toolbox.ReinstallDefaultApps()
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Gaming Services",
                Category = "Apps/Loja",
                Icon = "🎮",
                Description = "Correção essencial para erros ao baixar jogos do Xbox GamePass.",
                Execute = () => Toolbox.RepairGamingServices()
            });

            repairs.Add(new RepairAction
            {
                Name = "Reparar Pesquisa (Search)",
                Category = "Apps/Loja",
                Icon = "🔍",
                Description = "Reinicia serviços e reconstrói o índice do Windows Search.",
                Execute = () => { SystemUtils.RunExternalProcess("net", "stop wsearch", true); SystemUtils.RunExternalProcess("net", "start wsearch", true); }
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
            // 🚑 5. DIAGNÓSTICO (MSDT NATIVO) - AGORA COM DESCRIÇÕES!
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "Solução de Áudio",
                Category = "Soluções Win",
                Icon = "🔊",
                IsSlow = true,
                Description = "Diagnostica problemas de reprodução de som e drivers.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id AudioPlaybackDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Rede/Wifi",
                Category = "Soluções Win",
                Icon = "📶",
                IsSlow = true,
                Description = "Verifica adaptadores de rede, DNS e conectividade com a Internet.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id NetworkDiagnosticsNetworkAdapter", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Impressora",
                Category = "Soluções Win",
                Icon = "🖨️",
                IsSlow = true,
                Description = "Tenta detectar por que a impressora não está imprimindo.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id PrinterDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Bluetooth",
                Category = "Soluções Win",
                Icon = "🦷",
                IsSlow = true,
                Description = "Corrige problemas de conexão com dispositivos sem fio.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id BluetoothDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução Windows Update",
                Category = "Soluções Win",
                Icon = "🔄",
                IsSlow = true,
                Description = "Verifica se há atualizações pendentes travando o sistema.",
                Execute = () => SystemUtils.RunExternalProcess("msdt", "/id WindowsUpdateDiagnostic", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "Solução de Teclado",
                Category = "Soluções Win",
                Icon = "⌨️",
                IsSlow = true,
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
            // ☣️ 6. MANUTENÇÃO AVANÇADA / EXPERT
            // =================================================================

            repairs.Add(new RepairAction
            {
                Name = "SFC Scannow (Arquivos)",
                Category = "Avançado",
                Icon = "⚕️",
                IsSlow = true,
                Description = "Verifica a integridade de todos os arquivos protegidos do sistema.",
                Execute = () => SystemUtils.RunExternalProcess("cmd.exe", "/c sfc /scannow & pause", false, false)
            });

            repairs.Add(new RepairAction
            {
                Name = "DISM RestoreHealth",
                Category = "Avançado",
                Icon = "🚑",
                IsSlow = true,
                Description = "Usa o Windows Update para corrigir a imagem corrompida do sistema.",
                Execute = () => SystemUtils.RunExternalProcess("cmd.exe", "/c DISM /Online /Cleanup-Image /RestoreHealth & pause", false, false)
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
                Execute = () => { SystemUtils.RunExternalProcess("net", "stop winmgmt /y", true); SystemUtils.RunExternalProcess("winmgmt", "/resetrepository", true); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Resetar GPO Local",
                Category = "Avançado",
                Icon = "📜",
                IsDangerous = true,
                Description = "Restaura todas as Políticas de Grupo (Gpedit) para o padrão.",
                Execute = () => { SystemUtils.RunExternalProcess("cmd", "/c RD /S /Q \"%WinDir%\\System32\\GroupPolicyUsers\" & RD /S /Q \"%WinDir%\\System32\\GroupPolicy\" & gpupdate /force", true); }
            });

            repairs.Add(new RepairAction
            {
                Name = "Agendar CHKDSK C:",
                Category = "Avançado",
                Icon = "💾",
                IsSlow = true,
                Description = "Verifica erros no disco rígido na próxima reinicialização.",
                Execute = () => SystemUtils.RunExternalProcess("cmd.exe", "/c echo S | chkdsk c: /f /r & pause", false, false)
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
                Execute = () => SystemUtils.RunExternalProcess("compact.exe", "/CompactOS:always", false, true)
            });

            return repairs;
        }
    }
}