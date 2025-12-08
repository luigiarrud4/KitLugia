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
        /// Ponto de entrada principal para a UI.
        /// </summary>
        /// <param name="provider">O nome do provedor: "CLOUDFLARE", "GOOGLE", ou "DHCP".</param>
        public static (bool Success, string Message) SetDns(string provider)
        {
            // Validação de permissão é a primeira e mais importante etapa.
            if (!SystemUtils.IsAdmin())
            {
                return (false, "Acesso Negado!\n\nPara alterar as configurações de DNS, o KitLugia precisa ser executado como Administrador.");
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
        /// Helper interno que executa os comandos PowerShell para aplicar as configurações de DNS.
        /// </summary>
        private static (bool Success, string Message) SetDnsServers(string provider, string? primaryDns, string? secondaryDns)
        {
            try
            {
                string psScript;
                // Este script robusto foca apenas em adaptadores de rede físicos e ativos.
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

                // Executa o comando e captura a saída para análise de erro.
                string result = SystemUtils.RunExternalProcess("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"", hidden: true);

                // Análise detalhada da resposta do PowerShell.
                if (result.Contains("PermissionDenied", StringComparison.OrdinalIgnoreCase) || result.Contains("Acesso a um recurso CIM não estava disponível", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"O PowerShell retornou um erro de permissão.\n\nDetalhes:\n{result}");
                }
                if (result.Contains("Erro", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"O PowerShell retornou um erro inesperado: {result}");
                }

                FlushDnsCache();
                string successMessage = provider == "DHCP"
                    ? "DNS revertido para Automático (DHCP) com sucesso."
                    : $"DNS {provider} aplicado com sucesso.";
                return (true, successMessage);
            }
            catch (Exception ex)
            {
                return (false, $"Ocorreu um erro inesperado: {ex.Message}");
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
        /// Obtém informações sobre o DNS configurado na interface de rede principal (física e ativa).
        /// Esta é a versão definitiva que ignora adaptadores virtuais.
        /// </summary>
        public static (string Provider, string DnsIp) GetActiveDnsInfo()
        {
            try
            {
                // Lista de palavras-chave para identificar e ignorar adaptadores virtuais.
                var virtualKeywords = new List<string> { "virtual", "vpn", "loopback", "tap", "hyper-v", "vmware", "vbox", "wsl", "docker" };

                // Lógica de busca refinada para encontrar a conexão de internet "real".
                var activeInterface = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(i =>
                        // 1. A interface deve estar conectada e funcionando.
                        i.OperationalStatus == OperationalStatus.Up &&
                        // 2. Deve ser uma placa de rede principal (Ethernet ou Wi-Fi).
                        (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                        // 3. Essencial: deve ter um gateway (apontando para o seu roteador). Conexões sem gateway não são a internet principal.
                        i.GetIPProperties().GatewayAddresses.Any() &&
                        // 4. Sua descrição NÃO PODE conter nenhuma palavra-chave de adaptador virtual.
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
            catch (Exception)
            {
                // Ignora falhas de leitura, retornando o valor padrão.
            }

            // Se nenhuma interface válida for encontrada, assume DHCP.
            return ("Automático (DHCP)", "N/A");
        }
    }
}