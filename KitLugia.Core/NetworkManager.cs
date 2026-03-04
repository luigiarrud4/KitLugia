using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        /// <summary>
        /// Define o servidor DNS para um provedor específico (Cloudflare, Google) ou reverte para DHCP.
        /// </summary>
        public static (bool Success, string Message) SetDns(string provider)
        {
            if (!SystemUtils.IsAdmin())
            {
                return (false, "Acesso Negado!\nExecute como Administrador para alterar o DNS.");
            }

            switch (provider.ToUpper())
            {
                case "CLOUDFLARE":
                    return SetDnsServers("Cloudflare", "1.1.1.1", "1.0.0.1");
                case "GOOGLE":
                    return SetDnsServers("Google", "8.8.8.8", "8.8.4.4");
                case "DHCP":
                    return SetDnsServers("DHCP", null, null);
                default:
                    return (false, "Provedor de DNS desconhecido.");
            }
        }

        /// <summary>
        /// Configura o algoritmo de controle de congestionamento TCP para CTCP (Compound TCP).
        /// O padrão (CUBIC) foca em largura de banda. CTCP foca em manter a janela de transmissão estável,
        /// reduzindo a perda de pacotes e picos de lag em conexões instáveis.
        /// </summary>
        public static (bool Success, string Message) ApplyLatencyCongestionControl()
        {
            try
            {
                // 1. Desativa a heurística do Windows (auto-ajustes antigos que causam instabilidade)
                SystemUtils.RunExternalProcess("netsh", "int tcp set heuristics disabled", hidden: true);

                // 2. Define CTCP como provedor de congestionamento (Ideal para jogos/VoIP)
                SystemUtils.RunExternalProcess("netsh", "int tcp set supplemental template=internet congestionprovider=ctcp", hidden: true);

                // 3. Limita o autotuning para 'normal'. 'Disabled' limita a velocidade, 'Normal' é o equilíbrio.
                SystemUtils.RunExternalProcess("netsh", "int tcp set global autotuninglevel=normal", hidden: true);

                // 4. Desativa ECN (Explicit Congestion Notification). Roteadores antigos dropam pacotes com isso.
                SystemUtils.RunExternalProcess("netsh", "int tcp set global ecncapability=disabled", hidden: true);

                // 5. Desativa RSC (Receive Segment Coalescing). 
                // CRÍTICO: RSC agrupa pacotes na placa de rede para economizar CPU, mas aumenta o ping.
                SystemUtils.RunExternalProcess("netsh", "int tcp set global rsc=disabled", hidden: true);

                // 6. Desativa TCP Chimney Offload (Processamento na NIC que às vezes falha)
                SystemUtils.RunExternalProcess("netsh", "int tcp set global chimney=disabled", hidden: true);

                // 7. Desativa Timestamps (Reduz overhead do cabeçalho TCP - ganho marginal de latência)
                SystemUtils.RunExternalProcess("netsh", "int tcp set global timestamps=disabled", hidden: true);

                return (true, "Algoritmo TCP ajustado para CTCP e RSC desativado para menor jitter.");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao aplicar otimização TCP (CTCP): {ex.Message}");
            }
        }

        /// <summary>
        /// Helper interno que executa os comandos PowerShell para aplicar as configurações de DNS.
        /// </summary>
        private static (bool Success, string Message) SetDnsServers(string provider, string? primaryDns, string? secondaryDns)
        {
            try
            {
                string psScript;
                // Busca apenas interfaces físicas ativas, ignorando VPNs e virtuais.
                string findInterfacesPart =
                    "$interfaces = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.Virtual -eq $false };";

                if (provider == "DHCP")
                {
                    psScript = $"{findInterfacesPart} foreach ($if in $interfaces) {{ Set-DnsClientServerAddress -InterfaceIndex $if.InterfaceIndex -ResetServerAddresses -Confirm:$false }}";
                }
                else
                {
                    string ipArray = $"('{primaryDns}', '{secondaryDns}')";
                    psScript = $"{findInterfacesPart} foreach ($if in $interfaces) {{ Set-DnsClientServerAddress -InterfaceIndex $if.InterfaceIndex -ServerAddresses {ipArray} -Confirm:$false }}";
                }

                string result = SystemUtils.RunExternalProcess("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"", hidden: true);

                if (result.Contains("PermissionDenied") || result.Contains("Erro"))
                {
                    return (false, $"Erro ao configurar DNS: {result}");
                }

                FlushDnsCache();
                string successMessage = provider == "DHCP"
                    ? "DNS revertido para Automático (DHCP) com sucesso."
                    : $"DNS {provider} aplicado com sucesso.";
                return (true, successMessage);
            }
            catch (Exception ex)
            {
                return (false, $"Erro inesperado: {ex.Message}");
            }
        }

        /// <summary>
        /// Limpa o cache de resolução de DNS do Windows.
        /// </summary>
        public static (bool Success, string Message) FlushDnsCache()
        {
            try
            {
                SystemUtils.RunExternalProcess("ipconfig", "/flushdns", hidden: true);
                return (true, "Cache de DNS limpo com sucesso!");
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao limpar cache de DNS: {ex.Message}");
            }
        }

        /// <summary>
        /// Obtém informações sobre o DNS configurado na interface de rede principal.
        /// </summary>
        public static (string Provider, string DnsIp) GetActiveDnsInfo()
        {
            try
            {
                var virtualKeywords = new List<string> { "virtual", "vpn", "loopback", "tap", "hyper-v", "vmware", "vbox", "wsl", "docker" };

                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(i =>
                        i.OperationalStatus == OperationalStatus.Up &&
                        (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                        i.GetIPProperties().GatewayAddresses.Any() &&
                        !virtualKeywords.Any(keyword => i.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    );

                if (activeInterface != null)
                {
                    var dnsServers = activeInterface.GetIPProperties().DnsAddresses;
                    if (dnsServers.Any())
                    {
                        string firstDns = dnsServers.First().ToString();
                        if (firstDns == "1.1.1.1" || firstDns == "1.0.0.1") return ("Cloudflare", firstDns);
                        if (firstDns == "8.8.8.8" || firstDns == "8.8.4.4") return ("Google", firstDns);
                        return ("Personalizado", firstDns);
                    }
                }
            }
            catch { /* Falha silenciosa na leitura */ }

            return ("Automático (DHCP)", "N/A");
        }
    }
}