using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KitLugia.Core;
using Microsoft.Win32;
// Resolução de Conflitos WPF vs WinForms
using MessageBox = System.Windows.MessageBox;
using Application = System.Windows.Application;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace KitLugia.GUI.Pages
{
    public partial class AdvancedToolsPage : Page
    {
        private string _selectedIsoPath = "";
        private string _mountedDrive = "";

        public AdvancedToolsPage()
        {
            InitializeComponent();
        }

        // ==========================================
        // ISO MANAGER (WINHANCE) - Mantido intocado
        // ==========================================
        private void BtnSelectIso_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Arquivos ISO (*.iso)|*.iso" };
            if (dlg.ShowDialog() == true)
            {
                _selectedIsoPath = dlg.FileName;
                TxtIsoPath.Text = System.IO.Path.GetFileName(_selectedIsoPath);
                PanelIsoActions.Visibility = Visibility.Visible;
            }
        }

        private async void BtnMountIso_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            if (mw == null) return;

            mw.ShowInfo("MONTANDO ISO", "Aguarde, executando PowerShell...");
            var result = await IsoManager.MountIso(_selectedIsoPath);

            if (result.Success)
            {
                _mountedDrive = result.DriveLetter;
                mw.ShowSuccess("SUCESSO", $"ISO montada em: {_mountedDrive}");
                TxtMountStatus.Text = $"✅ ISO montada em: {_mountedDrive}";
                TxtIsoStatus.Text = $"✅ ISO montada com sucesso";
                TxtDriveInfo.Text = $"Letra: {_mountedDrive.Substring(0, 2)}";
                BtnMountIso.IsEnabled = false;
            }
            else
            {
                mw.ShowError("ERRO", result.Message);
            }
        }

        private void BtnInjectDrivers_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.ShowInfo("INJEÇÃO DE DRIVERS", "Para injetar drivers, utilize a ferramenta WinBoot.\nClique no botão 'Instalação via Partição' abaixo.");
        }

        private void BtnCreateIso_Click(object sender, RoutedEventArgs e)
        {
            var mw = Application.Current.MainWindow as MainWindow;
            mw?.ShowInfo("CRIAR ISO", "Para criar uma ISO personalizada, utilize ferramentas especializadas como o WinBoot.");
        }

        // ==========================================
        // WINBOOT (NO-USB)
        // ==========================================
        private void BtnWinboot_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new WinbootPage());
        }

        // ==========================================
        // GERENCIADOR DE PARTIÇÕES (Launcher)
        // ==========================================
        private void BtnOpenPartitions_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new PartitionsPage());
        }
    }
}
