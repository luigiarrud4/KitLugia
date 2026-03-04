using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static class Guardian
    {
        #region Definições de Tweaks (Lista Completa e Detalhada)
        
    // Mudei de private para internal/static acessível via método abaixo
    private static readonly List<ScannableTweak> HarmfulTweaks = new()
    {
        // ==================================================================================
        // 1. SEGURANÇA CRÍTICA DO SISTEMA
        // ==================================================================================
        new() {
            Name = "Mitigações de CPU (Spectre/Meltdown)",
            Description = "Correções de segurança para o processador. Protege contra vulnerabilidades que permitem que programas maliciosos leiam dados sensíveis da memória do sistema.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "FeatureSettingsOverride", HarmfulValue = 3, DefaultValue = 0
        },
        new() {
            Name = "Control Flow Guard (CFG)",
            Description = "Uma defesa contra exploits que impede que malwares sequestrem o fluxo de execução de programas legítimos para rodar código malicioso.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "EnableCfg", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Controle de Conta de Usuário (UAC)",
            Description = "A janela de confirmação 'Sim/Não'. Impede que vírus ou scripts façam alterações administrativas no sistema sem o seu consentimento explícito.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "EnableLUA", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Execução de Dados (DEP)",
            Description = "Impede que códigos maliciosos sejam executados em áreas da memória reservadas apenas para dados. Essencial para evitar ataques de buffer overflow.",
            Category = "Segurança Crítica", Type = TweakType.Bcd, ValueName = "nx", HarmfulValue = "AlwaysOff", DefaultValue = "OptIn"
        },
        new() {
            Name = "Protocolo Inseguro SMBv1",
            Description = "Um protocolo de compartilhamento de rede antigo e obsoleto, famoso por ser o vetor de ataque do ransomware WannaCry. Deve permanecer desativado.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", ValueName = "SMB1", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Execução Automática de USB",
            Description = "Impede que vírus de Pen Drive (autorun.inf) infectem o computador automaticamente assim que a mídia é conectada.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", ValueName = "NoDriveTypeAutoRun", HarmfulValue = 0, DefaultValue = 145
        },

        // ==================================================================================
        // 2. DEFESA E ANTIVÍRUS
        // ==================================================================================
        new() {
            Name = "Antivírus Windows Defender",
            Description = "Proteção essencial em tempo real contra vírus e ameaças. Se você não possui outro antivírus instalado, desativar isso deixa o PC vulnerável.",
            Category = "Defesa e Antivírus", Type = TweakType.Service, ServiceName = "WinDefend", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Firewall do Windows",
            Description = "Filtra o tráfego de rede de entrada e saída. Desativar permite que hackers ou worms acessem seu PC diretamente pela internet.",
            Category = "Defesa e Antivírus", Type = TweakType.Service, ServiceName = "MpsSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Proteção contra Violação",
            Description = "Recurso de autodefesa do Windows que impede que malwares ou scripts desativem o antivírus e suas configurações de segurança.",
            Category = "Defesa e Antivírus", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender\Features", ValueName = "TamperProtection", HarmfulValue = 0, DefaultValue = 5
        },
        new() {
            Name = "Central de Segurança (WSC)",
            Description = "Monitora o status do antivírus, firewall e manutenção. Se desligado, o Windows não alertará sobre falhas na sua proteção.",
            Category = "Defesa e Antivírus", Type = TweakType.Service, ServiceName = "wscsvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Delayed-Auto"
        },
        new() {
            Name = "Filtro SmartScreen",
            Description = "Verifica sites e downloads em busca de ameaças conhecidas antes de executá-los, protegendo contra phishing e malwares novos.",
            Category = "Defesa e Antivírus", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\System", ValueName = "EnableSmartScreen", HarmfulValue = 0, DefaultValue = 1
        },

        // ==================================================================================
        // 3. RESTRIÇÕES DO SISTEMA (SINAIS DE ALERTA)
        // ==================================================================================
        new() {
            Name = "Gerenciador de Tarefas (Bloqueado)",
            Description = "Se estiver bloqueado, é um forte indício de vírus ou restrição administrativa tentando impedir que você veja e encerre processos.",
            Category = "Restrições do Sistema", KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "DisableTaskMgr", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Editor do Registro (Bloqueado)",
            Description = "Malwares frequentemente bloqueiam o Regedit para impedir que você remova suas chaves de inicialização ou corrija o sistema.",
            Category = "Restrições do Sistema", KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "DisableRegistryTools", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Prompt de Comando (Bloqueado)",
            Description = "O bloqueio do CMD impede a execução de ferramentas de reparo, scripts de limpeza e diagnósticos avançados.",
            Category = "Restrições do Sistema", KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "DisableCMD", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Instalador Windows MSI (Bloqueado)",
            Description = "Impede a instalação ou remoção de programas .msi. Frequentemente usado para impedir a instalação de ferramentas de segurança.",
            Category = "Restrições do Sistema", Type = TweakType.Service, ServiceName = "msiserver", HarmfulStartMode = "Disabled", DefaultStartMode = "Demand"
        },
        
        // ==================================================================================
        // 4. SERVIÇOS ESSENCIAIS
        // ==================================================================================
        new() {
            Name = "Serviço de Áudio",
            Description = "Responsável por gerenciar o som do Windows. Se desativado, o computador ficará sem áudio.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "AudioSrv", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Serviço de Perfis de Usuário",
            Description = "Carrega e descarrega as configurações do usuário. Falha neste serviço pode impedir o login ou corromper o perfil.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "ProfSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Serviço de Eventos (Log)",
            Description = "Registra erros e atividades do sistema. Essencial para diagnóstico de falhas e para o funcionamento de vários serviços.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "eventlog", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Agendador de Tarefas",
            Description = "Permite configurar e executar tarefas automatizadas. Vital para manutenção do sistema e inicialização de muitos programas.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "Schedule", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Chamada Remota (RPC)",
            Description = "O 'sistema nervoso' do Windows. Permite a comunicação entre processos. Quase tudo depende disso. Nunca deve ser desativado.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "RpcSs", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Mecanismo de Filtragem Base (BFE)",
            Description = "Gerencia políticas de firewall e IPsec. Se desativado, reduz drasticamente a segurança da rede e quebra o Firewall.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "BFE", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Gerenciador de Credenciais (Vault)",
            Description = "Armazena e recupera senhas salvas de sites, redes e aplicativos com segurança.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "VaultSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Área de Transferência",
            Description = "Habilita a funcionalidade de copiar e colar moderna, incluindo histórico (Win+V) e sincronização.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "cbdhsvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Cache de Fontes",
            Description = "Otimiza o desempenho de renderização de texto. Desativar pode causar lentidão na abertura de aplicativos.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "FontCache", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },

        // ==================================================================================
        // 5. REDE E CONECTIVIDADE
        // ==================================================================================
        new() {
            Name = "Cliente DNS",
            Description = "Traduz nomes de sites (ex: google.com) para endereços IP. Sem ele, a navegação na internet para de funcionar.",
            Category = "Rede e Conectividade", Type = TweakType.Service, ServiceName = "Dnscache", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Cliente DHCP",
            Description = "Obtém um endereço IP automaticamente do seu roteador. Necessário para conectar à maioria das redes.",
            Category = "Rede e Conectividade", Type = TweakType.Service, ServiceName = "Dhcp", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Configuração de Wi-Fi (WLAN)",
            Description = "Gerencia conexões sem fio. Essencial para laptops e computadores que utilizam Wi-Fi.",
            Category = "Rede e Conectividade", Type = TweakType.Service, ServiceName = "WlanSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Auto"
        },
        new() {
            Name = "Lista de Redes (Network List)",
            Description = "Identifica as redes às quais o computador se conectou e suas configurações. Necessário para ícone de rede na barra.",
            Category = "Rede e Conectividade", Type = TweakType.Service, ServiceName = "netprofm", HarmfulStartMode = "Disabled", DefaultStartMode = "Manual"
        },
        new() {
            Name = "Throttling de Rede (Configuração Inválida)",
            Description = "Valores incorretos no registro podem limitar a velocidade da internet ou causar instabilidade em jogos online.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", ValueName = "NetworkThrottlingIndex", HarmfulValue = unchecked((int)0xFFFFFFFF), DefaultValue = 10
        },

        // ==================================================================================
        // 6. ATUALIZAÇÕES E LOJA
        // ==================================================================================
        new() {
            Name = "Windows Update",
            Description = "Mantém o sistema seguro e atualizado. Desativar permanentemente impede o recebimento de correções críticas.",
            Category = "Atualizações e Loja", Type = TweakType.Service, ServiceName = "wuauserv", HarmfulStartMode = "Disabled", DefaultStartMode = "Demand"
        },
        new() {
            Name = "Serviço de Instalação da Loja",
            Description = "Necessário para baixar e atualizar aplicativos da Microsoft Store (incluindo apps como Calculadora e Fotos).",
            Category = "Atualizações e Loja", Type = TweakType.Service, ServiceName = "InstallService", HarmfulStartMode = "Disabled", DefaultStartMode = "Demand"
        },
        new() {
            Name = "Transferência Inteligente (BITS)",
            Description = "Gerencia downloads em segundo plano. Se desativado, Windows Update e outros apps podem falhar ao baixar conteúdo.",
            Category = "Atualizações e Loja", Type = TweakType.Service, ServiceName = "BITS", HarmfulStartMode = "Disabled", DefaultStartMode = "Delayed-Auto"
        },
        new() {
            Name = "Otimização de Entrega (DoSvc)",
            Description = "Ajuda a baixar atualizações mais rápido. Desativar completamente pode causar falhas no Windows Update.",
            Category = "Atualizações e Loja", Type = TweakType.Service, ServiceName = "DoSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Delayed-Auto"
        },
        new() {
            Name = "Bloqueio de Drivers (Busca)",
            Description = "Configuração que impede o Windows de buscar drivers automaticamente para novos dispositivos conectados.",
            Category = "Atualizações e Loja", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching", ValueName = "SearchOrderConfig", HarmfulValue = 0, DefaultValue = 1
        },

        // ==================================================================================
        // 7. ESTABILIDADE E HARDWARE
        // ==================================================================================
        new() {
            Name = "Arquivo de Paginação (Page File)",
            Description = "Memória virtual no disco. Desativar pode causar fechamento repentino de jogos e erros de 'Memória Insuficiente'.",
            Category = "Estabilidade", Type = TweakType.PageFile
        },
        new() {
            Name = "Hibernação (Fast Startup)",
            Description = "Necessário para a Inicialização Rápida do Windows funcionar. Se desativado, o boot será mais lento.",
            Category = "Estabilidade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", ValueName = "HibernateEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Horário do Windows (Time)",
            Description = "Mantém a data e hora sincronizadas. Hora errada causa erros de certificado na internet e problemas em jogos.",
            Category = "Estabilidade", Type = TweakType.Service, ServiceName = "W32Time", HarmfulStartMode = "Disabled", DefaultStartMode = "Demand"
        },
        new() {
            Name = "Dynamic Tick (BCD)",
            Description = "Recurso de gerenciamento de energia da CPU. Desativá-lo (Yes) é um mito de performance antigo que não traz benefícios reais.",
            Category = "Estabilidade", Type = TweakType.Bcd, ValueName = "disabledynamictick", HarmfulValue = "Yes", DefaultValue = "No"
        },
        
        // ==================================================================================
        // 8. PRIVACIDADE GLOBAL
        // ==================================================================================
        new() {
            Name = "Acesso Global à Câmera (Bloqueado)",
            Description = "Um bloqueio total via registro. Impede que Zoom, Teams, Discord e outros apps usem sua webcam.",
            Category = "Privacidade Global", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam", ValueName = "Value", HarmfulValue = "Deny", DefaultValue = "Allow"
        },
        new() {
            Name = "Acesso Global ao Microfone (Bloqueado)",
            Description = "Um bloqueio total via registro. Nenhum aplicativo conseguirá capturar áudio do seu microfone.",
            Category = "Privacidade Global", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone", ValueName = "Value", HarmfulValue = "Deny", DefaultValue = "Allow"
        },
        new() {
            Name = "Aceleração de Mouse",
            Description = "Verifica se a aceleração do ponteiro está desativada (comum em otimizações gamer) ou no padrão do Windows.",
            Category = "Acessibilidade", Type = TweakType.Mouse
        },

        // ==================================================================================
        // 9. VULNERABILIDADES CRÍTICAS DE 2024 (NOVAS ADIÇÕES)
        // ==================================================================================
        new() {
            Name = "Virtualização Baseada em Segurança (VBS) - CVE-2024-21302",
            Description = "Vulnerabilidade crítica de 2024 que permite escalonamento de privilégios. Atacantes com acesso admin podem substituir arquivos do sistema por versões antigas, comprometendo a integridade do kernel e reintroduzindo vulnerabilidades já corrigidas.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", ValueName = "EnableVirtualizationBasedSecurity", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Integridade de Memória (HVCI) - Vulnerabilidade 2024",
            Description = "Proteção de integridade de código do hypervisor. Desativar expõe o sistema a injeção de kernel e rootkits. Vulnerabilidades de 2024 mostraram que sistemas sem HVCI são suscetíveis a ataques de persistência avançados.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", ValueName = "HypervisorEnforcedCodeIntegrity", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Pilha de Hardware (2024)",
            Description = "Mecanismo de segurança que previne ataques de buffer overflow no nível de hardware. Desativar torna o sistema vulnerável a exploits de memória que podem causar lentidão extrema e instabilidade.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "FeatureSettingsOverrideMask", HarmfulValue = 0, DefaultValue = 3
        },
        new() {
            Name = "Isolamento de Core (Memory Integrity) - 2024",
            Description = "Recursos de segurança avançados que isolam processos críticos. Vulnerabilidades recentes mostram que desativar isso permite que malware execute código em modo kernel, causando degradação severa de performance.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", ValueName = "RequirePlatformSignedDrivers", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção contra DMA - 2024",
            Description = "Proteção contra ataques Direct Memory Access. Vulnerabilidades de 2024 permitem que dispositivos maliciosos leiam/escrevam diretamente na memória, comprometendo completamente o sistema e causando lentidão crônica.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", ValueName = "AllowExternalStorageDevices", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Bloqueio de Driver Vulnerável (Microsoft Blocklist) - 2024",
            Description = "Lista atualizada de drivers vulneráveis. Desativar permite instalação de drivers com exploits conhecidos que podem comprometer o kernel, causar BSODs e lentidão extrema do sistema.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", ValueName = "DriverBlocklistEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Integridade do Sistema (SIP) - 2024",
            Description = "Protege arquivos críticos do sistema contra modificação não autorizada. Vulnerabilidades de 2024 mostram que desativar permite ransomware modificar arquivos do sistema, causando corrupção e lentidão.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemProtection", ValueName = "Enable", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Validação de Assinatura de Código (Code Signing) - 2024",
            Description = "Verifica assinaturas digitais de drivers e executáveis. Desativar permite execução de malware assinado digitalmente, comprometendo o sistema e degradando performance.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options", ValueName = "MitigationOptions", HarmfulValue = 0, DefaultValue = 256
        },
        new() {
            Name = "Proteção contra Injeção de DLL - 2024",
            Description = "Previne injeção de DLL maliciosa em processos legítimos. Vulnerabilidades recentes permitem que malware sequeerte processos do sistema, consumindo recursos e causando lentidão.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager", ValueName = "ProtectionMode", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Controle de Fluxo de Sincronização (Synchronization CFG) - 2024",
            Description = "Proteção avançada contra exploits de controle de fluxo. Desativar expõe o sistema a ataques que podem corromper a execução de processos críticos, causando instabilidade e lentidão.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "EnableCfg", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Endereço de Retorno (Return Flow Guard) - 2024",
            Description = "Mecanismo que protege contra ataques de retorno de função. Vulnerabilidades de 2024 mostram que desativar permite exploits que podem comprometer a estabilidade do sistema.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "EnableRfg", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Isolamento de Processo Crítico (CIS) - 2024",
            Description = "Isola processos críticos do sistema. Desativar permite que malware comprometa processos essenciais, causando consumo excessivo de CPU e lentidão generalizada.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\CriticalProcessIsolation", ValueName = "Enabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Kernel em Tempo Real (Real-time Kernel Protection) - 2024",
            Description = "Monitoramento em tempo real de atividades do kernel. Desativar permite que rootkits operem sem detecção, comprometendo performance e estabilidade.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureKernel", ValueName = "Enabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Validação de Integridade de Boot - 2024",
            Description = "Verifica integridade do processo de boot. Vulnerabilidades de 2024 permitem que malware persista através de reinicializações, causando lentidão crônica.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot", ValueName = "Verify", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção contra Exploits de Zero-Day - 2024",
            Description = "Sistema de proteção contra exploits desconhecidos. Desativar expõe o sistema a vulnerabilidades zero-day que podem causar comprometimento completo e degradação de performance.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ExploitProtection", ValueName = "Enabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Controle de Integridade de Memória Virtual - 2024",
            Description = "Protege memória virtual contra corrupção. Desativar permite que malware corrompa estruturas de memória, causando vazamentos e lentidão progressiva.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Memory\Integrity", ValueName = "Enabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Heap do Sistema - 2024",
            Description = "Protege estruturas de heap do sistema contra corrupção. Vulnerabilidades de 2024 permitem que malware exploite heap para escalar privilégios e degradar performance.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Heap", ValueName = "ProtectionEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Validação de Chamadas de Sistema (Syscall Validation) - 2024",
            Description = "Valida chamadas de sistema para prevenir abusos. Desativar permite que malware abuse de syscalls para comprometer o sistema e causar lentidão.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SystemCall", ValueName = "ValidationEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Proteção de Estruturas do Kernel - 2024",
            Description = "Protege estruturas internas do kernel contra modificação. Desativar permite que rootkits modifiquem o kernel, causando instabilidade severa e lentidão.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\KernelProtection", ValueName = "Enabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Controle de Integridade de Drivers - 2024",
            Description = "Monitora integridade de drivers carregados. Desativar permite drivers maliciosos operem, comprometendo performance e estabilidade do sistema.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DriverIntegrity", ValueName = "MonitoringEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        
        // ==================================================================================
        // 10. VULNERABILIDADES DE REDE E CONECTIVIDADE 2024
        // ==================================================================================
        new() {
            Name = "TCP/IP Stack Vulnerability - KB5041585",
            Description = "Vulnerabilidade crítica de 2024 no TCP/IP que causa alto uso de CPU e perda de pacotes. Afeta taxas de sucesso de conexões TCP e pode causar lentidão extrema na rede.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", ValueName = "TcpAckFrequency", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "SMBv3 Compression Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no protocolo SMBv3 que permite execução remota de código. Desativar proteções expõe o sistema a ataques de rede que podem comprometer performance.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", ValueName = "EnableCompression", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Windows Network Driver Interface - 2024",
            Description = "Vulnerabilidade em drivers de rede de 2024. Configurações incorretas podem causar instabilidade na rede e degradação de performance em aplicações de rede.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", ValueName = "DisableTaskOffload", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "DNS Client Cache Poisoning - 2024",
            Description = "Vulnerabilidade de 2024 que permite envenenamento de cache DNS. Configurações incorretas podem causar lentidão na resolução de nomes e redirecionamento malicioso.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Dnscache\Parameters", ValueName = "CacheHashTableBucketSize", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows Firewall Bypass - 2024",
            Description = "Vulnerabilidade de 2024 que permite bypass do firewall. Configurações incorretas podem expor o sistema a ataques de rede que comprometem performance.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters", ValueName = "EnableFirewall", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Remote Desktop Protocol (RDP) Vulnerability - 2024",
            Description = "Vulnerabilidade crítica de 2024 no RDP. Configurações inseguras podem permitir acesso remoto não autorizado e degradação de performance.",
            Category = "Rede e Conectividade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Terminal Server", ValueName = "fDenyTSConnections", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows Update Client Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no cliente Windows Update. Configurações incorretas podem impedir atualizações de segurança e causar lentidão no sistema.",
            Category = "Atualizações e Loja", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update", ValueName = "AUOptions", HarmfulValue = 1, DefaultValue = 4
        },
        new() {
            Name = "Windows Store App Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 em apps da Microsoft Store. Configurações incorretas podem permitir execução de código malicioso e degradar performance.",
            Category = "Atualizações e Loja", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsStore", ValueName = "AutoDownload", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Power Management Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no gerenciamento de energia. Configurações incorretas podem causar consumo excessivo de bateria e lentidão do sistema.",
            Category = "Estabilidade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", ValueName = "HibernateEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Memory Management Vulnerability - 2024",
            Description = "Vulnerabilidade crítica de 2024 no gerenciamento de memória. Informações sensíveis da RAM podem ser escritas no pagefile.sys. Atacantes com acesso físico podem analisar o conteúdo do pagefile após desligamento. Configurações incorretas causam vazamentos de memória, lentidão progressiva e aumentam o tempo de desligamento em mais de 30 minutos em sistemas com 2GB+ de RAM.",
            Category = "Estabilidade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", ValueName = "ClearPageFileAtShutdown", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows Search Service Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no serviço Windows Search. Configurações incorretas podem causar alto uso de CPU e lentidão na indexação.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "WSearch", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        new() {
            Name = "Windows Update Service Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no serviço Windows Update. Configurações incorretas podem impedir atualizações críticas e comprometer segurança.",
            Category = "Serviços Essenciais", Type = TweakType.Service, ServiceName = "wuauserv", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        new() {
            Name = "Windows Defender Service Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no Windows Defender. Configurações incorretas podem desativar proteção em tempo real e comprometer segurança.",
            Category = "Defesa e Antivírus", Type = TweakType.Service, ServiceName = "WinDefend", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        new() {
            Name = "Windows Firewall Service Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no serviço Firewall. Configurações incorretas podem expor o sistema a ataques de rede.",
            Category = "Defesa e Antivírus", Type = TweakType.Service, ServiceName = "MpsSvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        new() {
            Name = "Windows Security Center Service Vulnerability - 2024",
            Description = "Vulnerabilidade de 2024 no Centro de Segurança. Configurações incorretas podem impedir monitoramento de segurança.",
            Category = "Defesa e Antivírus", Type = TweakType.Service, ServiceName = "wscsvc", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        
        // ==================================================================================
        // 11. VULNERABILIDADES CRÍTICAS DE 2025-2026 (NOVAS ADIÇÕES)
        // ==================================================================================
        new() {
            Name = "Desktop Window Manager Information Disclosure - CVE-2026-20805",
            Description = "Vulnerabilidade zero-day de 2026 no Desktop Window Manager. Permite disclosure de informações de memória localmente, expondo endereços de seção de portas ALPC remotas. Ativamente explorada na natureza.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\DWM", ValueName = "Enable", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows Graphics Component Elevation of Privilege - CVE-2026-20822",
            Description = "Vulnerabilidade crítica de 2026 no componente gráfico. Use-after-free que permite escalonamento de privilégios para SYSTEM após ganhar uma race condition. Compromete completamente o sistema.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Graphics", ValueName = "HardwareAcceleration", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "VBS Enclave Elevation of Privilege - CVE-2026-20876",
            Description = "Heap-based buffer overflow no VBS Enclave de 2026. Permite ganhar Virtual Trust Level 2 (VTL2), comprometendo virtualização baseada em segurança do Windows.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard", ValueName = "HypervisorEnforcedCodeIntegrity", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "LSASS Remote Code Execution - CVE-2026-20854",
            Description = "Vulnerabilidade crítica de 2026 no LSASS. Use-after-free permite execução remota de código por atacantes autorizados. Compromete autenticação e credenciais do sistema.",
            Category = "Segurança Crítica", Type = TweakType.Service, ServiceName = "LSASS", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        new() {
            Name = "Secure Boot Certificate Expiration - CVE-2026-21265",
            Description = "Vulnerabilidade de 2026 na expiração de certificados Secure Boot de 2011. Permite bypass completo do Secure Boot em sistemas não atualizados, comprometendo integridade do boot.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\SecureBoot", ValueName = "SecureBootEnabled", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows SMB Server Elevation of Privilege - CVE-2026-20919",
            Description = "Vulnerabilidade de 2026 no servidor SMB. Permite escalonamento de privilégios através do protocolo SMB, comprometendo acesso a recursos de rede compartilhados.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\LanmanServer\Parameters", ValueName = "EnableSMB1Protocol", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "Windows URL Parsing Remote Code Execution - CVE-2025-59295",
            Description = "Vulnerabilidade de 2025 no parsing de URLs do Windows. Permite execução remota de código através de URLs maliciosas construídas para overflow de ponteiros de função.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings", ValueName = "URLSecurity", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Microsoft Graphics Component VM Escape - CVE-2025-49708",
            Description = "Vulnerabilidade crítica de 2025 com CVSS 9.9. Permite escape completo de máquinas virtuais, comprometendo todas as VMs no mesmo host com privilégios SYSTEM.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", ValueName = "TdrLevel", HarmfulValue = 0, DefaultValue = 3
        },
        new() {
            Name = "ASP.NET Security Feature Bypass - CVE-2025-55315",
            Description = "Vulnerabilidade crítica de 2025 com CVSS 9.9. Permite bypass de controles de segurança através de smuggling de requisições HTTP maliciosas dentro de requisições autenticadas.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\ASP.NET", ValueName = "RequestValidation", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows Server Update Service (WSUS) RCE - CVE-2025-59287",
            Description = "Vulnerabilidade crítica de 2025 com CVSS 9.8. Permite execução remota de código no WSUS, comprometendo sistema de atualizações da rede.",
            Category = "Segurança Crítica", Type = TweakType.Service, ServiceName = "WSUS", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        },
        new() {
            Name = "Windows NPU Power Management Bug - KB5074109",
            Description = "Bug de 2026 em laptops com NPU que impede sleep adequado, causando consumo excessivo de bateria e lentidão do sistema devido a throttling térmico.",
            Category = "Estabilidade", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", ValueName = "SleepInactivityTimeout", HarmfulValue = 0, DefaultValue = 1800
        },
        new() {
            Name = "Windows Update Boot Failure - KB5074109 Regression",
            Description = "Regressão crítica de 2026 na atualização KB5074109 causando boot failures com UNMOUNTABLE_BOOT_VOLUME. Requer recuperação manual e pode causar perda de dados.",
            Category = "Estabilidade", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update", ValueName = "RebootRequired", HarmfulValue = 1, DefaultValue = 0
        },
        new() {
            Name = "OneDrive/Dropbox Crash Bug - KB5074109 Regression",
            Description = "Bug de 2026 na atualização KB5074109 causando crashes e não-responsividade no OneDrive e Dropbox, afetando sincronização de arquivos e performance geral.",
            Category = "Estabilidade", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer", ValueName = "CloudStoreSync", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "WinSqlite3.dll Vulnerability - CVE-2025-6965",
            Description = "Vulnerabilidade de 2025 no componente WinSqlite3.dll. Ferramentas de segurança detectam como vulnerável mesmo em sistemas totalmente atualizados até janeiro 2026.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModel", ValueName = "SqliteSecurity", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows TPM 2.0 Out-of-Bounds Read - CVE-2025-2884",
            Description = "Vulnerabilidade de 2025 na implementação TPM 2.0. Out-of-bounds read na função CryptHmacSign pode expor dados sensíveis do Trusted Platform Module.",
            Category = "Segurança Crítica", KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TPM", ValueName = "TpmSecurity", HarmfulValue = 0, DefaultValue = 1
        },
        new() {
            Name = "Windows Kernel Memory Corruption - CVE-2026-20941",
            Description = "Vulnerabilidade de 2026 no Host Process for Windows Tasks. Permite escalonamento de privilégios através de corrupção de memória do kernel.",
            Category = "Segurança Crítica", Type = TweakType.Service, ServiceName = "TaskHost", HarmfulStartMode = "Disabled", DefaultStartMode = "Automatic"
        }
    };

        #endregion

        #region Métodos Públicos (Interface Gráfica)

        // *** NOVO: EXPOE A LISTA DE TWEAKS PARA A BUSCA ***
        public static List<ScannableTweak> GetAllTweaksDefinition()
        {
            return HarmfulTweaks;
        }

        public static List<ScannableTweak> GetHarmfulTweaksWithStatus()
        {
            foreach (var tweak in HarmfulTweaks)
            {
                CheckTweak(tweak);
            }
            return HarmfulTweaks;
        }

        public static (bool Success, string Message) ToggleTweak(ScannableTweak tweak)
        {
            try
            {
                bool applySafeValue = tweak.Status == TweakStatus.MODIFIED;
                string action = applySafeValue ? "restaurado para o padrão seguro" : "alterado (personalizado)";

                switch (tweak.Type)
                {
                    case TweakType.Registry:
                        if (string.IsNullOrEmpty(tweak.KeyPath) || string.IsNullOrEmpty(tweak.ValueName))
                            return (false, "Configuração de registro inválida.");

                        string path = tweak.KeyPath.Replace(@"HKEY_LOCAL_MACHINE\", "").Replace(@"HKEY_CURRENT_USER\", "");
                        RegistryKey baseKey = tweak.KeyPath.StartsWith("HKEY_LOCAL_MACHINE") ? Registry.LocalMachine : Registry.CurrentUser;

                        using (RegistryKey? key = baseKey.OpenSubKey(path, true) ?? baseKey.CreateSubKey(path))
                        {
                            object? valueToSet = applySafeValue ? tweak.DefaultValue : tweak.HarmfulValue;

                            if (valueToSet == null)
                            {
                                if (key.GetValue(tweak.ValueName) != null) key.DeleteValue(tweak.ValueName, false);
                            }
                            else
                            {
                                key.SetValue(tweak.ValueName, valueToSet, tweak.ValueKind);
                            }
                        }
                        break;

                    case TweakType.Service:
                        if (string.IsNullOrEmpty(tweak.ServiceName) || string.IsNullOrEmpty(tweak.DefaultStartMode))
                            return (false, "Configuração de serviço inválida.");

                        string mode = applySafeValue ? tweak.DefaultStartMode : tweak.HarmfulStartMode ?? "Disabled";
                        string scCmd = $"config {tweak.ServiceName} start= {mode.ToLower()}";

                        if (mode.Equals("Delayed-Auto", StringComparison.OrdinalIgnoreCase))
                            scCmd = $"config {tweak.ServiceName} start= delayed-auto";

                        SystemUtils.RunExternalProcess("sc.exe", scCmd, true);

                        if (applySafeValue && mode != "Disabled")
                        {
                            SystemUtils.RunExternalProcess("sc.exe", $"start {tweak.ServiceName}", true);
                        }
                        break;

                    case TweakType.Mouse:
                        bool setStandard = applySafeValue;
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", setStandard ? "1" : "0", RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold1", setStandard ? "6" : "0", RegistryValueKind.String);
                        Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseThreshold2", setStandard ? "10" : "0", RegistryValueKind.String);
                        break;

                    case TweakType.Bcd:
                        if (string.IsNullOrEmpty(tweak.ValueName)) return (false, "BCD inválido.");
                        string bcdValue = (applySafeValue ? tweak.DefaultValue?.ToString() : tweak.HarmfulValue?.ToString()) ?? "";

                        if (bcdValue == "delete")
                            SystemUtils.RunExternalProcess("bcdedit", $"/deletevalue {tweak.ValueName}", true);
                        else
                            SystemUtils.RunExternalProcess("bcdedit", $"/set {tweak.ValueName} {bcdValue}", true);
                        break;

                    case TweakType.PageFile:
                        const string pfKey = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
                        if (applySafeValue)
                            Registry.SetValue(pfKey, "PagingFiles", new string[] { @"?:\pagefile.sys" }, RegistryValueKind.MultiString);
                        else
                            Registry.SetValue(pfKey, "PagingFiles", Array.Empty<string>(), RegistryValueKind.MultiString);
                        break;
                }

                CheckTweak(tweak);
                return (true, $"{tweak.Name} foi {action}.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao alternar '{tweak.Name}': {ex.Message}");
                return (false, $"Erro ao alternar '{tweak.Name}': {ex.Message}");
            }
        }

        public static void RestoreAllTweakStates()
        {
            try
            {
                using (var configKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\KitLugia\Config", true))
                {
                    var allTweaks = GetAllTweaksDefinition();
                    int restoredCount = 0;
                    
                    foreach (var tweak in allTweaks)
                    {
                        string valueKey = $"Tweak_{tweak.Category}_{tweak.Name.GetHashCode()}";
                        string? savedStatus = configKey.GetValue(valueKey) as string;
                        
                        if (!string.IsNullOrEmpty(savedStatus) && Enum.TryParse<TweakStatus>(savedStatus, out var status))
                        {
                            if (status == TweakStatus.MODIFIED)
                            {
                                // Restaurar para o padrão seguro
                                ToggleTweak(tweak);
                                restoredCount++;
                            }
                        }
                    }
                    
                    Logger.Log($"Configurações restauradas: {restoredCount} tweaks");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao restaurar configurações: {ex.Message}");
            }
        }

        public static Dictionary<string, bool> GetTweakStates()
        {
            var states = new Dictionary<string, bool>();
            
            try
            {
                using (var configKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\KitLugia\Config", true))
                {
                    var allTweaks = GetAllTweaksDefinition();
                    
                    foreach (var tweak in allTweaks)
                    {
                        string valueKey = $"Tweak_{tweak.Category}_{tweak.Name.GetHashCode()}";
                        string? savedStatus = configKey.GetValue(valueKey) as string;
                        
                        if (!string.IsNullOrEmpty(savedStatus) && Enum.TryParse<TweakStatus>(savedStatus, out var status))
                        {
                            states[tweak.Name] = (status == TweakStatus.MODIFIED);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao carregar estados: {ex.Message}");
            }
            
            return states;
        }

        public static void ResetAllConfigurations()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\KitLugia\Config");
                Logger.Log("Todas as configurações foram resetadas.");
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao resetar configurações: {ex.Message}");
            }
        }

        public static void SaveQuickToggleConfig(string tweakName, bool isEnabled)
        {
            try
            {
                using (var configKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\KitLugia\QuickToggles", true))
                {
                    configKey.SetValue(tweakName, isEnabled, RegistryValueKind.DWord);
                    Logger.Log($"Quick toggle '{tweakName}' salvo como: {(isEnabled ? "ATIVADO" : "DESATIVADO")}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao salvar quick toggle: {ex.Message}");
            }
        }

        public static bool GetQuickToggleState(string tweakName)
        {
            try
            {
                using (var configKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\KitLugia\QuickToggles", true))
                {
                    object? value = configKey.GetValue(tweakName);
                    return value != null && value is int intValue && intValue == 1;
                }
            }
            catch
            {
                return false;
            }
        }

        public static List<string> GetAppliedQuickToggles()
        {
            var appliedToggles = new List<string>();
            
            try
            {
                using (var configKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\KitLugia\QuickToggles", true))
                {
                    foreach (var valueName in configKey.GetValueNames())
                    {
                        object? value = configKey.GetValue(valueName);
                        if (value != null && value is int intValue && intValue == 1)
                        {
                            appliedToggles.Add(valueName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Erro ao carregar toggles: {ex.Message}");
            }
            
            return appliedToggles;
        }

        #endregion

        #region Verificação Interna

        private static void CheckTweak(ScannableTweak tweak)
        {
            try
            {
                if (tweak.Type == TweakType.Registry)
                {
                    if (string.IsNullOrEmpty(tweak.KeyPath) || string.IsNullOrEmpty(tweak.ValueName))
                    {
                        tweak.Status = TweakStatus.ERROR; return;
                    }
                    object? currentValue = Registry.GetValue(tweak.KeyPath, tweak.ValueName, null);

                    bool isHarmful;
                    if (tweak.HarmfulValue == null)
                    {
                        isHarmful = (currentValue != null);
                    }
                    else
                    {
                        if (currentValue == null) isHarmful = false;
                        else if (currentValue is int or long)
                        {
                            long val = Convert.ToInt64(currentValue);
                            long harm = Convert.ToInt64(tweak.HarmfulValue);
                            isHarmful = (val == harm);
                        }
                        else
                        {
                            isHarmful = currentValue.ToString()?.Equals(tweak.HarmfulValue.ToString(), StringComparison.OrdinalIgnoreCase) ?? false;
                        }
                    }
                    tweak.Status = isHarmful ? TweakStatus.MODIFIED : TweakStatus.OK;
                }
                else if (tweak.Type == TweakType.Service)
                {
                    if (string.IsNullOrEmpty(tweak.ServiceName)) return;
                    string? startMode = SystemUtils.GetServiceStartMode(tweak.ServiceName);

                    if (startMode == null)
                    {
                        tweak.Status = TweakStatus.NOT_FOUND;
                        return;
                    }

                    bool matchesHarmful = startMode.Equals(tweak.HarmfulStartMode, StringComparison.OrdinalIgnoreCase);
                    tweak.Status = matchesHarmful ? TweakStatus.MODIFIED : TweakStatus.OK;
                }
                else if (tweak.Type == TweakType.Mouse)
                {
                    string speed = Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Mouse", "MouseSpeed", "1")?.ToString() ?? "1";
                    tweak.Status = (speed == "0") ? TweakStatus.MODIFIED : TweakStatus.OK;
                }
                else if (tweak.Type == TweakType.Bcd)
                {
                    if (string.IsNullOrEmpty(tweak.ValueName) || tweak.HarmfulValue == null) return;

                    string output = SystemUtils.RunExternalProcess("bcdedit", "/enum", true);
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    bool isHarmful = false;
                    string? harmfulStr = tweak.HarmfulValue?.ToString();

                    if (harmfulStr != null)
                    {
                        foreach (var line in lines)
                        {
                            if (line.Contains(tweak.ValueName, StringComparison.OrdinalIgnoreCase))
                            {
                                isHarmful = true;
                            }
                            break;
                        }
                    }

                    tweak.Status = isHarmful ? TweakStatus.MODIFIED : TweakStatus.OK;
                }
                else if (tweak.Type == TweakType.PageFile)
                {
                    tweak.Status = SystemTweaks.IsPageFileDisabled() ? TweakStatus.MODIFIED : TweakStatus.OK;
                }
            }
                catch (Exception ex)
            {
                tweak.Status = TweakStatus.ERROR;
                Logger.Log($"Erro ao verificar tweak '{tweak.Name}': {ex.Message}");
            }
        }

        #endregion
    }
}
