using System.Windows;
using Application = System.Windows.Application;
using System;
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

            base.OnStartup(e);

            var mainWindow = new MainWindow();
            
            // Só exibe a janela principal se não tiver o argumento --tray
            if (!StartMinimized)
            {
                mainWindow.Show();
            }
        }
    }
}