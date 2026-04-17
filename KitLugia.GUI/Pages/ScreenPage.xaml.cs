using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KitLugia.Core;
using Application = System.Windows.Application;

namespace KitLugia.GUI.Pages
{
    public partial class ScreenPage : Page
    {
        private const string DefaultProfileName = "Lugia_ColorProfile.json";
        private readonly string _defaultPath;

        public ScreenPage()
        {
            InitializeComponent();
            // Salva na pasta Documentos
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KitLugia");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            _defaultPath = Path.Combine(folder, DefaultProfileName);

            LoadInfo();
            CheckExistingProfile();
            // 🔥 LIMPEZA: Liberar recursos ao sair da página
            this.Unloaded += ScreenPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            this.Unloaded -= ScreenPage_Unloaded;
        }

        private void ScreenPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private void LoadInfo()
        {
            try
            {
                TxtResInfo.Text = $"Resolução Atual: {DisplayManager.GetCurrentResolutionInfo()}";
            }
            catch { TxtResInfo.Text = "Monitor Genérico"; }
        }

        private void CheckExistingProfile()
        {
            if (File.Exists(_defaultPath))
            {
                TxtProfileStatus.Text = $"Último perfil salvo em: {File.GetLastWriteTime(_defaultPath)}";
                TxtProfileStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            var result = DisplayManager.SaveColorProfile("UserBackup", _defaultPath);
            if (result.Success)
            {
                mw.ShowSuccess("SALVO", "Calibragem de cores salva com sucesso!");
                CheckExistingProfile();
            }
            else
            {
                mw.ShowError("ERRO", result.Message);
            }
        }

        private void BtnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            if (!File.Exists(_defaultPath))
            {
                mw.ShowError("NÃO ENCONTRADO", "Salve um perfil primeiro antes de tentar restaurar.");
                return;
            }

            var result = DisplayManager.RestoreColorProfile(_defaultPath);
            if (result.Success)
                mw.ShowSuccess("RESTAURADO", "Perfil de cores aplicado.");
            else
                mw.ShowError("ERRO", result.Message);
        }

        private void BtnResetProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            // Zera as cores para o padrão Linear (Remove o estouro)
            var result = DisplayManager.ResetColorProfileToDefault();

            if (result.Success)
            {
                mw.ShowSuccess("RESETADO", "As cores foram limpas para o padrão do Windows.");
            }
            else
            {
                mw.ShowError("ERRO", result.Message);
            }
        }

        private async void BtnFixColorConflict_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            mw.ShowInfo("PROCESSANDO", "Parando serviços de cor conflitantes...");

            // Roda em background para não travar a UI enquanto reinicia o serviço
            var result = await System.Threading.Tasks.Task.Run(() => DisplayManager.FixColorConflict());

            if (result.Success)
            {
                mw.ShowSuccess("CONCLUÍDO", result.Message);

                if (await mw.ShowConfirmationDialog("Deseja abrir o Gerenciador de Cores do Windows para verificar perfis antigos (ICC)?"))
                {
                    DisplayManager.OpenWindowsColorManagement();
                }
            }
            else
            {
                mw.ShowError("ERRO", result.Message);
            }
        }

        private async void BtnRestartDriver_Click(object sender, RoutedEventArgs e)
        {
            if (!(Application.Current.MainWindow is MainWindow mw)) return;

            if (await mw.ShowConfirmationDialog("Isso irá reiniciar o driver gráfico.\nA tela piscará e pode ficar preta por alguns segundos.\n\nDeseja continuar?"))
            {
                mw.ShowInfo("AGUARDE", "Reiniciando driver de vídeo...");
                var result = await DisplayManager.RestartGraphicsDriver();

                if (result.Success) mw.ShowSuccess("SUCESSO", result.Message);
                else mw.ShowError("FALHA", result.Message);
            }
        }

        private void BtnNvidia_Click(object sender, RoutedEventArgs e)
        {
            DisplayManager.OpenNvidiaControlPanel();
        }

        private void BtnWinColor_Click(object sender, RoutedEventArgs e)
        {
            DisplayManager.OpenWindowsColorManagement();
        }
    }
}