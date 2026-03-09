using System.Windows;
using Application = System.Windows.Application;
using System;
using System.Linq; // 🔥 Adicionado para corrigir .Contains()
// A linha duplicada "using System.Windows;" foi removida daqui
using System.Windows.Interop;
using System.Windows.Media;

namespace KitLugia.GUI
{
    public partial class App : Application
    {
        public bool StartMinimized { get; set; } = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Força renderização via Software para evitar telas brancas/pretas
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            // 🔥 Log cirúrgico dos argumentos recebidos
            if (e.Args.Length > 0)
            {
                KitLugia.Core.Logger.Log($"Argumentos recebidos: {string.Join(", ", e.Args)}");
                StartMinimized = e.Args.Contains("--tray");
                KitLugia.Core.Logger.Log($"StartMinimized: {StartMinimized}");
            }

            base.OnStartup(e);

            // 🔥 Verificar e corrigir métodos de inicialização
            KitLugia.Core.StartupManager.CheckAndFixStartupMethods();

            var mainWindow = new MainWindow();
            
            // Só exibe a janela principal se não tiver o argumento --tray
            if (!StartMinimized)
            {
                mainWindow.Show();
            }
        }
    }
}
