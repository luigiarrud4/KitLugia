using KitLugia.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using MessageBox = System.Windows.MessageBox; // Resolve ambiguidade com WinForms
using Application = System.Windows.Application;

#pragma warning disable CS4014 // Chamadas async não aguardadas são intencionais para operações em background

namespace KitLugia.GUI.Pages
{
    public partial class PrivacyPage : Page
    {
        public ObservableCollection<PrivacyCategoryViewModel> Categories { get; set; } = new ObservableCollection<PrivacyCategoryViewModel>();
        private DispatcherTimer? _refreshTimer;

        public PrivacyPage()
        {
            InitializeComponent();
            DataContext = this;
            LoadData();
            InitializeTimer();

            // 🔥 LIMPEZA: Para timer ao sair da página
            this.Unloaded += PrivacyPage_Unloaded;
        }

        // 🔥 CORREÇÃO: Cleanup público para ser chamado via reflection pelo MainWindow
        public void Cleanup()
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;
            this.Unloaded -= PrivacyPage_Unloaded;
        }

        private void PrivacyPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Cleanup();
        }

        private void InitializeTimer()
        {
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += (s, e) => RefreshStatus();
            _refreshTimer.Start();
        }

        private void LoadData()
        {
            Categories.Clear();
            var rawCats = OOShutUpManager.GetPrivacyCategories();

            foreach (var cat in rawCats)
            {
                var vm = new PrivacyCategoryViewModel { Name = cat.Key };
                foreach (var setting in cat.Value)
                {
                    vm.Settings.Add(new PrivacySettingViewModel(setting, RefreshStatus));
                }
                Categories.Add(vm);
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            // Atualiza status de cada item (para refletir mudanças externas)
            foreach (var cat in Categories)
            {
                foreach (var setting in cat.Settings)
                {
                    setting.Refresh();
                }
            }

            // Atualiza contadores
            var allSettings = Categories.SelectMany(c => c.Settings).ToList();
            int secured = allSettings.Count(s => s.IsEnabled);
            int total = allSettings.Count;

            TxtSecureCount.Text = secured.ToString();
            TxtVulnerableCount.Text = (total - secured).ToString();
            int percent = total > 0 ? (int)((double)secured / total * 100) : 0;
            TxtPrivacyScore.Text = $"{percent}% Protegido";
        }

        // --- Event Handlers dos Botões (Mantidos para simplicidade) ---

        private void BtnRefreshStatus_Click(object sender, RoutedEventArgs e) => RefreshStatus();

        private void BtnExpandAll_Click(object sender, RoutedEventArgs e)
        {
            // Na nova UI, tudo já está expandido por padrão neste design simplificado.
            // Poderíamos adicionar lógica de expandir/colapsar nos ViewModels se necessário.
            LoadData();
        }

        private async Task ApplyPreset(OOShutUpManager.PrivacyLevel level)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                mw.ShowInfo("PRIVACIDADE", $"Aplicando preset {level}...");
                await Task.Run(() => OOShutUpManager.ApplyPreset(level));
                mw.ShowSuccess("SUCESSO", "Configurações aplicadas.");
                RefreshStatus();
            }
        }

        private void BtnApplyRecommended_Click(object sender, RoutedEventArgs e) => ApplyPreset(OOShutUpManager.PrivacyLevel.Recommended);
        private void BtnApplyLimited_Click(object sender, RoutedEventArgs e) => ApplyPreset(OOShutUpManager.PrivacyLevel.Limited);
        private void BtnApplyNotRecommended_Click(object sender, RoutedEventArgs e) => ApplyPreset(OOShutUpManager.PrivacyLevel.NotRecommended);

        private async void BtnRestoreDefault_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mw)
            {
                if (await mw.ShowConfirmationDialog("Isso reativará toda a telemetria e coleta de dados padrão do Windows.\nDeseja continuar?"))
                {
                    await Task.Run(() => OOShutUpManager.RestoreDefaults());
                    mw.ShowSuccess("RESTAURADO", "Padrões do Windows restaurados.");
                    RefreshStatus();
                }
            }
        }

        private void BtnGenerateReport_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Funcionalidade em desenvolvimento.");
        private void BtnExportConfig_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Funcionalidade em desenvolvimento.");
        private void BtnSnapshot_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Funcionalidade em desenvolvimento.");
    }

    // --- View Models ---

    public class PrivacyCategoryViewModel
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<PrivacySettingViewModel> Settings { get; set; } = new ObservableCollection<PrivacySettingViewModel>();
    }

    public class PrivacySettingViewModel : INotifyPropertyChanged
    {
        private readonly OOShutUpManager.PrivacySetting _model;
        private readonly Action _refreshCallback;
        private bool _isEnabled;

        public PrivacySettingViewModel(OOShutUpManager.PrivacySetting model, Action refreshCallback)
        {
            _model = model;
            _refreshCallback = refreshCallback;
            Refresh(); // Carrega estado inicial
        }

        public string Name => _model.Name;
        public string Description => _model.Description;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                    
                    // Aplica mudança
                    if (value) OOShutUpManager.ApplyPrivacySetting(_model);
                    else OOShutUpManager.RevertPrivacySetting(_model);
                    
                    // Notifica UI pai para atualizar contadores
                    _refreshCallback?.Invoke();
                }
            }
        }

        // Comando para checkbox (opcional, já que usamos TwoWay binding no IsEnabled)
        public ICommand ToggleCommand => new RelayCommand(_ => { });

        public void Refresh()
        {
            bool newState = OOShutUpManager.IsPrivacySettingApplied(_model);
            if (_isEnabled != newState)
            {
                _isEnabled = newState;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
