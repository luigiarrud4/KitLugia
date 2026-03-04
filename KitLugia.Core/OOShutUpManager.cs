using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class OOShutUpManager
    {
        #region Estruturas

        public enum PrivacyLevel
        {
            Recommended,    // Seguro, não quebra nada
            Limited,        // Privacidade moderada
            NotRecommended  // Máximo, pode quebrar recursos (Cortana, Loja, etc)
        }

        public class PrivacySetting
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string RegistryPath { get; set; } = string.Empty;
            public string ValueName { get; set; } = string.Empty;
            public object? SafeValue { get; set; }   // Valor para "Proteger" (Desativar recurso)
            public object? UnsafeValue { get; set; } // Valor Padrão do Windows
            public PrivacyLevel Level { get; set; }
            public string Category { get; set; } = string.Empty;
            public bool IsService { get; set; } = false;
            public string? ServiceName { get; set; }
        }

        // ================================================================
        // LISTA MESTRA — Baseada no O&O ShutUp10++ e documentação Microsoft
        // ================================================================
        private static readonly List<PrivacySetting> PrivacySettings = new()
        {
            // ====================== TELEMETRIA ======================
            new() {
                Name = "Telemetria do Windows",
                Description = "Impede o envio de dados de uso e diagnóstico para a Microsoft.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                ValueName = "AllowTelemetry",
                SafeValue = 0, UnsafeValue = 3,
                Level = PrivacyLevel.Recommended, Category = "Telemetria"
            },
            new() {
                Name = "Serviço DiagTrack",
                Description = "Serviço de Experiência do Usuário Conectado e Telemetria.",
                IsService = true, ServiceName = "DiagTrack",
                SafeValue = 4, UnsafeValue = 2,
                Level = PrivacyLevel.Recommended, Category = "Telemetria"
            },
            new() {
                Name = "Serviço dmwappushservice",
                Description = "Serviço de roteamento de mensagens push WAP (telemetria).",
                IsService = true, ServiceName = "dmwappushservice",
                SafeValue = 4, UnsafeValue = 3,
                Level = PrivacyLevel.Recommended, Category = "Telemetria"
            },
            new() {
                Name = "Relatório de Erros do Windows",
                Description = "Impede envio de relatórios de erros para a Microsoft.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting",
                ValueName = "Disabled",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Telemetria"
            },
            new() {
                Name = "Dados de Diagnóstico Personalizados",
                Description = "Impede envio de dados de diagnóstico personalizados.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                ValueName = "LimitDiagnosticLogCollection",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Telemetria"
            },

            // ====================== PERSONALIZAÇÃO DE INPUT ======================
            new() {
                Name = "Envio de Dados de Digitação",
                Description = "Impede que dados de digitação sejam enviados para a Microsoft.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Input\TIPC",
                ValueName = "Enabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Input"
            },
            new() {
                Name = "Personalização de Escrita à Mão",
                Description = "Desativa envio de dados de escrita à mão para a Microsoft.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\TabletPC",
                ValueName = "PreventHandwritingDataSharing",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Input"
            },

            // ====================== LOCALIZAÇÃO ======================
            new() {
                Name = "Rastreamento de Localização",
                Description = "Desativa a localização global do dispositivo.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors",
                ValueName = "DisableLocation",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Localização"
            },

            // ====================== CORTANA & PESQUISA ======================
            new() {
                Name = "Cortana",
                Description = "Desativa a assistente virtual Cortana.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                ValueName = "AllowCortana",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Limited, Category = "Cortana & Pesquisa"
            },
            new() {
                Name = "Pesquisa na Web (Menu Iniciar)",
                Description = "Impede que o Menu Iniciar pesquise no Bing.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                ValueName = "DisableWebSearch",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Cortana & Pesquisa"
            },
            new() {
                Name = "Sugestões de Pesquisa na Nuvem",
                Description = "Impede sugestões de pesquisa na nuvem na barra de tarefas.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
                ValueName = "AllowCloudSearch",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Cortana & Pesquisa"
            },

            // ====================== COPILOT ======================
            new() {
                Name = "Windows Copilot",
                Description = "Desativa o assistente Copilot do Windows 11.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot",
                ValueName = "TurnOffWindowsCopilot",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Limited, Category = "Copilot & IA"
            },
            new() {
                Name = "Botão Copilot na Barra de Tarefas",
                Description = "Remove o botão do Copilot da barra de tarefas.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                ValueName = "ShowCopilotButton",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Limited, Category = "Copilot & IA"
            },
            new() {
                Name = "Windows Recall",
                Description = "Desativa a função Recall (captura de tela automática por IA).",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsAI",
                ValueName = "DisableAIDataAnalysis",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Copilot & IA"
            },

            // ====================== PUBLICIDADE ======================
            new() {
                Name = "ID de Publicidade",
                Description = "Impede que apps usem seu ID de publicidade.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
                ValueName = "Enabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Publicidade"
            },
            new() {
                Name = "ID de Publicidade (Política Global)",
                Description = "Desativa ID de publicidade via Group Policy para todos os usuários.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo",
                ValueName = "DisabledByGroupPolicy",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Publicidade"
            },
            new() {
                Name = "Instalação Automática de Apps",
                Description = "Impede instalação silenciosa de apps sugeridos (bloatware).",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                ValueName = "SilentInstalledAppsEnabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Publicidade"
            },
            new() {
                Name = "Sugestões no Menu Iniciar",
                Description = "Remove apps sugeridos do Menu Iniciar.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                ValueName = "SystemPaneSuggestionsEnabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Publicidade"
            },
            new() {
                Name = "Sugestões em Configurações",
                Description = "Remove sugestões de apps no aplicativo Configurações.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                ValueName = "SubscribedContent-338393Enabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Publicidade"
            },
            new() {
                Name = "Dicas e Sugestões do Windows",
                Description = "Desativa notificações de 'Dicas do Windows' e sugestões.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                ValueName = "SubscribedContent-338389Enabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Publicidade"
            },

            // ====================== CLIPBOARD & TIMELINE ======================
            new() {
                Name = "Histórico de Área de Transferência",
                Description = "Desativa o histórico de clipboard (Win+V).",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                ValueName = "AllowClipboardHistory",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Limited, Category = "Clipboard & Timeline"
            },
            new() {
                Name = "Sincronização de Clipboard na Nuvem",
                Description = "Impede sincronização do clipboard entre dispositivos.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                ValueName = "AllowCrossDeviceClipboard",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Clipboard & Timeline"
            },
            new() {
                Name = "Timeline (Feed de Atividades)",
                Description = "Desativa o rastreamento de atividades do usuário.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                ValueName = "EnableActivityFeed",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Clipboard & Timeline"
            },
            new() {
                Name = "Publicação de Atividades",
                Description = "Impede publicação de atividades do usuário para a Microsoft.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                ValueName = "PublishUserActivities",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Clipboard & Timeline"
            },
            new() {
                Name = "Upload de Atividades",
                Description = "Impede upload de histórico de atividades.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System",
                ValueName = "UploadUserActivities",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Clipboard & Timeline"
            },

            // ====================== ONEDRIVE ======================
            new() {
                Name = "Sincronização OneDrive",
                Description = "Desativa a sincronização automática de arquivos com o OneDrive.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\OneDrive",
                ValueName = "DisableFileSyncNGSC",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.NotRecommended, Category = "OneDrive"
            },
            new() {
                Name = "Oferta de Backup na Nuvem",
                Description = "Bloqueia a oferta de backup de pastas no OneDrive.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\OneDrive",
                ValueName = "KFMBlockOptIn",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Limited, Category = "OneDrive"
            },

            // ====================== WI-FI SENSE ======================
            new() {
                Name = "Wi-Fi Sense (Hotspot Automático)",
                Description = "Desativa conexão automática a hotspots sugeridos.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WcmSvc\wifinetworkmanager\config",
                ValueName = "AutoConnectAllowedOEM",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Wi-Fi"
            },

            // ====================== WINDOWS UPDATE ======================
            new() {
                Name = "Otimização de Entrega (P2P)",
                Description = "Impede que seu PC envie updates para outros na internet.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config",
                ValueName = "DODownloadMode",
                SafeValue = 0, UnsafeValue = 3,
                Level = PrivacyLevel.Recommended, Category = "Updates"
            },
            new() {
                Name = "Instalação Automática de Drivers",
                Description = "Impede que o Windows Update instale drivers automaticamente.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate",
                ValueName = "ExcludeWUDriversInQualityUpdate",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Limited, Category = "Updates"
            },

            // ====================== APPS & LOJA ======================
            new() {
                Name = "Apps em Segundo Plano",
                Description = "Impede que apps da loja rodem sem você abrir.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                ValueName = "GlobalUserDisabled",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Limited, Category = "Apps"
            },
            new() {
                Name = "Acesso à Câmera (Global)",
                Description = "Bloqueia o acesso de todos os apps à webcam.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                ValueName = "LetAppsAccessCamera",
                SafeValue = 2, UnsafeValue = 0,
                Level = PrivacyLevel.NotRecommended, Category = "Apps"
            },
            new() {
                Name = "Acesso ao Microfone (Global)",
                Description = "Bloqueia o acesso de todos os apps ao microfone.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                ValueName = "LetAppsAccessMicrophone",
                SafeValue = 2, UnsafeValue = 0,
                Level = PrivacyLevel.NotRecommended, Category = "Apps"
            },
            new() {
                Name = "Acesso a Contatos (Global)",
                Description = "Bloqueia acesso de apps aos contatos.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                ValueName = "LetAppsAccessContacts",
                SafeValue = 2, UnsafeValue = 0,
                Level = PrivacyLevel.NotRecommended, Category = "Apps"
            },
            new() {
                Name = "Acesso a Notificações (Global)",
                Description = "Bloqueia acesso de apps às notificações.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                ValueName = "LetAppsAccessNotifications",
                SafeValue = 2, UnsafeValue = 0,
                Level = PrivacyLevel.NotRecommended, Category = "Apps"
            },

            // ====================== FEEDBACK ======================
            new() {
                Name = "Solicitações de Feedback",
                Description = "Impede que o Windows peça feedback.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                ValueName = "DoNotShowFeedbackNotifications",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Feedback"
            },
            new() {
                Name = "Frequência de Feedback",
                Description = "Define frequência de solicitação de feedback como 'Nunca'.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Siuf\Rules",
                ValueName = "NumberOfSIUFInPeriod",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Feedback"
            },

            // ====================== EXPLORER & PRIVACIDADE ======================
            new() {
                Name = "Histórico de Arquivos Recentes",
                Description = "Impede que o Explorer mostre arquivos recentes no Acesso Rápido.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Explorer",
                ValueName = "ShowRecent",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Explorer"
            },

            // ====================== SEGURANÇA ======================
            new() {
                Name = "Botão Revelar Senha",
                Description = "Desativa o botão de revelar senha em campos de login.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CredUI",
                ValueName = "DisablePasswordReveal",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Limited, Category = "Segurança"
            },
            new() {
                Name = "Gravador de Passos",
                Description = "Desativa a ferramenta de gravação de passos (Steps Recorder).",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppCompat",
                ValueName = "DisableUAR",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Segurança"
            },

            // ====================== TELA DE BLOQUEIO ======================
            new() {
                Name = "Destaques do Windows (Spotlight)",
                Description = "Desativa imagens e dicas da Microsoft na tela de bloqueio.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                ValueName = "DisableWindowsSpotlightFeatures",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Limited, Category = "Tela de Bloqueio"
            },
            new() {
                Name = "Dicas e Truques na Tela de Bloqueio",
                Description = "Remove 'Você sabia?' e outras dicas da tela de bloqueio.",
                RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
                ValueName = "RotatingLockScreenOverlayEnabled",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Tela de Bloqueio"
            },
            new() {
                Name = "Conteúdo Sugerido na Nuvem",
                Description = "Desativa conteúdo sugerido pela Microsoft (dicas, apps, etc.).",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\CloudContent",
                ValueName = "DisableSoftLanding",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Tela de Bloqueio"
            },

            // ====================== EDGE ======================
            new() {
                Name = "Do Not Track (Edge)",
                Description = "Habilita cabeçalho 'Do Not Track' no Microsoft Edge.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\MicrosoftEdge\Main",
                ValueName = "DoNotTrack",
                SafeValue = 1, UnsafeValue = 0,
                Level = PrivacyLevel.Recommended, Category = "Edge"
            },
            new() {
                Name = "Sugestões de Pesquisa (Edge)",
                Description = "Desativa sugestões de pesquisa no Edge.",
                RegistryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\MicrosoftEdge\SearchScopes",
                ValueName = "ShowSearchSuggestionsGlobal",
                SafeValue = 0, UnsafeValue = 1,
                Level = PrivacyLevel.Recommended, Category = "Edge"
            },
        };

        #endregion

        #region Métodos de Gerenciamento

        public static List<PrivacySetting> GetPrivacySettings() => PrivacySettings;

        public static Dictionary<string, List<PrivacySetting>> GetPrivacyCategories()
        {
            return PrivacySettings.GroupBy(s => s.Category)
                                 .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Verifica se a configuração de privacidade está aplicada (protegida).
        /// CORRIGIDO: usa RegistryPath diretamente (não trunca mais).
        /// </summary>
        public static bool IsPrivacySettingApplied(PrivacySetting setting)
        {
            try
            {
                if (setting.IsService && !string.IsNullOrEmpty(setting.ServiceName))
                {
                    var startMode = SystemUtils.GetServiceStartMode(setting.ServiceName);
                    string safeValStr = setting.SafeValue?.ToString() ?? "4";
                    if (safeValStr == "4") return startMode == "Disabled";
                    return false; 
                }
                else
                {
                    // Registry.GetValue espera: keyName (caminho completo com hive), valueName, defaultValue
                    var val = Registry.GetValue(setting.RegistryPath, setting.ValueName, null);
                    
                    if (val == null) return false; // Se não existe, assume padrão (inseguro)
                    
                    return val.ToString() == setting.SafeValue?.ToString();
                }
            }
            catch { return false; }
        }

        public static bool ApplyPrivacySetting(PrivacySetting setting)
        {
            try
            {
                if (setting.IsService && !string.IsNullOrEmpty(setting.ServiceName))
                {
                    int mode = Convert.ToInt32(setting.SafeValue);
                    string modeStr = mode == 4 ? "disabled" : (mode == 2 ? "auto" : "demand");
                    SystemUtils.RunExternalProcess("sc", $"config \"{setting.ServiceName}\" start= {modeStr}", true);
                    if (mode == 4) SystemUtils.RunExternalProcess("sc", $"stop \"{setting.ServiceName}\"", true);
                    return true;
                }
                else
                {
                    string key = setting.RegistryPath;
                    string val = setting.ValueName;
                    object data = setting.SafeValue ?? 0;
                    
                    RegistryKey hive = key.StartsWith("HKEY_LOCAL_MACHINE") ? Registry.LocalMachine : Registry.CurrentUser;
                    string subKey = key.Substring(key.IndexOf('\\') + 1);

                    using var rk = hive.CreateSubKey(subKey, true);
                    rk.SetValue(val, data, data is string ? RegistryValueKind.String : RegistryValueKind.DWord);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"ApplyPrivacy[{setting.Name}]", ex.Message);
                return false;
            }
        }

        public static bool RevertPrivacySetting(PrivacySetting setting)
        {
            try
            {
                if (setting.IsService && !string.IsNullOrEmpty(setting.ServiceName))
                {
                    int mode = Convert.ToInt32(setting.UnsafeValue);
                    string modeStr = mode == 2 ? "auto" : "demand";
                    SystemUtils.RunExternalProcess("sc", $"config \"{setting.ServiceName}\" start= {modeStr}", true);
                    return true;
                }
                else
                {
                    string key = setting.RegistryPath;
                    string val = setting.ValueName;

                    RegistryKey hive = key.StartsWith("HKEY_LOCAL_MACHINE") ? Registry.LocalMachine : Registry.CurrentUser;
                    string subKey = key.Substring(key.IndexOf('\\') + 1);

                    using var rk = hive.OpenSubKey(subKey, true);
                    if (rk != null)
                    {
                        if (setting.UnsafeValue != null)
                            rk.SetValue(val, setting.UnsafeValue);
                        else
                            rk.DeleteValue(val, false);
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"RevertPrivacy[{setting.Name}]", ex.Message);
                return false;
            }
        }

        public static (bool Success, string Message) ApplyPreset(PrivacyLevel targetLevel)
        {
            try
            {
                int successCount = 0;
                foreach (var s in PrivacySettings)
                {
                    if ((int)s.Level <= (int)targetLevel)
                    {
                        if (ApplyPrivacySetting(s))
                            successCount++;
                    }
                }
                return (true, $"Preset aplicado com sucesso. {successCount} configurações ajustadas.");
            }
            catch (Exception ex) { return (false, $"Erro ao aplicar preset: {ex.Message}"); }
        }

        public static (bool Success, string Message) RestoreDefaults()
        {
            try
            {
                int successCount = 0;
                foreach (var s in PrivacySettings)
                {
                    if (RevertPrivacySetting(s))
                        successCount++;
                }
                return (true, $"Padrão restaurado. {successCount} configurações revertidas.");
            }
            catch (Exception ex) { return (false, $"Erro ao restaurar padrões: {ex.Message}"); }
        }

        #endregion
    }
}
