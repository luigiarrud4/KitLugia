using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    [SupportedOSPlatform("windows")]
    public static partial class Toolbox
    {
        /// <summary>
        /// Obtém uma lista de todos os drivers de terceiros (não-Microsoft) instalados no sistema.
        /// </summary>
        /// <returns>Uma lista de objetos 'DriverInfo'.</returns>
        public static List<DriverInfo> GetThirdPartyDrivers()
        {
            var drivers = new List<DriverInfo>();
            try
            {
                // A query WMI busca por todos os drivers assinados cujo provedor não é 'Microsoft'.
                var query = "SELECT DeviceName, DriverProviderName, DriverVersion, DriverDate FROM Win32_PnPSignedDriver WHERE DriverProviderName != 'Microsoft'";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (var item in searcher.Get().Cast<ManagementObject>())
                {
                    using (item)
                    {
                        DateTime date = DateTime.MinValue;
                        try
                        {
                            // O WMI usa um formato de data específico (CIM_DATETIME).
                            // Este é o método mais seguro para convertê-lo para um DateTime padrão do C#.
                            string wmiDate = item["DriverDate"]?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(wmiDate))
                            {
                                date = ManagementDateTimeConverter.ToDateTime(wmiDate);
                            }
                        }
                        catch { /* Ignora erros de conversão de data, mantendo a data mínima. */ }

                        drivers.Add(new DriverInfo(
                            item["DeviceName"]?.ToString() ?? "Dispositivo Desconhecido",
                            item["DriverProviderName"]?.ToString() ?? "Provedor Desconhecido",
                            item["DriverVersion"]?.ToString() ?? "N/A",
                            date
                        ));
                    }
                }
            }
            catch (Exception)
            {
                // Se a busca WMI falhar, a UI receberá uma lista vazia e poderá notificar o usuário.
            }

            // Retorna a lista ordenada alfabeticamente pelo nome do dispositivo para melhor visualização.
            return drivers.OrderBy(d => d.DeviceName).ToList();
        }

        /// <summary>
        /// Inicia o processo de backup de todos os drivers de terceiros para uma pasta de destino.
        /// </summary>
        /// <param name="destinationPath">O caminho da pasta onde o backup será salvo.</param>
        /// <returns>Uma tupla com o status da operação e uma mensagem para o usuário.</returns>
        public static (bool Success, string Message) BackupThirdPartyDrivers(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath) || !Directory.Exists(destinationPath))
            {
                return (false, "O caminho de destino fornecido é inválido ou não existe.");
            }

            try
            {
                // O DISM (Deployment Image Servicing and Management) é a ferramenta nativa do Windows
                // para exportar drivers. Executá-lo em uma nova janela permite que o usuário veja o progresso.
                string command = $"/c dism.exe /online /export-driver /destination:\"{destinationPath}\" & pause";
                SystemUtils.RunExternalProcess("cmd.exe", command, hidden: false, waitForExit: false);

                return (true, "O processo de backup dos drivers foi iniciado em uma nova janela. Aguarde a conclusão.");
            }
            catch (Exception ex)
            {
                return (false, $"Falha ao iniciar o processo de backup: {ex.Message}");
            }
        }

        /// <summary>
        /// Abre o Gerenciador de Dispositivos do Windows (devmgmt.msc).
        /// </summary>
        public static void OpenDeviceManager()
        {
            try
            {
                SystemUtils.RunExternalProcess("devmgmt.msc", "", hidden: false, waitForExit: false);
            }
            catch
            {
                // A UI pode opcionalmente mostrar um erro se não conseguir abrir o gerenciador.
            }
        }
    }
}