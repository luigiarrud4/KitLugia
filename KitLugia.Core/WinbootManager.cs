using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ookii.AnswerFile;

namespace KitLugia.Core
{
    public static class WinbootManager
    {
        public const string WINBOOT_LABEL = "KITLUGIA";
        
        // Caminho de instalação dinâmico - usa Program Files em vez de C:\KitLugia
        public static string KitLugiaInstallPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "KitLugia"
        );

        static WinbootManager()
        {
            // Registrar provedor de encoding para suportar OEM 850 (WinPE)
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        public static bool IsEfiMode()
        {
            try
            {
                // Método simples e confiável via bcdedit ou presença de winload.efi
                return File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "winload.efi"));
            }
            catch { return false; }
        }

        /// <summary>
        /// Gera um arquivo autounattend.xml usando a biblioteca Ookii.AnswerFile
        /// </summary>
        public static void GenerateAutounattendXml(string outputPath, bool bypassRequirements = true, bool localAccount = true, bool disablePrivacy = true, string? userName = "Usuario", string? password = null, bool fullAuto = true, bool disableDefender = false, bool autoLogon = true, bool remoteDesktop = false, string language = "pt-BR", string timeZone = "E. South America Standard Time", string[]? commands = null,
            bool showAllEditions = false, bool disableBitlocker = true, bool disableHibernate = false, bool disableCopilot = true, bool removeEdge = false, bool removeCortana = true, bool removeOneDrive = false, bool disableSpotlight = true, bool disableNews = true, bool disableChat = true,
            bool disableAutoUpdate = false, bool disableDeliveryOpt = true, bool delayUpdates = false, bool longPaths = true, bool disableLocation = true, bool disableActivity = true, bool disableAdID = true, bool disableErrorReporting = true, bool disableInkWorkspace = false,
            bool disableSmartScreen = false, bool disableDefenderSandbox = false, bool disableUAC = false, bool hideEula = true, bool hideOEM = true, bool hideWireless = true, bool hideOnlineAccount = true, bool protectYourPC = true, string computerName = "")
        {
            try
            {
                var options = new AnswerFileOptions
                {
                    // Instalação manual (usuário seleciona disco/partição durante setup)
                    InstallOptions = new ManualInstallOptions(),

                    // Configurações de idioma e região
                    Language = language,
                    TimeZone = timeZone,
                    ProcessorArchitecture = "amd64"
                };

                // Adicionar conta local se especificado
                if (localAccount && !string.IsNullOrEmpty(userName))
                {
                    var credential = new LocalCredential(
                        userName,
                        password ?? string.Empty, // Senha vazia se não especificada
                        "Administrators"
                    );
                    options.LocalAccounts.Add(credential);
                }

                // Desabilitar Windows Defender se solicitado
                if (disableDefender)
                {
                    options.EnableDefender = false;
                }

                // Desabilitar Cloud features se privacy desabilitado
                if (disablePrivacy)
                {
                    options.EnableCloud = false;
                }

                // Habilitar Área de Trabalho Remota se solicitado
                if (remoteDesktop)
                {
                    options.EnableRemoteDesktop = true;
                }

                // Configurar AutoLogon para instalação totalmente automática
                if (autoLogon && !string.IsNullOrEmpty(userName))
                {
                    var domainUser = new DomainUser(userName); // Usuário local (domain = null)
                    var credential = new DomainCredential(domainUser, password ?? string.Empty);
                    options.AutoLogon = new AutoLogonOptions(credential)
                    {
                        Count = 1
                    };
                }

                // Adicionar comandos pós-instalação se especificados
                if (commands != null && commands.Length > 0)
                {
                    foreach (var cmd in commands)
                    {
                        if (!string.IsNullOrWhiteSpace(cmd))
                        {
                            options.FirstLogonCommands.Add(cmd.Trim());
                        }
                    }
                }

                // Adicionar comandos de registry e tweaks
                var registryCommands = new List<string>();

                // Bypass de requisitos do Windows 11
                if (bypassRequirements)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassTPMCheck /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassSecureBootCheck /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassStorageCheck /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassCPUCheck /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassRAMCheck /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassDiskCheck /t REG_DWORD /d 1 /f");
                }

                // Mostrar todas as edições do Windows
                if (showAllEditions)
                {
                    registryCommands.Add("cmd.exe /c del /f /q X:\\Sources\\ei.cfg");
                    registryCommands.Add("cmd.exe /c echo [Channel] > X:\\Sources\\ei.cfg");
                    registryCommands.Add("cmd.exe /c echo _Default >> X:\\Sources\\ei.cfg");
                    registryCommands.Add("cmd.exe /c echo [VL] >> X:\\Sources\\ei.cfg");
                    registryCommands.Add("cmd.exe /c echo 0 >> X:\\Sources\\ei.cfg");
                }

                // Bypass de Microsoft Account
                if (localAccount)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE\" /v BypassNRO /t REG_DWORD /d 1 /f");
                }

                // Desabilitar BitLocker
                if (disableBitlocker)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\BitLocker\" /v \"PreventDeviceEncryption\" /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\EnhancedStorageDevices\" /v TCGSecurityActivationDisabled /t REG_DWORD /d 1 /f");
                }

                // Desabilitar Hibernação
                if (disableHibernate)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\System\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HibernateEnabled /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FlyoutMenuSettings\" /v ShowHibernateOption /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Windows Copilot
                if (disableCopilot)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot\" /v TurnOffWindowsCopilot /t REG_DWORD /d 1 /f");
                }

                // Desabilitar Cortana
                if (removeCortana)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\Software\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Windows Spotlight
                if (disableSpotlight)
                {
                    registryCommands.Add("reg.exe add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsSpotlightOnLockScreen /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsConsumerFeatures /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsSpotlightActiveUser /t REG_DWORD /d 1 /f");
                }

                // Desabilitar News and Interests
                if (disableNews)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Dsh\" /v AllowNewsAndInterests /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Chat/Teams
                if (disableChat)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Communications\" /v ConfigureChatAutoInstall /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Chat\" /v \"ChatIcon\" /t REG_DWORD /d 3 /f");
                }

                // Desabilitar atualizações automáticas
                if (disableAutoUpdate)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v NoAutoUpdate /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v AutoInstallMinorUpdates /t REG_DWORD /d 0 /f");
                }

                // Atrasar atualizações
                if (delayUpdates)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\\AU\" /v AUOptions /t REG_DWORD /d 3 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v DeferFeatureUpdates /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v DeferFeatureUpdatesPeriodInDays /t REG_DWORD /d 365 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v DeferQualityUpdates /t REG_DWORD /d 1 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v DeferQualityUpdatesPeriodInDays /t REG_DWORD /d 365 /f");
                }

                // Desabilitar Delivery Optimization
                if (disableDeliveryOpt)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DODownloadMode /t REG_DWORD /d 0 /f");
                }

                // Habilitar Long File Paths
                if (longPaths)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\" /v LongPathsEnabled /t REG_DWORD /d 1 /f");
                }

                // Desabilitar Location Tracking
                if (disableLocation)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\location\" /v Value /t REG_SZ /d Deny /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Sensor\\Overrides\\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}\" /v SensorPermissionState /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\lfsvc\\Service\\Configuration\" /v Status /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SYSTEM\\Maps\" /v AutoUpdateEnabled /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Activity History
                if (disableActivity)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v EnableActivityFeed /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v PublishUserActivities /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v UploadUserActivities /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Advertising ID
                if (disableAdID)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo\" /v DisabledByGroupPolicy /t REG_DWORD /d 1 /f");
                }

                // Desabilitar Windows Error Reporting
                if (disableErrorReporting)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Error Reporting\" /v Disabled /t REG_DWORD /d 1 /f");
                }

                // Desabilitar Windows Ink Workspace
                if (disableInkWorkspace)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\WindowsInkWorkspace\" /v AllowWindowsInkWorkspace /t REG_DWORD /d 0 /f");
                }

                // Desabilitar SmartScreen
                if (disableSmartScreen)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\" /v SmartScreenEnabled /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AppHost\" /v EnableWebContentEvaluation /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Sandbox do Defender
                if (disableDefenderSandbox)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows Defender\\Features\" /v TamperProtection /t REG_DWORD /d 0 /f");
                    registryCommands.Add("powershell.exe -Command \"Set-MpPreference -DisableRealtimeMonitoring $true\"");
                }

                // Desabilitar UAC
                if (disableUAC)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v EnableLUA /t REG_DWORD /d 0 /f");
                }

                // Desabilitar Telemetria
                if (disablePrivacy)
                {
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f");
                    registryCommands.Add("reg.exe add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f");
                }

                // Adicionar comandos de registry ao FirstLogonCommands
                foreach (var cmd in registryCommands)
                {
                    options.FirstLogonCommands.Add(cmd);
                }

                // Configurar nome do computador se especificado
                if (!string.IsNullOrEmpty(computerName))
                {
                    options.ComputerName = computerName;
                }

                // Remover Edge se solicitado (requer script PowerShell)
                if (removeEdge)
                {
                    options.FirstLogonCommands.Add("powershell.exe -ExecutionPolicy Bypass -Command \"Invoke-WebRequest -Uri 'https://github.com/ShadowWhisperer/Remove-MS-Edge/blob/main/Remove-NoTerm.exe?raw=true' -OutFile '%TEMP%\\Remove-NoTerm.exe'\"");
                    options.FirstLogonCommands.Add("cmd.exe /c \"%TEMP%\\Remove-NoTerm.exe /silent /install\"");
                }

                // Remover OneDrive se solicitado
                if (removeOneDrive)
                {
                    options.FirstLogonCommands.Add("powershell.exe -Command \"Get-AppxPackage *OneDrive* | Remove-AppxPackage\"");
                }

                // Gerar o arquivo usando o método estático
                AnswerFileGenerator.Generate(outputPath, options);

                Log($"Arquivo autounattend.xml gerado com sucesso em: {outputPath}");
                Log($"Configurações: Bypass={bypassRequirements}, LocalAccount={localAccount}, DisablePrivacy={disablePrivacy}, FullAuto={fullAuto}, ShowAllEditions={showAllEditions}, DisableBitlocker={disableBitlocker}, RemoveEdge={removeEdge}, RemoveCortana={removeCortana}, RemoveOneDrive={removeOneDrive}");
            }
            catch (Exception ex)
            {
                Log($"Erro ao gerar autounattend.xml: {ex.Message}");
                throw;
            }
        }

        // --- DISK ENGINE ---
        public static List<DiskInfo> GetDisks(bool filterWinboot = false, bool safeMode = false)
        {
            var disks = new List<DiskInfo>();
            try
            {
                using var diskDriveQuery = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                using var diskResults = diskDriveQuery.Get();
                foreach (ManagementObject diskDrive in diskResults)
                {
                    using (diskDrive)
                    {
                        var disk = new DiskInfo
                        {
                            Index = (uint)diskDrive["Index"],
                            Model = diskDrive["Model"]?.ToString() ?? "Desconhecido",
                            Interface = diskDrive["InterfaceType"]?.ToString() ?? "USB/SATA/NVMe",
                            Size = (ulong)diskDrive["Size"]
                        };

                        using var partitionQuery = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{diskDrive["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
                        using var partitionResults = partitionQuery.Get();
                        foreach (ManagementObject partition in partitionResults)
                        {
                            using (partition)
                            {
                                var partInfo = new PartitionInfo
                                {
                                    Index = (uint)partition["Index"],
                                    DiskIndex = disk.Index,
                                    Name = partition["Name"]?.ToString() ?? "Partição",
                                    Size = (ulong)partition["Size"]
                                };

                                using var logicalDiskQuery = new ManagementObjectSearcher($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                                using var logicalResults = logicalDiskQuery.Get();
                                foreach (ManagementObject logicalDisk in logicalResults)
                                {
                                    using (logicalDisk)
                                    {
                                        partInfo.DriveLetter = logicalDisk["DeviceID"]?.ToString() ?? string.Empty;
                                        partInfo.Label = logicalDisk["VolumeName"]?.ToString() ?? string.Empty;
                                        partInfo.FileSystem = logicalDisk["FileSystem"]?.ToString() ?? "RAW";
                                        partInfo.FreeSpace = (ulong)logicalDisk["FreeSpace"];
                                    }
                                }
                                if (filterWinboot)
                                {
                                    // 20GB mínimo (Garante ocultação total de MSR, EFI, Recovery e do Winboot de 8GB)
                                    if (partInfo.Size < 20000000000) continue;

                                    if (safeMode)
                                    {
                                        // 🔥 MODO SEGURO - Não usa strings de texto, apenas verificações estruturais
                                        // MSR, EFI, Recovery são geralmente < 20GB ou têm tipos específicos
                                        // Winboot é identificado pelo label WINBOOT_LABEL
                                        if (partInfo.Label.Equals(WINBOOT_LABEL, StringComparison.OrdinalIgnoreCase)) continue;
                                        if (partInfo.Label.Equals("Winboot", StringComparison.OrdinalIgnoreCase)) continue;
                                    }
                                    else
                                    {
                                        // 🔥 FALLBACKS PARA MÚLTIPLOS IDIOMAS - Funciona com ISOs em qualquer idioma
                                        // System partitions (English, Portuguese, Spanish, French, German, Italian, Russian, Chinese, Japanese, Korean)
                                        string[] systemLabels = { "System", "Sistema", "Système", "Systemlaufwerk", "Sistema operativo", "Система", "系统", "システム", "시스템" };
                                        if (systemLabels.Any(l => partInfo.Label.Contains(l, StringComparison.OrdinalIgnoreCase))) continue;

                                        // Recovery partitions (English, Portuguese, Spanish, French, German, Italian, Russian, Chinese, Japanese, Korean)
                                        string[] recoveryLabels = { "Recovery", "Recuperação", "Recuperación", "Récupération", "Wiederherstellung", "Ripristino", "Восстановление", "恢复", "復旧", "복구" };
                                        if (recoveryLabels.Any(l => partInfo.Label.Contains(l, StringComparison.OrdinalIgnoreCase))) continue;

                                        // Reserved partitions (English, Portuguese, Spanish, French, German, Italian, Russian, Chinese, Japanese, Korean)
                                        string[] reservedLabels = { "Reserved", "Reservado", "Reservado", "Réservé", "Reserviert", "Riservato", "Зарезервировано", "保留", "予約", "예약" };
                                        if (reservedLabels.Any(l => partInfo.Label.Contains(l, StringComparison.OrdinalIgnoreCase))) continue;

                                        // Winboot partitions (para não selecionar a própria partição Winboot)
                                        if (partInfo.Label.Equals(WINBOOT_LABEL, StringComparison.OrdinalIgnoreCase)) continue;
                                        if (partInfo.Label.Equals("Winboot", StringComparison.OrdinalIgnoreCase)) continue;
                                    }
                                }

                                disk.Partitions.Add(partInfo);
                            }
                        }
                        disks.Add(disk);
                    }
                }
            }
            catch (Exception ex) { Logger.Log($"Erro WinbootManager.GetDisks: {ex.Message}"); }
            return disks;
        }

        public static List<PartitionInfo> GetRemovablePartitions()
        {
             var allDisks = GetDisks(false);
             var candidates = new List<PartitionInfo>();

             foreach (var d in allDisks)
             {
                 foreach (var p in d.Partitions)
                 {
                     // FILTER: Safety (> 6GB)
                     if (p.Size < 6442450944) continue; // 6GB in bytes

                     // FILTER: Suspect Label
                     bool isSuspect = p.Label.Contains("Winboot", StringComparison.OrdinalIgnoreCase) ||
                                      p.Label.Contains("NAO_DELETAR", StringComparison.OrdinalIgnoreCase) ||
                                      p.Label.Contains("LUGIA", StringComparison.OrdinalIgnoreCase);

                     if (isSuspect)
                     {
                         candidates.Add(p);
                     }
                 }
             }
             return candidates;
        }

        // --- LOGGING ENGINE ---
        private static StringBuilder _logSession = new StringBuilder();
        public static event Action<string>? OnLogUpdate;

        public static void Log(string message)
        {
            string logLine = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logSession.AppendLine(logLine);
            OnLogUpdate?.Invoke(logLine);

            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KitLugia", "Logs");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "Winboot.log"), logLine + Environment.NewLine);
            }
            catch { }
        }

        public static string GetSessionLog() => _logSession.ToString();

        // --- DRIVER MAGIC ---
        public static async Task<bool> ExportHostDrivers(string targetDir)
        {
            Log($"Exportando drivers do host para {targetDir}...");
            return await Task.Run(async () =>
            {
                try
                {
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    // Detectar DISM do host
                    string dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
                    if (!File.Exists(dismPath))
                    {
                        Log("ERRO: DSM.exe não encontrado no System32.");
                        return false;
                    }

                    // Exportar drivers
                    var (code, output) = await RunProcessCaptured(dismPath, $"/online /export-driver /destination:\"{targetDir}\"");
                    if (code != 0)
                    {
                        Log($"ERRO ao exportar drivers: {output}");
                        return false;
                    }

                    Log("Exportação de drivers concluída com sucesso.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"EXCEÇÃO ao exportar drivers: {ex.Message}");
                    return false;
                }
            });
        }

        // --- DIAGNOSTICS ---
        public static async Task<List<string>> PerformDiagnostics(string isoPath)
        {
            return await Task.Run(() =>
            {
                var errors = new List<string>();
                Log("Iniciando diagnósticos de sistema...");

                // 1. Admin Check
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem"))
                    {
                        var results = searcher.Get();
                        Log("WMI: OK (Serviço de gerenciamento funcionando)");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add("WMI Error: Falha ao acessar informações do sistema. Rode como Admin.");
                    Log($"ERRO WMI: {ex.Message}");
                }

                // 2. ISO Check
                if (!string.IsNullOrEmpty(isoPath))
                {
                    if (File.Exists(isoPath))
                    {
                        var info = new FileInfo(isoPath);
                        Log($"ISO: Encontrada ({info.Length / 1024 / 1024} MB)");
                    }
                    else
                    {
                        errors.Add("ISO: Arquivo não encontrado no caminho especificado.");
                        Log("ERRO ISO: Arquivo inexistente.");
                    }
                }

                // 3. Tools Check
                string[] tools = { "diskpart.exe", "bcdedit.exe", "robocopy.exe", "powershell.exe" };
                foreach (var tool in tools)
                {
                    if (File.Exists(Path.Combine(Environment.SystemDirectory, tool)) || 
                        File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "WindowsPowerShell", "v1.0", tool)))
                        Log($"{tool}: OK");
                    else
                    {
                        errors.Add($"{tool}: Ferramenta de sistema não encontrada.");
                        Log($"ERRO: {tool} ausente.");
                    }
                }


                return errors;
            });
        }


        // --- BOOT SERVICE ---
        public static async Task<string?> CreateRamdiskEntry(string description, string driveLetter, string wimPath, string sdiPath)
        {
            Log($"Configurando entradas BCD para WIM: {description}...");
            try
            {
                string cleanDesc = SanitizeDescription(description);
                await RunBcdeditLogged($"/create {{ramdiskoptions}} /d \"{cleanDesc}\"");
                await RunBcdeditLogged($"/set {{ramdiskoptions}} ramdisksdidevice partition={driveLetter}");
                await RunBcdeditLogged($"/set {{ramdiskoptions}} ramdisksdipath {sdiPath}");

                string createResult = await RunBcdeditLogged($"/create /d \"{cleanDesc}\" /application osloader");
                var match = Regex.Match(createResult, @"{[a-fA-F0-9-]+}");
                if (!match.Success)
                {
                    Log("ERRO: Falha ao obter GUID da nova entrada BCD.");
                    return null;
                }

                string newGuid = match.Value;
                Log($"ID Criado: {newGuid}");
                await RunBcdeditLogged($"/set {newGuid} device ramdisk=[{driveLetter}]{wimPath},{{ramdiskoptions}}");
                await RunBcdeditLogged($"/set {newGuid} osdevice ramdisk=[{driveLetter}]{wimPath},{{ramdiskoptions}}");
                await RunBcdeditLogged($"/set {newGuid} path \\windows\\system32\\boot\\winload.efi");
                await RunBcdeditLogged($"/set {newGuid} systemroot \\windows");
                await RunBcdeditLogged($"/set {newGuid} detecthal yes");
                await RunBcdeditLogged($"/set {newGuid} winpe yes");
                await RunBcdeditLogged($"/displayorder {newGuid} /addlast");

                Log("BCD: Configuração WIM finalizada com sucesso.");
                return newGuid;
            }
            catch (Exception ex)
            {
                Log($"ERRO BCD: {ex.Message}");
                return null;
            }
        }

        public static async Task<string?> CreateEfiBootEntry(string description, string driveLetter, string efiPath)
        {
            Log($"Configurando entradas BCD para EFI (Universal Chainload): {description}...");
            try
            {
                string cleanDesc = SanitizeDescription(description);
                // TENTATIVA FINAL: Usar 'osloader' apontando diretamente para o Shim/Grub específico.
                // Se isso falhar com 0xc000007b, é bloqueio do Windows Boot Manager.
                string createResult = await RunBcdeditLogged($"/create /d \"{cleanDesc}\" /application osloader");
                var match = Regex.Match(createResult, @"{[a-fA-F0-9-]+}");
                if (!match.Success) return null;

                string newGuid = match.Value;
                string cleanDrive = driveLetter.Replace(":", "");
                
                await RunBcdeditLogged($"/set {newGuid} device partition={cleanDrive}:");
                await RunBcdeditLogged($"/set {newGuid} path {efiPath}");
                
                // Configurações padrão para chainload
                await RunBcdeditLogged($"/set {newGuid} recoveryenabled No");
                await RunBcdeditLogged($"/set {newGuid} osdevice partition={cleanDrive}:");
                await RunBcdeditLogged($"/set {newGuid} systemroot \\Unidentified_System"); // Placebo para satisfazer verificações
                
                await RunBcdeditLogged($"/displayorder {newGuid} /addlast");

                Log("BCD: Configuração EFI Shim/Grub finalizada.");
                return newGuid;
            }
            catch (Exception ex)
            {
                Log($"ERRO BCD EFI: {ex.Message}");
                return null;
            }
        }

        public static async Task<string?> CreateLegacyBootSectorEntry(string description, string driveLetter, string binPath)
        {
            Log($"Configurando entradas BCD para Legacy BootSector: {description}...");
            try
            {
                string createResult = await RunBcdeditLogged($"/create /d \"{description}\" /application bootsector");
                var match = Regex.Match(createResult, @"{[a-fA-F0-9-]+}");
                if (!match.Success) return null;

                string newGuid = match.Value;
                string cleanDrive = driveLetter.Replace(":", "");
                await RunBcdeditLogged($"/set {newGuid} device partition={cleanDrive}:");
                await RunBcdeditLogged($"/set {newGuid} path {binPath}");
                await RunBcdeditLogged($"/displayorder {newGuid} /addlast");

                Log("BCD: Configuração Legacy BootSector finalizada.");
                return newGuid;
            }
            catch (Exception ex)
            {
                Log($"ERRO BCD Legacy: {ex.Message}");
                return null;
            }
        }

        // REMOVIDO: Método experimental de firmware removido para garantir 100% de segurança no PC do usuário.

        private static async Task<string> RunBcdeditLogged(string args)
        {
            var (code, output) = await RunProcessCaptured("bcdedit.exe", args);
            Log($"> bcdedit {args}");
            if (code != 0) Log($"[!] Alerta: Saída erro {code}: {output}");
            return output;
        }

        private static async Task<(int ExitCode, string Output)> RunProcessCaptured(string filename, string args)
        {
            return await Task.Run(() =>
            {
                var psi = new ProcessStartInfo(filename, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var proc = Process.Start(psi);
                string output = proc?.StandardOutput.ReadToEnd() ?? "";
                string error = proc?.StandardError.ReadToEnd() ?? "";
                proc?.WaitForExit();

                return (proc?.ExitCode ?? -1, output + error);
            });
        }

        private static string SanitizeDescription(string description)
        {
            if (string.IsNullOrEmpty(description)) return "KitLugia_Entry";
            // Remove aspas e caracteres que quebram bcdedit e echo
            return description.Replace("\"", "").Replace("'", "").Replace("`", "").Replace(";", "").Replace("(", "").Replace(")", "").Replace(" ", "_").Trim();
        }

        public struct BootFileInfo
        {
            public string WimPath;
            public string SdiPath;
            public string Description;
            public bool IsWim;
            public bool IsEfi;
            public string EfiPath;
            public string SafetyWarning; // Novo: Aviso se o Boot Manager pode bloquear
        }

        public static async Task<BootFileInfo?> DetectBootFile(string driveLetter)
        {
            return await Task.Run(() =>
            {
                string drive = driveLetter.Replace(":", "");
                
                // 1. Check for Standard Windows / WinPE
                string[] commonWims = { 
                    $"{drive}:\\sources\\boot.wim", 
                    $"{drive}:\\sources\\install.wim",
                    $"{drive}:\\SSTR\\strelec10x64Eng.wim", // Sergei Strelec
                    $"{drive}:\\SSTR\\strelec10x64.wim",
                    $"{drive}:\\SSTR\\strelec8x64.wim"
                };

                foreach (var wim in commonWims)
                {
                    if (File.Exists(wim))
                    {
                        string sdi = $"{drive}:\\boot\\boot.sdi";
                        if (!File.Exists(sdi))
                        {
                            // Try to find any .sdi
                            var sdiFiles = Directory.GetFiles($"{drive}:\\", "*.sdi", SearchOption.AllDirectories);
                            sdi = sdiFiles.FirstOrDefault() ?? "";
                        }

                        return new BootFileInfo
                        {
                            WimPath = wim.Substring(2), // Just the path from root
                            SdiPath = string.IsNullOrEmpty(sdi) ? "" : sdi.Substring(2),
                            Description = wim.Contains("strelec", StringComparison.OrdinalIgnoreCase) ? "Sergei Strelec PE" : "KitLugia Winboot Setup",
                            IsWim = true
                        };
                    }
                }

                // 2. Check for Linux / Generic EFI / GRUB / ISOLINUX
                // Prioridade: Shim (Assinado) -> Grub (Nativo) -> Bootx64 (Genérico)
                string[] efiLoaders = {
                    $"{drive}:\\EFI\\ubuntu\\shimx64.efi",      // Ubuntu/Mint Signed
                    $"{drive}:\\EFI\\fedora\\shimx64.efi",      // Fedora Signed
                    $"{drive}:\\EFI\\debian\\shimx64.efi",      // Debian
                    $"{drive}:\\EFI\\opensuse\\shim.efi",       // OpenSUSE
                    $"{drive}:\\EFI\\BOOT\\grubx64.efi",        // Fallback Grub
                    $"{drive}:\\EFI\\BOOT\\BOOTX64.EFI"         // Generic Fallback
                };

                string[] legacyLoaders = {
                    $"{drive}:\\isolinux\\isolinux.bin",
                    $"{drive}:\\boot\\isolinux\\isolinux.bin",
                    $"{drive}:\\isolinux.bin"
                };
                
                // Generic check for Linux signature files
                string[] linuxSignatures = {
                    $"{drive}:\\casper\\vmlinuz",
                    $"{drive}:\\live\\vmlinuz",
                    $"{drive}:\\vmlinuz"
                };

                foreach (var linux in linuxSignatures)
                {
                    if (File.Exists(linux))
                    {
                        // Found Linux, now find best loader based on mode
                        bool isSystemEfi = IsEfiMode();
                        string bestLoader = isSystemEfi ? efiLoaders.FirstOrDefault(File.Exists) ?? linux : legacyLoaders.FirstOrDefault(File.Exists) ?? linux;
                        
                        string distro = "Linux (Genérico)";
                        if (File.Exists($"{drive}:\\.disk\\info")) distro = File.ReadAllText($"{drive}:\\.disk\\info");
                        else if (File.Exists($"{drive}:\\etc\\os-release")) distro = "Linux (OS-Release)";
                        else if (File.Exists($"{drive}:\\ubuntu")) distro = "Ubuntu";
                        else if (File.Exists($"{drive}:\\fedora")) distro = "Fedora";

                        return new BootFileInfo
                        {
                            Description = distro.Length > 30 ? distro.Substring(0, 30) : distro,
                            IsEfi = isSystemEfi,
                            IsWim = false,
                            EfiPath = bestLoader.Contains(":") ? bestLoader.Substring(2) : bestLoader,
                            SafetyWarning = "Modo Turbo: O KitLugia tentará ajustar o GRUB automaticamente para bootar deste drive."
                        };
                    }
                }

                foreach (var efi in efiLoaders)
                {
                    if (File.Exists(efi))
                    {
                        return new BootFileInfo
                        {
                            Description = "Generic Multi-ISO / Linux",
                            IsEfi = true,
                            IsWim = false,
                            EfiPath = efi.Contains(":") ? efi.Substring(2) : efi,
                            SafetyWarning = "Este tipo de ISO pode ser bloqueado pelo Windows (Erro 0xc000007b). Recomenda-se o uso do Menu de Boot (F12) se falhar."
                        };
                    }
                }

                return (BootFileInfo?)null;
            });
        }

        public static async Task<BootFileInfo?> IdentifyIsoType(string isoPath)
        {
            Log($"Identificando conteúdo da ISO: {Path.GetFileName(isoPath)}...");
            return await Task.Run(async () =>
            {
                try
                {
                    await RunProcessCaptured("powershell.exe", $"-Command \"Mount-DiskImage -ImagePath '{isoPath}' -StorageType ISO -Access ReadOnly\"");
                    await Task.Delay(1500); // Wait for mount

                    var (getLetterCode, getLetterOutput) = await RunProcessCaptured("powershell.exe", $"-Command \"(Get-DiskImage -ImagePath '{isoPath}' | Get-Volume).DriveLetter\"");
                    string isoDrive = getLetterOutput.Trim().Replace("\r", "").Replace("\n", "");
                    
                    BootFileInfo? info = null;
                    if (!string.IsNullOrEmpty(isoDrive))
                    {
                        info = await DetectBootFile(isoDrive);
                        
                        // Add property to indicate if we are in Legacy mode or not for this detection
                        // We check the system, but the ISO might have different loaders.
                        // We will add a 'IsLegacy' flag to BootFileInfo in a separate step if needed.
                        
                        Log($"Detecção rápida: {info?.Description ?? "Tipo Desconhecido"}");
                    }

                    await RunProcessCaptured("powershell.exe", $"-Command \"Dismount-DiskImage -ImagePath '{isoPath}'\"");
                    return info;
                }
                catch (Exception ex)
                {
                    Log($"Erro na identificação: {ex.Message}");
                    return null;
                }
            });
        }

        public static async Task<BootFileInfo?> ExtractFiles(string isoPath, string targetPath)
        {
            Log($"Extraindo ISO {isoPath} via 7-Zip Direto (Modo Turbo) para {targetPath}...");

            return await Task.Run(async () =>
            {
                try
                {
                    string sevenZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "App", "7Zip", "7z.exe");
                    
                    if (!File.Exists(sevenZipPath))
                    {
                        Log($"ERRO: 7-Zip não encontrado em {sevenZipPath}");
                        return (BootFileInfo?)null;
                    }

                    // 1. Extração Direta com 7-Zip
                    Log("Iniciando Extração Direta I/O pela RAM...");
                    string args = $"x \"{isoPath}\" -o\"{targetPath}\" -y";
                    
                    var (extCode, extOut) = await RunProcessCaptured(sevenZipPath, args);
                    
                    // 7-Zip return codes: 0 = No error, 1 = Warning (Non fatal errors)
                    if (extCode != 0 && extCode != 1) 
                    {
                        Log($"ERRO 7-Zip (Código {extCode}): {extOut}");
                        return (BootFileInfo?)null;
                    }

                    Log("Cópia de arquivos finalizada em altíssima velocidade.");

                    // 2. DETECTAR BOOT FILE no destino
                    var bootInfo = await DetectBootFile(targetPath);

                    return bootInfo;
                }
                catch (Exception ex)
                {
                    Log($"FALHA NA EXTRAÇÃO (7-Zip): {ex.Message}");
                    return (BootFileInfo?)null;
                }
            });
        }

        public static async Task<bool> ApplyCustomizations(string winbootDrive, bool bypassRequirements, bool localAccount, bool disablePrivacy, bool injectKit, bool autoCleanup, string? customXmlPath, string? userName, string? password, bool fullAuto, uint targetDisk, uint targetPartition, string? injectedFilesPath = null, bool safeMode = false, Func<string, Task<bool>>? downloadConfirmationCallback = null)
        {
            var modeText = safeMode ? "MODO SEGURO (Sem strings de texto - 100% universal)" : "PADRÃO";
            Log($"Aplicando customizações na unidade {winbootDrive} (Modo: {modeText})...");

            return await Task.Run(async () =>
            {
                try
                {
                    // 0. Gravar Alvo (Legacy)

                    // 1. Unattend.xml
                    string targetXml = Path.Combine(winbootDrive, "autounattend.xml");
                    if (!string.IsNullOrEmpty(customXmlPath) && File.Exists(customXmlPath))
                    {
                        // Se for um perfil customizado (E2B), tentamos injetar o nome de usuário/senha se fornecido
                        if (!string.IsNullOrEmpty(userName))
                        {
                            Log($"Customizando Perfil E2B com usuário: {userName}");
                            string xmlContent = File.ReadAllText(customXmlPath);
                            string patchedXml = PatchUnattendXml(xmlContent, userName, password);
                            File.WriteAllText(targetXml, patchedXml, Encoding.UTF8);
                        }
                        else
                        {
                            File.Copy(customXmlPath, targetXml, true);
                        }
                        Log($"Arquivo Unattend customizado importado/patchado de: {customXmlPath}");
                    }
                    else
                    {
                        string xmlContent = GenerateAutounattend(bypassRequirements, localAccount, disablePrivacy, injectKit, autoCleanup, userName, password, fullAuto, targetDisk, targetPartition);
                        File.WriteAllText(targetXml, xmlContent, Encoding.UTF8);
                        Log($"Arquivo autounattend.xml gerado (Target: Disk {targetDisk}, Part {targetPartition + 1}).");
                    }


                    // 2. Injeção de Arquivos (KitLugia + Scripts)
                    string setupDir = Path.Combine(winbootDrive, "_KitLugiaSetup");
                    Directory.CreateDirectory(setupDir);

                    // E2B METHODOLOGY: Se for um perfil E2B, precisamos da estrutura \_ISO\E2B para o FiraDisk
                    string e2bBaseDir = Path.Combine(winbootDrive, "_ISO", "E2B");
                    string firaDiskDir = Path.Combine(e2bBaseDir, "FIRADISK");
                    
                    
                    // Estrutura para Injeção de Arquivos do Usuário
                    if (!string.IsNullOrEmpty(injectedFilesPath) && Directory.Exists(injectedFilesPath))
                    {
                        Log($"Preparando injeção de arquivos de: {injectedFilesPath}");
                        string injectedTarget = Path.Combine(setupDir, "Injected");
                        Directory.CreateDirectory(injectedTarget);
                        CopyDirectory(injectedFilesPath, injectedTarget);
                    }
                    
                    Log("Preparando estrutura de compatibilidade Easy2Boot (_ISO/E2B)...");
                    Directory.CreateDirectory(firaDiskDir);

                    // PATH PORTABILIDADE: Sempre usar a pasta local do App
                    string goodiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "BootGoodies");
                    
                    if (!Directory.Exists(goodiesPath))
                    {
                        // Fallback apenas para debug/dev se não foi compilado ainda
                        string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                        goodiesPath = Path.Combine(projectRoot, "KitLugia.Core", "Resources", "BootGoodies");
                    }

                    if (Directory.Exists(Path.Combine(goodiesPath, "E2B_FiraDisk")))
                    {
                        Log("Copiando ferramentas FiraDisk/E2B para a partição...");
                        CopyDirectory(Path.Combine(goodiesPath, "E2B_FiraDisk"), firaDiskDir);
                    }

                    if (injectKit || autoCleanup)
                    {
                        if (injectKit)
                        {
                            Log("Injetando arquivos do KitLugia para auto-instalação...");
                            string appSource = AppDomain.CurrentDomain.BaseDirectory;
                            CopyDirectory(appSource, Path.Combine(setupDir, "App"));
                        }

                        // 🔥 Download automático do .NET Runtime se não existir localmente
                        string dotnetRuntimeSource = Path.Combine(goodiesPath, "dotnet-runtime.exe");

                        if (!File.Exists(dotnetRuntimeSource))
                        {
                            Log("Instalador offline do .NET Runtime não encontrado.");
                            Log("O KitLugia pode baixar automaticamente o .NET Desktop Runtime 8.0 (~50MB) para instalação offline.");

                            // Pergunta ao usuário se deseja baixar (se callback fornecido)
                            bool shouldDownload = true;
                            if (downloadConfirmationCallback != null)
                            {
                                try
                                {
                                    shouldDownload = await downloadConfirmationCallback(
                                        "O instalador do .NET Desktop Runtime 8.0 não foi encontrado localmente.\n\n" +
                                        "Deseja baixar automaticamente (~50MB)?\n\n" +
                                        "- Sim: Baixa automaticamente e salva para uso futuro\n" +
                                        "- Não: O Winboot tentará instalar via winget na primeira inicialização (requer internet)"
                                    );
                                }
                                catch (Exception ex)
                                {
                                    Log($"⚠️ Erro ao obter confirmação de download: {ex.Message}");
                                    Log("Baixando automaticamente...");
                                }
                            }
                            else
                            {
                                Log("Callback não fornecido. Baixando automaticamente...");
                            }

                            if (shouldDownload)
                            {
                                Log("Iniciando download automático...");
                                try
                                {
                                    // URL direto do Microsoft CDN para .NET Desktop Runtime 8.0.15 x64 (LTS)
                                    string dotnetUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.15/windowsdesktop-runtime-8.0.15-win-x64.exe";
                                    string tempDownloadPath = Path.Combine(Path.GetTempPath(), "windowsdesktop-runtime-8.0.15-win-x64.exe");

                                    Log($"Baixando .NET Runtime de: {dotnetUrl}");
                                    Log("Isso pode levar alguns minutos (tamanho aproximado: 50MB)...");

                                    using (var client = new System.Net.WebClient())
                                    {
                                        client.DownloadProgressChanged += (sender, e) =>
                                        {
                                            if (e.ProgressPercentage % 10 == 0 && e.ProgressPercentage > 0)
                                            {
                                                Log($"Download: {e.ProgressPercentage}% ({e.BytesReceived / 1024 / 1024}MB / {e.TotalBytesToReceive / 1024 / 1024}MB)");
                                            }
                                        };
                                        client.DownloadFile(dotnetUrl, tempDownloadPath);
                                    }

                                    // Copia para Resources para uso futuro
                                    File.Copy(tempDownloadPath, dotnetRuntimeSource, true);
                                    Log("✅ .NET Runtime baixado com sucesso e salvo em Resources!");

                                    // Limpa arquivo temporário
                                    try { File.Delete(tempDownloadPath); } catch { }
                                }
                                catch (Exception ex)
                                {
                                    Log($"⚠️ Falha ao baixar .NET Runtime automaticamente: {ex.Message}");
                                    Log("O Winboot prosseguirá normalmente e tentará instalar via winget na primeira inicialização (requer internet).");
                                }
                            }
                            else
                            {
                                Log("Download cancelado pelo usuário.");
                                Log("O Winboot prosseguirá normalmente e tentará instalar via winget na primeira inicialização (requer internet).");
                            }
                        }
                        else
                        {
                            Log("✅ Instalador offline do .NET Runtime encontrado localmente.");
                        }

                        // Copiar instalador offline do .NET Runtime para a partição Winboot
                        if (File.Exists(dotnetRuntimeSource))
                        {
                            Log("Copiando instalador offline do .NET Runtime 8.0 para a partição Winboot...");
                            File.Copy(dotnetRuntimeSource, Path.Combine(setupDir, "dotnet-runtime.exe"), true);
                        }
                        else
                        {
                            Log("AVISO: Instalador offline do .NET Runtime não disponível. O Winboot tentará instalar via winget (requer internet).");
                        }

                        if (autoCleanup)
                        {
                            Log("Gerando script de auto-limpeza (Cleanup)...");
                            // Script de limpeza PERSISTENTE (Tenta até conseguir)
                            string cleanupBat = "@echo off\n" +
                                              "echo Buscando unidade LugiaBoot para limpeza...\n" +
                                              ":search\n" +
                                              "set TARGET_DRIVE=\n" +
                                              "for %%i in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (\n" +
                                              "  if exist \"%%i:\\_KitLugiaSetup\\first_logon.bat\" set TARGET_DRIVE=%%i\n" +
                                              ")\n" +
                                              "if \"%TARGET_DRIVE%\"==\"\" (\n" +
                                              "  echo Unidade nao encontrada ou ja removida.\n" +
                                              "  exit\n" +
                                              ")\n" +
                                              "echo Unidade detectada: %TARGET_DRIVE%. Tentando remover...\n" +
                                              ":retry\n" +
                                              "(echo select volume %TARGET_DRIVE%\n" +
                                              " echo delete partition override\n" +
                                              " echo select volume c\n" +
                                              " echo extend\n" +
                                              " echo exit) > %temp%\\dp_clean.txt\n" +
                                              "diskpart /s %temp%\\dp_clean.txt > nul 2>&1\n" +
                                              "if exist \"%TARGET_DRIVE%:\\_KitLugiaSetup\\first_logon.bat\" (\n" +
                                              "  echo Falha ao remover (particao em uso). Tentando novamente em 10s...\n" +
                                              "  timeout /t 10 > nul\n" +
                                              "  goto retry\n" +
                                              ")\n" +
                                              "echo Sucesso! Particao removida e espaco restaurado.\n" +
                                          "echo Removendo atalhos de instalacao...\n" +
                                          "if exist \"%userprofile%\\Desktop\\Restaurar_Espaco_Lugia.lnk\" del /f /q \"%userprofile%\\Desktop\\Restaurar_Espaco_Lugia.lnk\"\n" +
                                          "echo Removendo entrada de boot (BCD)...\n" +
                                          "for /f \"tokens=2 delims={}\" %%a in ('bcdedit /enum all ^| findstr /c:\"KitLugia Winboot Setup\" /B /S') do bcdedit /delete {%%a} /f > nul 2>&1\n" +
                                          "schtasks /delete /tn \"KitLugiaCleanup\" /f > nul 2>&1\n" +
                                          "echo Limpeza concluida. A pasta " + KitLugiaInstallPath + " foi mantida conforme solicitado.\n" +
                                          "timeout /t 3 > nul\n" +
                                          "exit";
                            File.WriteAllText(Path.Combine(setupDir, "cleanup.bat"), cleanupBat);
                            
                            // Arquivo de aviso para o usuário não deletar na tela de formatação
                            File.WriteAllText(Path.Combine(winbootDrive, "!!!_NAO_DELETER_ESTA_PARTICAO_!!!.txt"), "ESTA PARTICAO CONTEM OS ARQUIVOS DE INSTALACAO DO WINDOWS. SE VOCE DELETER ELA, A INSTALACAO VAI FALHAR!");
                        }


                        // 2.1. Script de Primeiro Logon que orquestra tudo
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("@echo off");
                        sb.AppendLine("TITLE KitLugia - Finalizando Configuracao");
                        sb.AppendLine("color 0E");
                        sb.AppendLine("echo =========================================");
                        sb.AppendLine("echo   KITLUGIA AUTOMATION - NAO FECHE ESTA JANELA");
                        sb.AppendLine("echo =========================================");
                        sb.AppendLine("echo Aplicando ajustes finais no sistema...");

                        // Verificar e instalar .NET Desktop Runtime 8.0 se necessário (usando instalador offline)
                        sb.AppendLine("echo Verificando requisitos de sistema (.NET 8)...");
                        sb.AppendLine("reg query \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\" /s | findstr \".NET Desktop Runtime 8\" > nul 2>&1");
                        sb.AppendLine("if errorlevel 1 (");
                        sb.AppendLine("  echo .NET Desktop Runtime 8.0 nao encontrado. Instalando...");
                        sb.AppendLine("  if exist \"%~dp0dotnet-runtime.exe\" (");
                        sb.AppendLine("    echo Executando instalador offline (pode levar alguns minutos)...");
                        sb.AppendLine("    \"%~dp0dotnet-runtime.exe\" /install /quiet /norestart");
                        sb.AppendLine("    echo .NET Desktop Runtime 8.0 instalado com sucesso.");
                        sb.AppendLine("  ) else (");
                        sb.AppendLine("    echo AVISO: Instalador offline nao encontrado. Tentando via winget...");
                        sb.AppendLine("    winget install Microsoft.DotNet.DesktopRuntime.8 --silent --accept-package-agreements --accept-source-agreements");
                        sb.AppendLine("  )");
                        sb.AppendLine(") else (");
                        sb.AppendLine("  echo .NET Desktop Runtime 8.0 ja esta instalado.");
                        sb.AppendLine(")");

                        sb.AppendLine("timeout /t 5 > nul");
                        
                        if (injectKit)
                        {
                            sb.AppendLine("echo Instalando KitLugia (Robocopy Mode)...");
                            sb.AppendLine($"if not exist \"{KitLugiaInstallPath}\" mkdir \"{KitLugiaInstallPath}\"");
                            sb.AppendLine($"robocopy \"%~dp0App\" \"{KitLugiaInstallPath}\" /E /R:3 /W:5 /MT /NP");
                            
                            // Copiar o script de limpeza para o C: para execução persistente e segura
                            if (autoCleanup)
                            {
                                sb.AppendLine($"copy /Y \"%~dp0cleanup.bat\" \"{KitLugiaInstallPath}\\cleanup.bat\"");
                            }

                            // Criar Atalhos no Desktop via PowerShell
                            sb.AppendLine("echo Criando atalhos na Area de Trabalho...");
                            string psLaunch = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\\KitLugia.lnk');$s.TargetPath='{KitLugiaInstallPath}\\KitLugia.GUI.exe';$s.WorkingDirectory='{KitLugiaInstallPath}';$s.Save()";
                            sb.AppendLine($"powershell -NoProfile -Command \"{psLaunch}\"");
                        }

                        // Mover arquivos injetados para o Desktop Público
                        sb.AppendLine("if exist \"%~dp0Injected\" (");
                        sb.AppendLine("  echo Movendo arquivos injetados para Area de Trabalho Publica...");
                        sb.AppendLine("  if not exist \"C:\\Users\\Public\\Desktop\\Injected_Files\" mkdir \"C:\\Users\\Public\\Desktop\\Injected_Files\"");
                        sb.AppendLine("  robocopy \"%~dp0Injected\" \"C:\\Users\\Public\\Desktop\\Injected_Files\" /E /R:3 /W:5 /MT /NP");
                        sb.AppendLine(")");
                        
                        if (autoCleanup)
                        {
                            // Atalho para Cleanup Manual se falhar o automático
                            string psCleanup = $"$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\\Restaurar_Espaco_Lugia.lnk');$s.TargetPath='{KitLugiaInstallPath}\\cleanup.bat';$s.IconLocation='C:\\Windows\\System32\\shell32.dll,238';$s.Save()";
                            sb.AppendLine($"powershell -NoProfile -Command \"{psCleanup}\"");

                            sb.AppendLine("echo Iniciando limpeza automatica (Modo Persistente)...");
                            // Tenta limpar na hora via o script local no C:
                            sb.AppendLine($"start /min \"\" cmd /c \"call {KitLugiaInstallPath}\\cleanup.bat\"");
                            
                            // Agendar tarefa persistente de limpeza (SYSTEM) para o Logon
                            // Roda o script que está no C:, que não será deletado
                            sb.AppendLine("echo Agendando limpeza persistente no proximo logon...");
                            sb.AppendLine($"schtasks /create /tn \"KitLugiaCleanup\" /tr \"cmd /c \\\"{KitLugiaInstallPath}\\cleanup.bat\\\"\" /sc onlogon /rl highest /f");
                        }
                        
                        if (injectKit)
                        {
                            sb.AppendLine("echo Abrindo KitLugia...");
                            sb.AppendLine($"start \"\" \"{KitLugiaInstallPath}\\KitLugia.GUI.exe\""); 
                        }

                        sb.AppendLine("echo Concluido! Esta janela fechara em instantes.");
                        sb.AppendLine("timeout /t 5 > nul");
                        sb.AppendLine("exit");
                        File.WriteAllText(Path.Combine(setupDir, "first_logon.bat"), sb.ToString());
                    }

                    // 3. Bypass via Registro (para WinPE)
                    if (bypassRequirements)
                    {
                        // Reforço de confiabilidade: Injeção direta no registro via WinPE (bypass.reg)
                        // Isso garante o bypass mesmo se o XML falhar em ser lido pelo Setup
                        string regContent = "Windows Registry Editor Version 5.00\r\n\r\n" +
                                          "[HKEY_LOCAL_MACHINE\\SYSTEM\\Setup\\LabConfig]\r\n" +
                                          "\"BypassTPMCheck\"=dword:00000001\r\n" +
                                          "\"BypassSecureBootCheck\"=dword:00000001\r\n" +
                                          "\"BypassRAMCheck\"=dword:00000001\r\n" +
                                          "\"BypassCPUCheck\"=dword:00000001\r\n" +
                                          "\"BypassStorageCheck\"=dword:00000001\r\n" +
                                          "\"BypassDiskCheck\"=dword:00000001\r\n" +
                                          "\"BypassNRO\"=dword:00000001\r\n";
                        File.WriteAllText(Path.Combine(winbootDrive, "bypass.reg"), regContent, Encoding.UTF8);
                        
                        // Script de auxílio para execução manual se precisarem shif+f10
                        string manualBypass = "@echo off\r\nregedit /s X:\\bypass.reg\r\nexit";
                        File.WriteAllText(Path.Combine(winbootDrive, "fix_tpm.bat"), manualBypass);
                    }

                    // 4. Atalho de Restauração na Área de Trabalho (via $OEM$)
                    if (autoCleanup)
                    {
                         try
                         {
                             // Estrutura: sources/$OEM$/$1/Users/Public/Desktop/
                             string oemPath = Path.Combine(winbootDrive, "sources", "$OEM$", "$1", "Users", "Public", "Desktop");
                             Directory.CreateDirectory(oemPath);
                             
                             string restoreBatContent = "@echo off\r\n" +
                                                       "echo ====================================================\r\n" +
                                                       "echo    RESTAURACAO DE ESPACO - KITLUGIA\r\n" +
                                                       "echo ====================================================\r\n" +
                                                       "echo.\r\n" +
                                                       "echo Este script irá remover a partição de instalação do Windows (8GB)\r\n" +
                                                       "echo e devolver o espaço para o seu Disco Local (C:).\r\n" +
                                                       "echo.\r\n" +
                                                       "pause\r\n" +
                                                       "echo Buscando unidade LugiaBoot...\r\n" +
                                                       "set TARGET_DRIVE=\r\n" +
                                                       "for %%i in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (\r\n" +
                                                       "  if exist \"%%i:\\_KitLugiaSetup\\first_logon.bat\" set TARGET_DRIVE=%%i\r\n" +
                                                       ")\r\n" +
                                                       "if \"%TARGET_DRIVE%\"==\"\" (\r\n" +
                                                       "  echo ERRO: Partição de instalação não encontrada!\r\n" +
                                                       "  pause\r\n" +
                                                       "  exit\r\n" +
                                                       ")\r\n" +
                                                       "echo Unidade encontrada: %TARGET_DRIVE%\r\n" +
                                                       "(echo select volume %TARGET_DRIVE%\r\n" +
                                                       " echo delete partition override\r\n" +
                                                       " echo select volume c\r\n" +
                                                       " echo extend\r\n" +
                                                       " echo exit) > %temp%\\dp_restore.txt\r\n" +
                                                       "diskpart /s %temp%\\dp_restore.txt\r\n" +
                                                       "echo.\r\n" +
                                                       "echo Sucesso! Espaço restaurado.\r\n" +
                                                       "pause\r\n" +
                                                       "del \"%~f0\""; // Deleta o próprio script após sucesso

                             File.WriteAllText(Path.Combine(oemPath, "Restaurar_Espaco_Lugia.bat"), restoreBatContent, Encoding.GetEncoding(850));
                             Log("Atalho de restauração criado em $OEM$ (Desktop Público).");
                         }
                         catch (Exception ex)
                         {
                             Log($"Aviso: Falha ao criar atalho OEM: {ex.Message}");
                         }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Log($"ERRO ao aplicar customizações: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<bool> PatchLinuxConfig(string driveLetter)
        {
            Log("Iniciando varredura e patch de configurações Linux (Turbo Boot)...");
            return await Task.Run(() =>
            {
                try
                {
                    int patchedCount = 0;
                    string drive = driveLetter.Replace(":", "");
                    
                    // 1. GRUB.CFG Patching
                    // Procura em locais comuns: /boot/grub/, /EFI/BOOT/, /EFI/ubuntu/, /
                    var grubFiles = Directory.GetFiles($"{drive}:\\", "grub.cfg", SearchOption.AllDirectories);
                    
                    foreach (var grub in grubFiles)
                    {
                        // Limpar atributo somente leitura se existir
                        File.SetAttributes(grub, FileAttributes.Normal);
                        
                        string content = File.ReadAllText(grub);
                        bool changed = false;

                        // Padrão 1: search --fs-uuid ... -> search --label KITLUGIA
                        // Isso faz o GRUB procurar pela etiqueta da partição em vez do UUID da ISO original
                        if (Regex.IsMatch(content, @"search\s+--no-floppy\s+--fs-uuid\s+--set=root\s+[a-fA-F0-9-]+"))
                        {
                            Log($"Patching UUID search in {grub}...");
                            content = Regex.Replace(content, @"search\s+--no-floppy\s+--fs-uuid\s+--set=root\s+[a-fA-F0-9-]+", 
                                $"search --no-floppy --set=root --label {WINBOOT_LABEL}");
                            changed = true;
                        }
                        else if (content.Contains("--fs-uuid"))
                        {
                             Log($"Patching generic UUID search in {grub}...");
                             content = Regex.Replace(content, @"--fs-uuid\s+[a-fA-F0-9-]{10,}", $"--label {WINBOOT_LABEL}");
                             changed = true;
                        }

                        // Padrão 2: cdrom-detect (Debian/Kali)
                        // Tenta forçar a montagem da nossa partição
                        if (content.Contains("cdrom-detect/try-usb=true")) 
                        {
                            // Já tem, não faz nada
                        }
                        else if (content.Contains("vmlinuz"))
                        {
                            // Adiciona parâmetros de boot USB amigáveis
                            Log($"Adicionando parâmetros USB-Live ao kernel em {grub}...");
                            content = content.Replace("quiet splash", $"quiet splash cdrom-detect/try-usb=true ignore_uuid root=LABEL={WINBOOT_LABEL}");
                            changed = true;
                        }

                        if (changed)
                        {
                            File.SetAttributes(grub, FileAttributes.Normal);
                            File.WriteAllText(grub, content);
                            patchedCount++;
                        }
                    }

                    // 2. ISOLINUX / SYSLINUX Patching
                    var syslinuxFiles = Directory.GetFiles($"{drive}:\\", "*.cfg", SearchOption.AllDirectories)
                                        .Where(f => f.EndsWith("isolinux.cfg") || f.EndsWith("syslinux.cfg"));
                    
                    foreach (var cfg in syslinuxFiles)
                    {
                        File.SetAttributes(cfg, FileAttributes.Normal);
                        string content = File.ReadAllText(cfg);
                        bool changed = false;

                        // Substitui label=... por label=KITLUGIA
                        if (Regex.IsMatch(content, @"root=live:CDLABEL=[^ ]+"))
                        {
                             Log($"Patching Live Label in {cfg}...");
                             content = Regex.Replace(content, @"root=live:CDLABEL=[^ ]+", $"root=live:LABEL={WINBOOT_LABEL}");
                             changed = true;
                        }

                        if (changed)
                        {
                            File.SetAttributes(cfg, FileAttributes.Normal);
                            File.WriteAllText(cfg, content);
                            patchedCount++;
                        }
                    }

                    Log($"Turbo Boot: {patchedCount} arquivos de configuração foram adaptados para USB.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"Erro no Patch Linux: {ex.Message}");
                    return false; // Não é fatal, o usuário ainda pode tentar o boot
                }
            });
        }

        /// <summary>
        /// Estratégia "Grub-First": Torna o GRUB do Linux o bootloader principal da partição,
        /// permitindo chainload do Windows Setup. Resolve o erro 0xc000007b definitivamente.
        /// </summary>
        public static async Task InstallGrubAsPrimary(string driveLetter)
        {
            Log("Iniciando estratégia 'Grub-First' (Inversão de Bootloader)...");
            await Task.Run(() =>
            {
                try
                {
                    string drive = driveLetter.Replace(":", "");
                    string bootDir = $"{drive}:\\EFI\\BOOT";
                    
                    if (!Directory.Exists(bootDir))
                    {
                        Log("Diretório EFI\\BOOT não encontrado. Cancelando inversão.");
                        return;
                    }

                    // 1. Identificar Linux Loaders disponíveis
                    Log("1. Identificando Linux Loaders disponíveis...");
                    string bootx64 = Path.Combine(bootDir, "BOOTX64.EFI"); 
                    string grubPath = Path.Combine(bootDir, "grubx64.efi");
                    
                    // Se não tiver grubx64.efi na raiz, procurar em subpastas de distros
                    if (!File.Exists(grubPath))
                    {
                        string[] possibleGrubs = { 
                            $"{drive}:\\EFI\\ubuntu\\grubx64.efi", 
                            $"{drive}:\\EFI\\debian\\grubx64.efi",
                            $"{drive}:\\EFI\\fedora\\grubx64.efi",
                            $"{drive}:\\boot\\grub\\x86_64-efi\\grub.efi"
                        };
                        var found = possibleGrubs.FirstOrDefault(File.Exists);
                        if (found != null) 
                        {
                            Log($"Grub encontrado em {found}. Copiando para EFI\\BOOT...");
                            File.Copy(found, grubPath, true);
                        }
                    }

                    // 2. Detectar se o BOOTX64.EFI atual é Microsoft (bootmgr)
                    // Bootmgr do Windows > 1.2MB; Shim do Linux < 1MB em geral
                    bool isMicrosoftBoot = false;
                    if (File.Exists(bootx64))
                    {
                        long size = new FileInfo(bootx64).Length;
                        if (size > 1200000) isMicrosoftBoot = true;
                    }

                    if (isMicrosoftBoot)
                    {
                        Log("2. Bootloader atual é Windows (Bootmgr). Realizando backup...");
                        string winBoot = Path.Combine(bootDir, "win_boot.efi");
                        if (!File.Exists(winBoot)) File.Move(bootx64, winBoot);
                        
                        // Precisa colocar Shim / Grub no lugar
                        string[] possibleShims = { 
                            $"{drive}:\\EFI\\ubuntu\\shimx64.efi", 
                            $"{drive}:\\EFI\\debian\\shimx64.efi",
                            $"{drive}:\\EFI\\fedora\\shimx64.efi"
                        };
                        var foundShim = possibleShims.FirstOrDefault(File.Exists);
                        if (foundShim != null)
                        {
                            File.Copy(foundShim, bootx64, true);
                            Log($"Shim Linux aplicado como Bootloader Principal ({foundShim}).");
                        }
                        else if (File.Exists(grubPath))
                        {
                            File.Copy(grubPath, bootx64, true);
                            Log("Grub usado diretamente como Bootloader Principal (sem Shim).");
                        }
                        else
                        {
                            Log("AVISO: Nenhum Shim/Grub encontrado. Revertendo backup...");
                            string winBoot2 = Path.Combine(bootDir, "win_boot.efi");
                            if (File.Exists(winBoot2)) File.Move(winBoot2, bootx64);
                            return;
                        }
                    }
                    else
                    {
                        Log("2. Bootloader já é Linux (Shim). Nenhum backup necessário.");
                    }

                    // 3. Configurar Menu GRUB para Chainload do Windows
                    Log("3. Configurando menu GRUB com entrada para Windows...");
                    string windowsMenuEntry = @"
# === KitLugia Grub-First: Windows Chainload ===
menuentry '🪟 Windows Setup / Boot Manager' --class windows {
    insmod chain
    if [ -f /EFI/BOOT/win_boot.efi ]; then
        chainloader /EFI/BOOT/win_boot.efi
    elif [ -f /EFI/Microsoft/Boot/bootmgfw.efi ]; then
        chainloader /EFI/Microsoft/Boot/bootmgfw.efi
    fi
}
";
                    // Procurar grub.cfg existente
                    string[] cfgPaths = { 
                        $"{drive}:\\boot\\grub\\grub.cfg", 
                        $"{drive}:\\EFI\\BOOT\\grub.cfg",
                        Path.Combine(bootDir, "grub.cfg")
                    };
                    
                    string targetCfg = cfgPaths.FirstOrDefault(File.Exists);
                    if (targetCfg != null)
                    {
                        string currentContent = File.ReadAllText(targetCfg);
                        if (!currentContent.Contains("KitLugia Grub-First"))
                        {
                            File.AppendAllText(targetCfg, "\n" + windowsMenuEntry);
                            Log($"Menu Windows adicionado ao {targetCfg}");
                        }
                        else
                        {
                            Log("Menu Windows já existe no grub.cfg. Pulando.");
                        }
                    }
                    else
                    {
                        // Criar grub.cfg mínimo
                        string newCfg = Path.Combine(bootDir, "grub.cfg");
                        File.WriteAllText(newCfg, windowsMenuEntry);
                        Log($"Criado grub.cfg mínimo em {newCfg}");
                    }

                    Log("Estratégia Grub-First aplicada com sucesso! Linux é agora o bootloader principal.");
                }
                catch (Exception ex)
                {
                    Log($"Erro no Grub-First: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Método DUAL BOOT e NVRAM Direto: Cria uma entrada no BCD e na NVRAM 
        /// apontando diretamente para o Bootloader UEFI (Linux/Grub) da partição.
        /// Sem WinPE (Sem tela azul que fecha). O PC vai direto para o Linux.
        /// </summary>
        public static async Task<string?> CreateDirectNvramBoot(string winbootDrive, string linuxDescription)
        {
            Log("Criando Entrada de Boot EFI Direta (NVRAM/BCD)...");
            
            string drive = winbootDrive.Replace(":", "");
            string bootDir = $"{drive}:\\EFI\\BOOT";
            
            // 1. Encontrar o bootloader principal do Linux
            string[] possibleLoaders = { 
                $"{drive}:\\EFI\\BOOT\\BOOTX64.EFI",
                $"{drive}:\\EFI\\BOOT\\grubx64.efi",
                $"{drive}:\\EFI\\ubuntu\\shimx64.efi",
                $"{drive}:\\EFI\\ubuntu\\grubx64.efi"
            };
            
            string? targetEfi = possibleLoaders.FirstOrDefault(File.Exists);
            if (targetEfi == null)
            {
                Log("ERRO: Nenhum Bootloader EFI encontrado na imagem Linux!");
                return null;
            }

            // Pega apenas o caminho relativo para o BCD (ex: \EFI\BOOT\BOOTX64.EFI)
            string relativePath = targetEfi.Substring(2);
            Log($"Bootloader EFI detectado: {relativePath}");

            // 2. Criar entrada copiando o bootmgr atual
            string cleanDesc = SanitizeDescription(linuxDescription);
            string bridgeDescription = $"Linux ({cleanDesc})";
            
            Log("Clonando BCD mgr...");
            var (copyExit, copyOut) = await RunProcessCaptured("bcdedit.exe", $"/copy {{bootmgr}} /d \"{bridgeDescription}\"");
            
            var match = Regex.Match(copyOut, @"{[a-fA-F0-9-]+}");
            string guid = match.Success ? match.Value : "";
            
            if (string.IsNullOrEmpty(guid))
            {
                Log("ERRO: Falha ao clonar BCD.");
                return null;
            }

            Log($"Entrada BCD criada: {guid}");

            // 3. Apontar o Device para a nossa partição Linux
            // Como é um aplicativo UEFI, apontamos o device e o path dele.
            await RunProcessCaptured("bcdedit.exe", $"/set {guid} device partition={drive}:");
            await RunProcessCaptured("bcdedit.exe", $"/set {guid} path {relativePath}");

            // 4. Inserir no Menu do Windows (Tela Azul normal) como fallback
            await RunProcessCaptured("bcdedit.exe", $"/displayorder {guid} /addlast");

            // 5. MÁGICA: Injetar na NVRAM da Placa Mãe (Bootsequence / One-time boot)
            // Isso faz o PC dar boot no Linux DIRETAMENTE e automaticamente na próxima reinicialização!
            Log("Injetando ordem na BIOS / NVRAM (Bootsequence direto)...");
            var (fwExit, fwOut) = await RunProcessCaptured("bcdedit.exe", $"/set {{fwbootmgr}} bootsequence {guid}");
            
            if (fwExit == 0)
                Log($"SUCESSO: O Computador iniciará o Linux automaticamente pelo Firmware!");
            else
                Log($"Aviso: A placa-mãe não suporta fwbootmgr dinâmico. Você poderá escolher no submenu do Windows. Erro: {fwOut}");

            return guid;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string target = Path.Combine(targetDir, Path.GetFileName(file));
                try { File.Copy(file, target, true); } catch { }
            }
            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string target = Path.Combine(targetDir, Path.GetFileName(directory));
                CopyDirectory(directory, target);
            }
        }


        public class BcdEntry
        {
            public string Guid { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public bool IsCritical { get; set; } = false;
        }

        public static async Task<List<BcdEntry>> ScanBcdEntriesAsync()
        {
            Log("Escaneando entradas do menu de Boot (KitLugia & Linux)...");
            var entries = new List<BcdEntry>();

            return await Task.Run(() =>
            {
                try
                {
                    var (enumCode, enumOutput) = RunProcessCaptured("bcdedit.exe", "/enum all /v").GetAwaiter().GetResult();

                    if (enumCode != 0)
                    {
                        Log($"FALHA BCDEDIT: {enumOutput}");
                        return entries;
                    }

                    // 🔥 ESTRATÉGIA 1: Busca por descrições conhecidas do KitLugia
                    string[] descriptionPatterns = {
                        @"(description|descriç[ãa]o|descricao|beschreibung|descripción|description)\s+(KitLugia|Generic|Linux|Sergei|Winboot|Multi-ISO)",
                        @"(description|descriç[ãa]o|descricao|beschreibung|descripción|description)\s+.*\b(KITLUGIA|LUGIA)\b",
                        @"(description|descriç[ãa]o|descricao|beschreibung|descripción|description)\s+.*\b(WINBOOT)\b"
                    };

                    // 🔥 ESTRATÉGIA 2: Busca por device que aponta para partição Winboot
                    var winbootPartitions = GetDisks(false, false).SelectMany(d => d.Partitions)
                        .Where(p => p.Label.Contains("KITLUGIA", StringComparison.OrdinalIgnoreCase) ||
                                   p.Label.Contains("Winboot", StringComparison.OrdinalIgnoreCase))
                        .Select(p => p.DriveLetter.Replace(":", ""))
                        .ToList();

                    string[] blocks = enumOutput.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (string block in blocks)
                    {
                        string? guid = null;
                        var guidMatch = Regex.Match(block, @"(identifier|identificador)\s+({[a-fA-F0-9-]+})", RegexOptions.IgnoreCase);
                        if (guidMatch.Success)
                        {
                            guid = guidMatch.Groups[2].Value;
                        }

                        // Segurança Absoluta (marcar OS base como crítico)
                        bool isCritical = false;
                        if (guid != null && (guid.Equals("{bootmgr}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{current}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{default}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{fwbootmgr}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{memdiag}", StringComparison.OrdinalIgnoreCase)))
                        {
                            isCritical = true;
                        }

                        // Extrai descrição
                        string description = "Sem descrição";
                        var descMatch = Regex.Match(block, @"(description|descriç[ãa]o|descricao)\s+(.+)", RegexOptions.IgnoreCase);
                        if (descMatch.Success)
                        {
                            description = descMatch.Groups[2].Value.Trim();
                        }

                        // Extrai tipo de aplicação
                        string type = "Desconhecido";
                        var appMatch = Regex.Match(block, @"application\s+(\w+)", RegexOptions.IgnoreCase);
                        if (appMatch.Success)
                        {
                            type = appMatch.Groups[1].Value;
                        }

                        bool shouldInclude = false;
                        string? reason = null;

                        // Estratégia 1: Busca por descrições
                        foreach (var pattern in descriptionPatterns)
                        {
                            if (Regex.IsMatch(block, pattern, RegexOptions.IgnoreCase))
                            {
                                shouldInclude = true;
                                reason = "Descrição KitLugia/Linux";
                                break;
                            }
                        }

                        // Estratégia 2: Busca por device que aponta para partição Winboot
                        if (!shouldInclude && winbootPartitions.Count > 0)
                        {
                            var deviceMatch = Regex.Match(block, @"(device|dispositivo)\s+partition=([A-Z]:)", RegexOptions.IgnoreCase);
                            if (deviceMatch.Success)
                            {
                                string driveLetter = deviceMatch.Groups[2].Value;
                                if (winbootPartitions.Contains(driveLetter.Replace(":", "")))
                                {
                                    shouldInclude = true;
                                    reason = $"Aponta para partição Winboot ({driveLetter})";
                                }
                            }
                        }

                        // Estratégia 3: Busca por ramdisksdidevice (entradas WIM)
                        if (!shouldInclude && winbootPartitions.Count > 0)
                        {
                            var ramdiskMatch = Regex.Match(block, @"ramdisksdidevice\s+partition=([A-Z]:)", RegexOptions.IgnoreCase);
                            if (ramdiskMatch.Success)
                            {
                                string driveLetter = ramdiskMatch.Groups[1].Value;
                                if (winbootPartitions.Contains(driveLetter.Replace(":", "")))
                                {
                                    shouldInclude = true;
                                    reason = $"Ramdisk aponta para Winboot ({driveLetter})";
                                }
                            }
                        }

                        // Estratégia 4: Busca por application bootsector (Legacy)
                        if (!shouldInclude)
                        {
                            var appMatch2 = Regex.Match(block, @"application\s+bootsector", RegexOptions.IgnoreCase);
                            if (appMatch2.Success)
                            {
                                var deviceMatch = Regex.Match(block, @"(device|dispositivo)\s+partition=([A-Z]:)", RegexOptions.IgnoreCase);
                                if (deviceMatch.Success)
                                {
                                    string driveLetter = deviceMatch.Groups[2].Value;
                                    if (winbootPartitions.Contains(driveLetter.Replace(":", "")))
                                    {
                                        shouldInclude = true;
                                        reason = $"Bootsector aponta para Winboot ({driveLetter})";
                                    }
                                }
                            }
                        }

                        // Incluir se encontrou pelo menos uma estratégia OU se for crítico (para mostrar ao usuário)
                        if ((shouldInclude && guid != null) || isCritical)
                        {
                            entries.Add(new BcdEntry
                            {
                                Guid = guid ?? "",
                                Description = description,
                                Reason = reason ?? (isCritical ? "Entrada crítica do sistema" : ""),
                                Type = type,
                                IsCritical = isCritical
                            });
                        }
                    }

                    Log($"Escaneamento BCD concluído. {entries.Count} entradas encontradas.");
                    return entries;
                }
                catch (Exception ex)
                {
                    Log($"Erro ao escanear BCD: {ex.Message}");
                    return entries;
                }
            });
        }

        public static async Task<bool> CleanBcdEntriesAsync(List<string>? guidsToDelete = null)
        {
            if (guidsToDelete == null || guidsToDelete.Count == 0)
            {
                Log("Nenhuma entrada para remover.");
                return true;
            }

            Log($"Limpando {guidsToDelete.Count} entradas do menu de Boot...");
            return await Task.Run(async () =>
            {
                try
                {
                    foreach (string guid in guidsToDelete)
                    {
                        // Não deletar entradas críticas do sistema
                        if (guid.Equals("{bootmgr}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{current}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{default}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{fwbootmgr}", StringComparison.OrdinalIgnoreCase) ||
                            guid.Equals("{memdiag}", StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"⚠️ Pulando entrada crítica: {guid}");
                            continue;
                        }

                        Log($"Removendo entrada BCD: {guid}");
                        await RunProcessCaptured("bcdedit.exe", $"/delete {guid} /f");
                    }

                    // Limpa também o bootsequence se houver algo travado lá
                    await RunProcessCaptured("bcdedit.exe", "/set {fwbootmgr} displayorder {bootmgr} /addfirst");
                    await RunProcessCaptured("bcdedit.exe", "/deletevalue {fwbootmgr} bootsequence");

                    // Limpa também o displayorder do bootmgr para remover referências fantasma
                    await RunProcessCaptured("bcdedit.exe", "/deletevalue {bootmgr} displayorder");

                    Log($"Limpeza BCD concluída. {guidsToDelete.Count} entradas removidas.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"Erro ao limpar BCD: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<List<BcdEntry>> ScanWinbootForCleanup()
        {
            Log("Escaneando Winboot para limpeza...");
            return await ScanBcdEntriesAsync();
        }

        public static async Task<bool> RemoveWinboot(PartitionInfo? specificTarget = null, bool safeMode = false, List<string>? customGuids = null)
        {
            Log(customGuids != null ? $"Iniciando remoção do Winboot ({customGuids.Count} GUIDs customizados)..." : "Iniciando remoção do Winboot...");
            return await Task.Run(async () =>
            {
                // Tenta iniciar VDS (Safe Mode Fix)
                try {
                    await RunProcessCaptured("sc", "config vds start= demand");
                    await RunProcessCaptured("net", "start vds");
                } catch { }

                try
                {
                    // 1. Remover entradas do BCD
                    if (customGuids != null)
                    {
                        // Remove GUIDs customizados (selecionados pelo usuário)
                        await CleanBcdEntriesAsync(customGuids);
                    }
                    else
                    {
                        // Modo automático: remove tudo
                        await CleanBcdEntriesAsync();
                    }

                    // 2. Destruir Partição Alvo
                    StringBuilder dpScript = new StringBuilder();
                    
                    if (specificTarget != null)
                    {
                        // 🔥 SEGURANÇA CRÍTICA: Verificar se não é partição do sistema
                        var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.Replace(":", "");
                        if (specificTarget.DriveLetter.Replace(":", "").Equals(systemDrive, StringComparison.OrdinalIgnoreCase))
                        {
                            Log($"❌ ERRO CRÍTICO: Tentando deletar partição do sistema {specificTarget.DriveLetter}. Operação abortada.");
                            return false;
                        }
                        
                        // Remoção Direta via Seleção do Usuário
                         Log($"Removendo ALVO SELECIONADO: Volume {specificTarget.DriveLetter} ({specificTarget.Label})...");
                         // Tenta pegar o numero do volume usando diskpart filter (mais seguro que confiar no index antigo)
                         dpScript.AppendLine($"select volume {specificTarget.DriveLetter.Replace(":", "")}");
                         dpScript.AppendLine("delete partition override");
                    }
                    else
                    {
                         // Modo Varredura (Legacy / Auto)
                        Log("Escaneando volumes para limpeza automática...");
                        string listScript = "list volume\nexit";
                        string listPath = Path.Combine(Path.GetTempPath(), "list_vol_cleanup.txt");
                        File.WriteAllText(listPath, listScript);
                        var (listCode, listOutput) = await RunProcessCaptured("diskpart.exe", $"/s \"{listPath}\"");
                        File.Delete(listPath);

                        string volPattern = @"Volume\s+(\d+)\s+([A-Z])?\s+(Winboot|LUGIA_BOOT|NAO_DELETAR)";
                        var volMatches = Regex.Matches(listOutput, volPattern, RegexOptions.IgnoreCase);

                        if (volMatches.Count == 0)
                        {
                            Log("Nenhuma partição Winboot encontrada para remoção automática.");
                        }

                        foreach (Match m in volMatches)
                        {
                            string volNum = m.Groups[1].Value;
                            string volLetter = m.Groups[2].Value;
                            
                            // 🔥 SEGURANÇA CRÍTICA: Não deletar volume do sistema
                            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.Replace(":", "");
                            if (!string.IsNullOrEmpty(volLetter) && volLetter.Equals(systemDrive, StringComparison.OrdinalIgnoreCase))
                            {
                                Log($"❌ ERRO CRÍTICO: Volume {volNum} ({volLetter}) parece ser o volume do sistema. Pulando.");
                                continue;
                            }
                            
                            Log($"Agendando remoção do Volume {volNum}...");
                            dpScript.AppendLine($"select volume {volNum}");
                            dpScript.AppendLine("delete partition override");
                        }
                    }


                    // 3. Tentar estender a unidade principal (C: ou a primeira com letra)
                    var disks = GetDisks(false, safeMode);
                    string? sourceLetter = null;
                    foreach(var d in disks)
                    {
                        // Filter out partitions that should not be considered for extension
                        var filteredPartitions = d.Partitions.Where(p =>
                            p.Size >= 3000000000 && // Skip partitions smaller than 3GB (e.g., MSR/EFI)
                            !p.Name.Contains("Reserved", StringComparison.OrdinalIgnoreCase) &&
                            !p.Label.Equals(WINBOOT_LABEL, StringComparison.OrdinalIgnoreCase) &&
                            !p.Label.Equals("Winboot", StringComparison.OrdinalIgnoreCase)
                        ).ToList();

                        var cPart = filteredPartitions.FirstOrDefault(p => p.DriveLetter.Equals("C:", StringComparison.OrdinalIgnoreCase));
                        if (cPart != null) { sourceLetter = "C"; break; }
                        sourceLetter = filteredPartitions.FirstOrDefault(p => !string.IsNullOrEmpty(p.DriveLetter))?.DriveLetter.Replace(":", "");
                        if (sourceLetter != null) break;
                    }

                    if (!string.IsNullOrEmpty(sourceLetter))
                    {
                        Log($"Estendendo unidade principal: {sourceLetter}");
                        dpScript.AppendLine($"select volume {sourceLetter}");
                        dpScript.AppendLine("extend");
                    }
                    dpScript.AppendLine("exit");

                    if (dpScript.Length > 10) // "exit" + newline is 6
                    {
                        string scriptPath = Path.Combine(Path.GetTempPath(), "cleanup_winboot_dp.txt");
                        File.WriteAllText(scriptPath, dpScript.ToString());
                        var (dpCode, dpOutput) = await RunProcessCaptured("diskpart.exe", $"/s \"{scriptPath}\"");
                        Log(dpOutput);
                        File.Delete(scriptPath);
                    }

                    Log("Processo de limpeza concluído.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"ERRO na remoção: {ex.Message}");
                    return false;
                }
            });
        }

        public static async Task<bool> CreateBootPartition(string sourceDriveLetter, int sizeMb, string label, bool multiIso = false, bool safeMode = false)
        {
            Log($"Iniciando criação de partição no disco de origem {sourceDriveLetter} (Multi-ISO: {multiIso})...");
            return await Task.Run(async () =>
            {
                // 🔥 SEGURANÇA CRÍTICA: Verificar se não está criando no disco do sistema
                var sysDrive = Path.GetPathRoot(Environment.SystemDirectory)?.Replace(":", "");
                if (sourceDriveLetter.Replace(":", "").Equals(sysDrive, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"❌ ERRO CRÍTICO: Tentando criar partição Winboot na partição do sistema {sourceDriveLetter}.");
                    Log("❌ Isso pode causar problemas de boot e instabilidade.");
                    Log("❌ Use uma partição de dados (D:, E:, etc) para criar o Winboot.");
                    return false;
                }
                
                // 0. VDS (Safe Mode Fix)
                try 
                {
                    await RunProcessCaptured("sc", "config vds start= demand");
                    await RunProcessCaptured("net", "start vds");
                }
                catch { }

                // 1. AUTO-CLEANUP: Detectar e remover Winboot existente (evita boot duplicado)
                Log("Verificando se já existe uma partição Winboot anterior...");
                var existingPartitions = GetRemovablePartitions();
                if (existingPartitions.Any())
                {
                    Log($"Encontrada(s) {existingPartitions.Count} partição(ões) Winboot existente(s).");
                    
                    // 🔥 SEGURANÇA: Verificar se as partições são realmente Winboot antes de deletar
                    var validWinbootPartitions = existingPartitions.Where(p =>
                        !string.IsNullOrEmpty(p.Label) && (
                            p.Label.Contains("KITLUGIA", StringComparison.OrdinalIgnoreCase) ||
                            p.Label.Contains("Winboot", StringComparison.OrdinalIgnoreCase) ||
                            p.Label.Contains("Multi-ISO", StringComparison.OrdinalIgnoreCase) ||
                            p.Label.Contains("PE", StringComparison.OrdinalIgnoreCase)
                        )
                    ).ToList();
                    
                    if (validWinbootPartitions.Count != existingPartitions.Count)
                    {
                        Log($"⚠️ AVISO: {existingPartitions.Count - validWinbootPartitions.Count} partição(ões) não parecem ser Winboot e NÃO serão deletadas.");
                        Log("⚠️ Somente partições com labels contendo 'KITLUGIA', 'Winboot', 'Multi-ISO' ou 'PE' serão removidas.");
                    }
                    
                    if (validWinbootPartitions.Any())
                    {
                        Log($"Removendo {validWinbootPartitions.Count} partição(ões) Winboot legítima(s) antes de criar nova...");
                        
                        // Limpa BCD primeiro
                        var (enumCode, enumOutput) = await RunProcessCaptured("bcdedit.exe", "/enum all");
                        string bcdPattern = @"(identifier|identificador)\s+({[a-fA-F0-9-]+})[\s\S]*?description\s+(KitLugia Winboot Setup|Sergei Strelec PE|Generic Multi-ISO / Linux)";
                        var bcdMatches = Regex.Matches(enumOutput, bcdPattern, RegexOptions.IgnoreCase);
                        foreach (Match m in bcdMatches)
                        {
                            string guid = m.Groups[2].Value;
                            Log($"Removendo entrada BCD antiga: {guid}");
                            await RunProcessCaptured("bcdedit.exe", $"/delete {guid} /f");
                        }

                        // Deleta cada partição antiga e estende o volume de origem
                        foreach (var oldPart in validWinbootPartitions)
                        {
                            string letter = oldPart.DriveLetter.Replace(":", "");
                            if (string.IsNullOrEmpty(letter)) continue;
                            
                            // 🔥 SEGURANÇA: Verificação adicional - não deletar partição do sistema
                            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.Replace(":", "");
                            if (letter.Equals(systemDrive, StringComparison.OrdinalIgnoreCase))
                            {
                                Log($"❌ ERRO CRÍTICO: Tentando deletar partição do sistema {letter}:. Operação abortada.");
                                continue;
                            }
                            
                            Log($"Deletando partição antiga: {letter}: ({oldPart.Label})");
                            StringBuilder cleanScript = new StringBuilder();
                            cleanScript.AppendLine($"select volume {letter}");
                            cleanScript.AppendLine("delete partition override");
                            cleanScript.AppendLine($"select volume {sourceDriveLetter}");
                            cleanScript.AppendLine("extend");
                            cleanScript.AppendLine("exit");

                            string cleanPath = Path.Combine(Path.GetTempPath(), "winboot_cleanup_dp.txt");
                            File.WriteAllText(cleanPath, cleanScript.ToString());
                            var (cleanExit, cleanOut) = await RunProcessCaptured("diskpart.exe", $"/s \"{cleanPath}\"");
                            Log(cleanOut);
                            File.Delete(cleanPath);
                        }
                        Log("Limpeza de Winboot anterior concluída. Espaço restaurado.");
                    }
                    else
                    {
                        Log("⚠️ Nenhuma partição Winboot legítima encontrada para deletar. Continuando...");
                    }
                }

                // 2. DETECÇÃO MBR/GPT ROBUSTA via PowerShell
                bool isGpt = false;
                try
                {
                    // Descobre o PartitionStyle do disco de origem (Remove : se houver)
                    string cleanLetter = sourceDriveLetter.Replace(":", "");
                    var (psExit, psOutput) = await RunProcessCaptured("powershell.exe", 
                        $"-Command \"Get-Disk -Number ((Get-Partition -DriveLetter '{cleanLetter}').DiskNumber) | Select-Object -ExpandProperty PartitionStyle\"");
                    
                    string style = psOutput.Trim();
                    Log($"PowerShell Disk Style: {style}");
                    if (style.Equals("GPT", StringComparison.OrdinalIgnoreCase))
                    {
                        isGpt = true;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Aviso na detecção PS: {ex.Message}. Usando fallback WMI.");
                    // Fallback WMI
                    try {
                        string wimId = sourceDriveLetter.EndsWith(":") ? sourceDriveLetter : sourceDriveLetter + ":";
                        using (var searcher = new ManagementObjectSearcher(
                            $"ASSOCIATORS OF {{Win32_LogicalDisk.DeviceID='{wimId}'}} WHERE AssocClass=Win32_LogicalDiskToPartition"))
                        {
                            foreach (ManagementObject partition in searcher.Get())
                            {
                                using (partition) // 🔥 LIMPEZA: Dispose do objeto WMI
                                {
                                    string partType = partition["Type"]?.ToString() ?? "";
                                    if (partType.Contains("GPT", StringComparison.OrdinalIgnoreCase)) isGpt = true;
                                    break;
                                }
                            }
                        }
                    } catch { }
                }
                Log($"Tipo de partição consolidado: {(isGpt ? "GPT (UEFI)" : "MBR (Legacy BIOS)")}");

                // 3. CRIAR PARTIÇÃO (Script Resiliente)
                bool isSystemEfi = IsEfiMode();
                StringBuilder script = new StringBuilder();
                script.AppendLine("rescan");
                script.AppendLine($"select volume {sourceDriveLetter}");
                script.AppendLine($"shrink desired={sizeMb} minimum={sizeMb}");
                script.AppendLine("create partition primary");
                
                string fs = multiIso ? "fat32" : "ntfs";
                script.AppendLine($"format quick fs={fs} label=\"{WINBOOT_LABEL}\"");
                
                // CRÍTICO: 'assign' ANTES de 'active' para garantir letra mesmo se o firmware reclamar
                script.AppendLine("assign"); 

                // MBR 'active' SAFETY:
                // SÓ aplicamos 'active' se o disco for REMOVÍVEL (Pendrive).
                // NUNCA aplicamos em discos fixos (SSD/HDD) para não sequestrar o boot do host.
                bool isRemovable = false;
                try {
                    var disks = GetDisks(false, safeMode);
                    var targetDisk = disks.FirstOrDefault(d => d.Partitions.Any(p => p.DriveLetter.Equals(sourceDriveLetter, StringComparison.OrdinalIgnoreCase)));
                    if (targetDisk != null && (targetDisk.Interface.Contains("USB", StringComparison.OrdinalIgnoreCase) || targetDisk.Interface.Contains("Removable", StringComparison.OrdinalIgnoreCase))) {
                        isRemovable = true;
                    }
                } catch { }

                if (!isGpt && !isSystemEfi && isRemovable)
                {
                    Log("Disco MBR e REMOVÍVEL Detectado: Aplicando 'active' na partição.");
                    script.AppendLine("active"); 
                }
                else
                {
                    Log("Segurança MBR: Pulando 'active' para disco fixo ou sistema UEFI.");
                }

                script.AppendLine("exit");

                string scriptPath = Path.Combine(Path.GetTempPath(), "winboot_create_dp.txt");
                File.WriteAllText(scriptPath, script.ToString());

                Log("Executando Script Diskpart (Etapa 1: Criação e Formatação)...");
                var (exitCode, output) = await RunProcessCaptured("diskpart.exe", $"/s \"{scriptPath}\"");
                Log("--- DISKPART OUTPUT ---");
                Log(output);
                File.Delete(scriptPath);

                // 4. VERIFICAÇÃO E CORREÇÃO DE LETRA (Crítico)
                // 🔥 NÃO DEPENDE DE STRINGS DE TEXTO - Usa WMI para verificar se a partição tem letra
                // Isso funciona em qualquer idioma do Windows/ISO
                bool hasLetter = false;
                try
                {
                    await Task.Delay(1000); // Aguarda diskpart terminar
                    var disksCheck = GetDisks(false, safeMode);
                    var targetPartition = disksCheck.SelectMany(d => d.Partitions)
                                                  .FirstOrDefault(p => p.Label.Equals(WINBOOT_LABEL, StringComparison.OrdinalIgnoreCase));
                    hasLetter = targetPartition != null && !string.IsNullOrEmpty(targetPartition.DriveLetter);
                }
                catch { }

                if (!hasLetter)
                {
                    Log("Aviso: Diskpart não confirmou atribuição de letra. Tentando atribuição forçada...");
                    // Procura a partição sem letra com o label KITLUGIA
                    StringBuilder fixScript = new StringBuilder();
                    fixScript.AppendLine("rescan");
                    fixScript.AppendLine("list volume");
                    fixScript.AppendLine("exit");
                    
                    var (listCode, listOut) = await RunProcessCaptured("diskpart.exe", "/s " + scriptPath); // Reusa o path mas com novo conteúdo
                    File.WriteAllText(scriptPath, fixScript.ToString());
                    (listCode, listOut) = await RunProcessCaptured("diskpart.exe", $"/s \"{scriptPath}\"");
                    
                    // Tenta achar o volume pelo label no output do list volume
                    var match = Regex.Match(listOut, @"Volume\s+(\d+)\s+\w\s+" + WINBOOT_LABEL, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string volNum = match.Groups[1].Value;
                        Log($"Volume {WINBOOT_LABEL} encontrado como {volNum}. Forçando atribuição...");
                        File.WriteAllText(scriptPath, $"select volume {volNum}\nassign\nexit");
                        await RunProcessCaptured("diskpart.exe", $"/s \"{scriptPath}\"");
                    }
                    File.Delete(scriptPath);
                }

                // Verificamos se agora temos uma partição com a letra
                await Task.Delay(2000);
                var disksAfter = GetDisks(false, safeMode);
                var createdPart = disksAfter.SelectMany(d => d.Partitions)
                                            .FirstOrDefault(p => p.Label.Equals(WINBOOT_LABEL, StringComparison.OrdinalIgnoreCase));

                if (createdPart == null || string.IsNullOrEmpty(createdPart.DriveLetter))
                {
                    Log("ERRO FATAL: A partição foi criada mas ainda não possui letra de unidade.");
                    return false;
                }

                Log($"Partição Winboot pronta em {createdPart.DriveLetter}");
                return true;
            });
        }




        private static string GenerateAutounattend(bool bypassRequirements, bool localAccount, bool disablePrivacy, bool inject, bool cleanup, string? user, string? pass, bool fullAuto, uint targetDisk = 0, uint targetPartition = 0)
        {
            // Sanitize Username to avoid XML errors or Windows Setup failures
            if (!string.IsNullOrEmpty(user))
            {
                // Remove invalid chars for Windows Usernames: " / \ [ ] : ; | = , + * ? < > @
                user = Regex.Replace(user, @"[\\""/\[\]:;|=,\+\*\?<>\@]", "");
                if (string.IsNullOrWhiteSpace(user)) user = "LugiaUser"; // Fallback
            }

            // 🔥 USA en-US COMO PADRÃO UNIVERSAL (WinPE em inglês funciona em qualquer idioma)
            // Isso garante compatibilidade total com comandos e scripts que esperam output em inglês
            const string systemLocale = "en-US";
            const string inputLocale = "0409"; // en-US keyboard layout

            StringBuilder xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            xml.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

            // --- PASS 1: windowsPE (Setup Environment) ---
            xml.AppendLine("  <settings pass=\"windowsPE\">");

            // 1.1 International (WinPE)
            xml.AppendLine("    <component name=\"Microsoft-Windows-International-Core-WinPE\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonVisual\" xmlns:wcm=\"http://schemas.microsoft.com/WCM/2002/Xml\">");
            xml.AppendLine("      <SetupUILanguage>");
            xml.AppendLine($"        <UILanguage>{systemLocale}</UILanguage>");
            xml.AppendLine("      </SetupUILanguage>");
            xml.AppendLine($"      <InputLocale>{inputLocale}:0000{inputLocale}</InputLocale>");
            xml.AppendLine($"      <SystemLocale>{systemLocale}</SystemLocale>");
            xml.AppendLine($"      <UILanguage>{systemLocale}</UILanguage>");
            xml.AppendLine($"      <UserLocale>{systemLocale}</UserLocale>");
            xml.AppendLine("    </component>");

            // 1.2 Setup Configuration & Bypasses
            xml.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonVisual\" xmlns:wcm=\"http://schemas.microsoft.com/WCM/2002/Xml\">");
            
            if (fullAuto)
            {
                // Disk Configuration
                xml.AppendLine("      <DiskConfiguration>");
                xml.AppendLine("        <Disk wcm:action=\"add\">");
                xml.AppendLine($"          <DiskID>{targetDisk}</DiskID>");
                xml.AppendLine("          <WillWipeDisk>false</WillWipeDisk>");
                xml.AppendLine("          <ModifyPartitions>");
                xml.AppendLine("            <ModifyPartition wcm:action=\"add\">");
                xml.AppendLine("              <Order>1</Order>");
                xml.AppendLine($"              <PartitionID>{targetPartition + 1}</PartitionID>");
                xml.AppendLine("              <Format>NTFS</Format>");
                xml.AppendLine("              <Label>Windows</Label>");
                xml.AppendLine("            </ModifyPartition>");
                xml.AppendLine("          </ModifyPartitions>");
                xml.AppendLine("        </Disk>");
                xml.AppendLine("        <WillShowUI>OnError</WillShowUI>");
                xml.AppendLine("      </DiskConfiguration>");
                
                xml.AppendLine("      <ImageInstall>");
                xml.AppendLine("        <OSImage>");
                xml.AppendLine("          <InstallTo>");
                xml.AppendLine($"            <DiskID>{targetDisk}</DiskID>");
                xml.AppendLine($"            <PartitionID>{targetPartition + 1}</PartitionID>");
                xml.AppendLine("          </InstallTo>");
                xml.AppendLine("          <WillShowUI>OnError</WillShowUI>");
                xml.AppendLine("        </OSImage>");
                xml.AppendLine("      </ImageInstall>");

                xml.AppendLine("      <UserData>");
                xml.AppendLine("        <AcceptEula>true</AcceptEula>");
                xml.AppendLine("        <FullName>Lugia User</FullName>");
                xml.AppendLine("        <Organization>KitLugia</Organization>");
                xml.AppendLine("        <ProductKey>");
                xml.AppendLine("          <Key>VK7JG-NPHTM-C97JM-9MPGT-3V66T</Key>");
                xml.AppendLine("          <WillShowUI>OnError</WillShowUI>");
                xml.AppendLine("        </ProductKey>");
                xml.AppendLine("      </UserData>");
            }

            // BYPASS REQUIREMENTS (Must be here in windowsPE)
            if (bypassRequirements)
            {
                xml.AppendLine("      <RunSynchronous>");
                xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                xml.AppendLine("          <Order>1</Order>");
                xml.AppendLine("          <Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f</Path>");
                xml.AppendLine("        </RunSynchronousCommand>");
                xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                xml.AppendLine("          <Order>2</Order>");
                xml.AppendLine("          <Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f</Path>");
                xml.AppendLine("        </RunSynchronousCommand>");
                xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                xml.AppendLine("          <Order>3</Order>");
                xml.AppendLine("          <Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassRAMCheck /t REG_DWORD /d 1 /f</Path>");
                xml.AppendLine("        </RunSynchronousCommand>");
                xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                xml.AppendLine("          <Order>4</Order>");
                xml.AppendLine("          <Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassStorageCheck /t REG_DWORD /d 1 /f</Path>");
                xml.AppendLine("        </RunSynchronousCommand>");
                xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                xml.AppendLine("          <Order>5</Order>");
                xml.AppendLine("          <Path>reg add HKLM\\SYSTEM\\Setup\\LabConfig /v BypassCPUCheck /t REG_DWORD /d 1 /f</Path>");
                xml.AppendLine("        </RunSynchronousCommand>");
                xml.AppendLine("      </RunSynchronous>");
            }
            
            xml.AppendLine("    </component>");
            xml.AppendLine("  </settings>");

            // --- PASS 4: specialize (System Config) ---
            if (localAccount || disablePrivacy || bypassRequirements)
            {
                xml.AppendLine("  <settings pass=\"specialize\">");
                xml.AppendLine("    <component name=\"Microsoft-Windows-Deployment\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonVisual\" xmlns:wcm=\"http://schemas.microsoft.com/WCM/2002/Xml\">");
                xml.AppendLine("      <RunSynchronous>");
                int order = 1;
                // Crítico: BypassNRO para permitir instalação sem internet (OOBE)
                xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                xml.AppendLine($"          <Order>{order++}</Order>");
                xml.AppendLine("          <Path>reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path>");
                xml.AppendLine("        </RunSynchronousCommand>");
                if (disablePrivacy)
                {
                     xml.AppendLine("        <RunSynchronousCommand wcm:action=\"add\">");
                     xml.AppendLine($"          <Order>{order++}</Order>");
                     xml.AppendLine("          <Path>reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OOBE\" /v DisablePrivacyExperience /t REG_DWORD /d 1 /f</Path>");
                     xml.AppendLine("        </RunSynchronousCommand>");
                }
                xml.AppendLine("      </RunSynchronous>");
                xml.AppendLine("    </component>");
                xml.AppendLine("  </settings>");
            }

            // --- PASS 7: oobeSystem (User Config) ---
            xml.AppendLine("  <settings pass=\"oobeSystem\">");
            xml.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonVisual\" xmlns:wcm=\"http://schemas.microsoft.com/WCM/2002/Xml\">");
            xml.AppendLine("      <OOBE>");
            if (disablePrivacy)
            {
                xml.AppendLine("        <HideEULAPage>true</HideEULAPage>");
                xml.AppendLine("        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>");
                xml.AppendLine("        <ProtectYourPC>3</ProtectYourPC>");
            }
            // Controlled by 'Local Account' checkbox (or privacy as fallback)
            if (localAccount || disablePrivacy)
            {
                xml.AppendLine("        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
            }
            xml.AppendLine("      </OOBE>");

            // Account Creation
            if (!string.IsNullOrEmpty(user))
            {
                xml.AppendLine("      <UserAccounts>");
                xml.AppendLine("        <LocalAccounts>");
                xml.AppendLine("          <LocalAccount wcm:action=\"add\">");
                xml.AppendLine($"            <Name>{user}</Name>");
                xml.AppendLine($"            <DisplayName>{user}</DisplayName>");
                xml.AppendLine("            <Group>Administrators</Group>");
                if (!string.IsNullOrEmpty(pass))
                {
                    xml.AppendLine("            <Password>");
                    xml.AppendLine($"              <Value>{pass}</Value>");
                    xml.AppendLine("              <PlainText>true</PlainText>");
                    xml.AppendLine("            </Password>");
                }
                xml.AppendLine("          </LocalAccount>");
                xml.AppendLine("        </LocalAccounts>");
                xml.AppendLine("      </UserAccounts>");
                xml.AppendLine("      <AutoLogon>");
                xml.AppendLine($"        <Username>{user}</Username>");
                xml.AppendLine("        <Enabled>true</Enabled>");
                xml.AppendLine("        <LogonCount>1</LogonCount>");
                if (!string.IsNullOrEmpty(pass))
                {
                    xml.AppendLine("        <Password>");
                    xml.AppendLine($"          <Value>{pass}</Value>");
                    xml.AppendLine("          <PlainText>true</PlainText>");
                    xml.AppendLine("        </Password>");
                }
                xml.AppendLine("      </AutoLogon>");
            }

            if (inject || cleanup)
            {
                xml.AppendLine("      <FirstLogonCommands>");
                xml.AppendLine("        <SynchronousCommand wcm:action=\"add\">");
                xml.AppendLine("          <Order>1</Order>");
                xml.AppendLine("          <Description>KitLugia Automation</Description>");
                xml.AppendLine("          <CommandLine>cmd /c \"for %i in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (if exist %i:\\_KitLugiaSetup\\first_logon.bat (call %i:\\_KitLugiaSetup\\first_logon.bat &amp; exit))\"</CommandLine>");
                xml.AppendLine("        </SynchronousCommand>");
                xml.AppendLine("      </FirstLogonCommands>");
            }

            xml.AppendLine("    </component>");
            
            if (disablePrivacy)
            {
                xml.AppendLine("    <component name=\"Microsoft-Windows-International-Core\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonVisual\" xmlns:wcm=\"http://schemas.microsoft.com/WCM/2002/Xml\">");
                xml.AppendLine("      <InputLocale>0409:00000409</InputLocale>");
                xml.AppendLine("      <SystemLocale>en-US</SystemLocale>");
                xml.AppendLine("      <UILanguage>en-US</UILanguage>");
                xml.AppendLine("      <UserLocale>en-US</UserLocale>");
                xml.AppendLine("    </component>");
            }
            xml.AppendLine("  </settings>");
            xml.AppendLine("</unattend>");
            return xml.ToString();
        }


        /// <summary>
        /// Injeta o comando de instalação do KitLugia em um XML Unattend existente.
        /// Procura pela seção FirstLogonCommands e adiciona se necessário.
        /// </summary>
        private static string PatchUnattendXml(string xml, string userName, string? password)
        {
            try
            {
                // 1. PATCH DE USUÁRIO (Súrgico - Apenas dentro de LocalAccounts)
                if (!string.IsNullOrEmpty(userName))
                {
                    // Regex mais inteligente que procura o contexto de conta local
                    // Altera o Nome e DisplayName apenas se tiver um Password ou LocalAccount por perto
                    xml = Regex.Replace(xml, @"(<LocalAccount.*?>.*?<Name>).*?(</Name>)", $"$1{userName}$2", RegexOptions.Singleline);
                    xml = Regex.Replace(xml, @"(<LocalAccount.*?>.*?<DisplayName>).*?(</DisplayName>)", $"$1{userName}$2", RegexOptions.Singleline);
                    
                    if (!string.IsNullOrEmpty(password))
                    {
                        xml = Regex.Replace(xml, @"(<Password>.*?<Value>).*?(</Value>)", $"$1{password}$2", RegexOptions.Singleline);
                    }
                    
                    // Fallback genérico para <Username> se for conta simples
                    xml = Regex.Replace(xml, @"(<Username>).*?(</Username>)", $"$1{userName}$2", RegexOptions.Singleline);
                }

                // 2. INJEÇÃO DE COMANDO (Garantir que o KitLugia rode)
                // Usamos um loop de varredura de drivers para encontrar o first_logon.bat na partição KITLUGIA
                string robustCommand = "cmd /c \"for %i in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do (if exist %i:\\_KitLugiaSetup\\first_logon.bat (call %i:\\_KitLugiaSetup\\first_logon.bat &amp; exit))\"";

                if (xml.Contains("</FirstLogonCommands>"))
                {
                    string commandNode = "\n        <SynchronousCommand wcm:action=\"add\">\n" +
                                         "          <Order>99</Order>\n" +
                                         $"          <CommandLine>{robustCommand}</CommandLine>\n" +
                                         "          <Description>KitLugia Setup</Description>\n" +
                                         "        </SynchronousCommand>\n      ";
                    xml = xml.Replace("</FirstLogonCommands>", commandNode + "</FirstLogonCommands>");
                }
                else if (xml.Contains("</component>"))
                {
                     string fullSection = "\n      <FirstLogonCommands>\n" +
                                          "        <SynchronousCommand wcm:action=\"add\">\n" +
                                          "          <Order>99</Order>\n" +
                                          $"          <CommandLine>{robustCommand}</CommandLine>\n" +
                                          "          <Description>KitLugia Setup</Description>\n" +
                                          "        </SynchronousCommand>\n" +
                                          "      </FirstLogonCommands>\n    ";
                     
                     // Inserir antes do fechamento do component pass oobeSystem (amd64_Microsoft-Windows-Shell-Setup)
                     if (xml.Contains("Microsoft-Windows-Shell-Setup"))
                     {
                         // Tenta achar o fim do componente shell setup
                         int shellIndex = xml.IndexOf("Microsoft-Windows-Shell-Setup");
                         int endCompIndex = xml.IndexOf("</component>", shellIndex);
                         if (endCompIndex > 0)
                         {
                             xml = xml.Insert(endCompIndex, fullSection);
                         }
                     }
                }

                return xml;
            }
            catch { return xml; }
        }

        public class AutomationProfile
        {
            public string FriendlyName { get; set; } = "Desconhecido";
            public string Description { get; set; } = "";
            public string FileName { get; set; } = "";
            public string FullPath { get; set; } = "";
            public bool IsDanger { get; set; }
            public bool IsRecommended { get; set; }
        }

        public static List<AutomationProfile> GetAutomationProfiles()
        {
            var profiles = new List<AutomationProfile>();
            
            // Perfil padrão (Gerador Interno)
            profiles.Add(new AutomationProfile 
            { 
                FriendlyName = "Personalizado (Gerador Interno)", 
                Description = "Crie sua própria automação usando as caixas de seleção acima.",
                FileName = null!
            });

            string goodiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "BootGoodies", "E2B_Unattend");
            
            // Portabilidade Garantida: Tenta pasta local primeiro, depois fallback de dev
            if (!Directory.Exists(goodiesPath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                goodiesPath = Path.Combine(projectRoot, "KitLugia.Core", "Resources", "BootGoodies", "E2B_Unattend");
            }

            if (Directory.Exists(goodiesPath))
            {
                try
                {
                    var files = Directory.GetFiles(goodiesPath, "*.xml");
                    foreach (var f in files)
                    {
                        string name = Path.GetFileName(f);
                        var profile = new AutomationProfile { FileName = name, FullPath = f };

                        // Traduções e descrições solicitadas pelo usuário
                        if (name.Contains("Win11_Pro_ContaLocal_SemTPM"))
                        {
                            profile.FriendlyName = "Windows 11 Pro - Conta Local e Sem TPM";
                            profile.Description = "Instala Win11 Pro pulando TPM/SecureBoot e forçando conta local.";
                        }
                        else if (name.Contains("Win11_Pro_SemBloatware_ContaLocal"))
                        {
                            profile.FriendlyName = "⭐ Win11Pro RECOMENDADO Limpo";
                            profile.Description = "Instalação otimizada sem apps inúteis e com conta local.";
                            profile.IsRecommended = true;
                        }
                        else if (name.Contains("WinLite10 - Windows 10 Pro"))
                        {
                            profile.FriendlyName = "Windows 10 Pro Lite (Otimizado)";
                            profile.Description = "Versão extremamente leve e rápida do Windows 10 Pro.";
                        }
                        else if (name.Contains("Win11_Pular_Requisitos_Geral"))
                        {
                            profile.FriendlyName = "Windows 11 - Pular Todos Requisitos";
                            profile.Description = "Ignora TPM 2.0, RAM, CPU e SecureBoot em qualquer versão.";
                        }
                        else if (name.Contains("Utilman - Hack Windows"))
                        {
                            profile.FriendlyName = "Hack de Recuperação (Utilman)";
                            profile.Description = "Substitui 'Acessibilidade' pelo CMD para resetar senhas.";
                        }
                        else if (name.Contains("ZZDANGER_Auto_WipeDisk0_Win10ProUS"))
                        {
                            profile.FriendlyName = "⚠️ AUTO-WIPE: Apagar Disco 0 (PERIGOSO)";
                            profile.Description = "Limpa o Disco 0 INTEIRO e instala o Win10 automaticamente.";
                            profile.IsDanger = true;
                        }
                        else if (name.Contains("No key (choose a version to install)"))
                        {
                            profile.FriendlyName = "Sem Chave - Menu de Versão";
                            profile.Description = "Não pede chave e deixa você escolher Pro/Home no menu.";
                        }
                        else if (name.Contains("SDI_CHOCO"))
                        {
                            profile.FriendlyName = "E2B: Instalação + Drivers + Softwares";
                            profile.Description = "Usa SDI para drivers e Chocolatey para apps comuns.";
                        }
                        else
                        {
                            profile.FriendlyName = "E2B: " + name.Replace(".xml", "");
                            profile.Description = "Script de automação avançada importado do Easy2Boot.";
                        }

                        profiles.Add(profile);
                    }
                }
                catch (Exception ex) { Log($"Erro ao carregar perfis de automação: {ex.Message}"); }
            }
            else
            {
                Log("Aviso: Diretório de BootGoodies não encontrado para carregar perfis E2B.");
            }

            return profiles;
        }

        // --- ADAPTIVE SIZING ---

        public static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path) && !File.Exists(path)) return 0;
            
            // Se for arquivo único (ex: single file publish)
            if (File.Exists(path) && !File.GetAttributes(path).HasFlag(FileAttributes.Directory))
            {
                return new FileInfo(path).Length;
            }

            long size = 0;
            try
            {
                // Arquivos na raiz
                var fileQuery = Directory.EnumerateFiles(path);
                foreach (var file in fileQuery)
                {
                    size += new FileInfo(file).Length;
                }
                // Subpastas (recursivo)
                var dirQuery = Directory.EnumerateDirectories(path);
                foreach (var dir in dirQuery)
                {
                    size += GetDirectorySize(dir);
                }
            }
            catch { }
            return size;
        }

        public static int CalculateRequiredSizeGB(string? userInjectedPath = null)
        {
            try 
            {
                long baseSize = 4L * 1024 * 1024 * 1024; // 4GB Base (WinPE + Boot Files + Small ISO)
                
                // Tamanho do App (KitLugia + Runtime)
                long appSize = GetDirectorySize(AppDomain.CurrentDomain.BaseDirectory);
                
                // Tamanho dos Goodies (Se externo)
                string goodiesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "BootGoodies");
                if (!Directory.Exists(goodiesPath))
                {
                    // Fallback Dev
                    string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));
                    goodiesPath = Path.Combine(projectRoot, "KitLugia.Core", "Resources", "BootGoodies");
                }
                long goodiesSize = GetDirectorySize(goodiesPath);

                // Tamanho da Injeção do Usuário
                long injectedSize = 0;
                if (!string.IsNullOrEmpty(userInjectedPath) && Directory.Exists(userInjectedPath))
                {
                    injectedSize = GetDirectorySize(userInjectedPath);
                }

                // Buffer de Segurança: 2GB (Updates, Logs, Temp)
                long bufferSize = 2L * 1024 * 1024 * 1024;

                long totalBytes = baseSize + appSize + goodiesSize + injectedSize + bufferSize;

                // Converter para GB arredondado para cima
                double gb = (double)totalBytes / (1024 * 1024 * 1024);
                int totalGB = (int)Math.Ceiling(gb);
                
                // Mínimo 8GB para evitar problemas
                return Math.Max(8, totalGB);
            }
            catch 
            {
                return 8; // Fallback seguro
            }
        }

        // --- IN-PLACE UPGRADE (UPDATE) ENGINE ---

        public static async Task<bool> StartInPlaceUpgrade(string isoPath, int index, string targetEditionId)
        {
            Log($"Iniciando Atualização In-place (ISO: {Path.GetFileName(isoPath)}, Index: {index})...");
            
            try 
            {
                // 1. Montar ISO
                string driveLetter = await MountIso(isoPath);
                if (string.IsNullOrEmpty(driveLetter))
                {
                    Log("Erro: Não foi possível montar a ISO.");
                    return false;
                }

                string setupPath = Path.Combine(driveLetter, "setup.exe");
                if (!File.Exists(setupPath)) setupPath = Path.Combine(driveLetter, "sources", "setup.exe");

                if (!File.Exists(setupPath))
                {
                    Log("Erro: setup.exe não encontrado na ISO.");
                    await DismountIso(isoPath);
                    return false;
                }

                // 2. Backup da EditionID atual
                string currentEditionId = GetCurrentEditionId();
                Log($"EditionID atual: {currentEditionId} -> Alvo: {targetEditionId}");

                // 3. Spoof EditionID no Registro (Burlar trava de edição)
                SetEditionId(targetEditionId);

                // 4. Rodar o setup com bypass de requisitos (/product server)
                Log("Lançando Setup do Windows (Ignorando Requisitos)...");
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = setupPath,
                    Arguments = "/product server",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process? p = Process.Start(psi);
                if (p != null)
                {
                    Log("Setup iniciado. O KitLugia aguardará o término para restaurar o registro.");
                    
                    // Task para monitorar o processo e restaurar o registro quando fechar
                    _ = Task.Run(async () => {
                        await p.WaitForExitAsync();
                        Log("Setup do Windows fechado. Restaurando EditionID original...");
                        SetEditionId(currentEditionId);
                        await DismountIso(isoPath);
                        Log("Processo de atualização finalizado.");
                    });
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log($"Erro crítico na atualização: {ex.Message}");
            }

            return false;
        }

        public struct WimEditionInfo
        {
            public int Index;
            public string Name;
            public string Architecture;
            public string EditionId;
            public string Version;

            public override string ToString() => $"{Name} ({Architecture} - {Version})";
        }

        public static async Task<List<WimEditionInfo>> GetIsoEditions(string isoPath)
        {
            var editions = new List<WimEditionInfo>();
            string drive = "";
            try 
            {
                drive = await MountIso(isoPath);
                if (string.IsNullOrEmpty(drive)) return editions;

                string wimPath = Path.Combine(drive, "sources", "install.wim");
                if (!File.Exists(wimPath)) wimPath = Path.Combine(drive, "sources", "install.esd");

                if (File.Exists(wimPath))
                {
                    var (_, output) = await RunProcessCaptured("dism.exe", $"/Get-ImageInfo /ImageFile:\"{wimPath}\"");
                    
                    // Parse DISM output
                    var matches = Regex.Matches(output, @"Índice\s*:\s*(\d+).*?Nome\s*:\s*(.*?)(?=Descrição|Tamanho|Índice|$)", RegexOptions.Singleline);
                    foreach (Match m in matches)
                    {
                        var info = new WimEditionInfo { 
                            Index = int.Parse(m.Groups[1].Value), 
                            Name = m.Groups[2].Value.Trim() 
                        };
                        
                        // Pegar EditionID detalhado
                        var (_, detail) = await RunProcessCaptured("dism.exe", $"/Get-ImageInfo /ImageFile:\"{wimPath}\" /Index:{info.Index}");
                        var edMatch = Regex.Match(detail, @"Edição\s*:\s*(.*)");
                        if (edMatch.Success) info.EditionId = edMatch.Groups[1].Value.Trim();
                        
                        var archMatch = Regex.Match(detail, @"Arquitetura\s*:\s*(.*)");
                        if (archMatch.Success) info.Architecture = archMatch.Groups[1].Value.Trim();

                        var verMatch = Regex.Match(detail, @"Versão\s*:\s*(.*)");
                        if (verMatch.Success) info.Version = verMatch.Groups[1].Value.Trim();

                        editions.Add(info);
                    }
                }
            }
            catch (Exception ex) { Log($"Erro ao ler edições da ISO: {ex.Message}"); }
            finally { if (!string.IsNullOrEmpty(drive)) await DismountIso(isoPath); }
            
            return editions;
        }

        private static async Task<string> MountIso(string isoPath)
        {
            string script = $"Mount-DiskImage -ImagePath '{isoPath}' -PassThru | Get-Volume | Select-Object -ExpandProperty DriveLetter";
            string drv = await RunPowerShell(script);
            drv = drv.Trim();
            return drv.Length == 1 ? drv + ":" : "";
        }

        private static async Task DismountIso(string isoPath)
        {
            await RunPowerShell($"Dismount-DiskImage -ImagePath '{isoPath}'");
        }

        private static async Task<string> RunPowerShell(string script)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            return await process.StandardOutput.ReadToEndAsync();
        }

        public static string GetCurrentEditionId()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                return key?.GetValue("EditionID")?.ToString() ?? "Professional";
            }
            catch { return "Professional"; }
        }

        public static void SetEditionId(string editionId)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", true);
                if (key != null)
                {
                    key.SetValue("EditionID", editionId);
                    Log($"Registro: EditionID alterado para {editionId}");
                }
            }
            catch (Exception ex) { Log($"Erro ao modificar EditionID no registro: {ex.Message}"); }
        }

        private static async Task<string?> LocateWinreWim()
        {
            Log("Localizando 'Doador' para Ponte (Winre/Boot.wim)...");
            
            // 1. Check common paths
            var paths = new List<string> {
                @"C:\Recovery\WindowsRE\winre.wim",
                @"C:\Windows\System32\Recovery\winre.wim"
            };
            foreach (var p in paths) if (File.Exists(p)) return p;

            // 2. ULTIMATE RECOURSE: Extract from local ISO
            return await FindWimInLocalIsos();
        }

        private static async Task<string?> FindWimInLocalIsos()
        {
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] searchPaths = { Path.Combine(userProfile, "Downloads"), Path.Combine(userProfile, "Desktop") };
                foreach (var folder in searchPaths.Where(Directory.Exists))
                {
                    var isos = Directory.GetFiles(folder, "*Strelec*.iso");
                    foreach (var iso in isos)
                    {
                        var (code, output) = await RunProcessCaptured("powershell.exe", $"-Command \"Mount-DiskImage -ImagePath '{iso}'\"");
                        if (code == 0)
                        {
                            await Task.Delay(2000);
                            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.CDRom && d.IsReady))
                            {
                                string wimPath = Path.Combine(drive.RootDirectory.FullName, "sources", "boot.wim");
                                if (File.Exists(wimPath))
                                {
                                    string cachePath = Path.Combine(Path.GetTempPath(), "kitlugia_donor_boot.wim");
                                    File.Copy(wimPath, cachePath, true);
                                    await RunProcessCaptured("powershell.exe", $"-Command \"Dismount-DiskImage -ImagePath '{iso}'\"");
                                    return cachePath;
                                }
                            }
                            await RunProcessCaptured("powershell.exe", $"-Command \"Dismount-DiskImage -ImagePath '{iso}'\"");
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetStrelecDistroPath(string description)
        {
            if (description.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase)) return "\\Linux\\ubuntu";
            if (description.Contains("Kali", StringComparison.OrdinalIgnoreCase)) return "\\Linux\\kalilinux2019";
            if (description.Contains("Fedora", StringComparison.OrdinalIgnoreCase)) return "\\Linux\\fedora";
            if (description.Contains("Debian", StringComparison.OrdinalIgnoreCase)) return "\\Linux\\debian";
            return "\\Linux\\generic";
        }
    }
}
