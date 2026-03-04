using System;
using System.Collections.Generic;
using System.ComponentModel; // Necessário para INotifyPropertyChanged
using System.Linq;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    public enum SearchResultType { Navigation, Action, Tweak, Service }
  
// Agora implementa INotifyPropertyChanged para atualizar a UI sem travar
public class GlobalSearchResult : INotifyPropertyChanged
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "🔍";
        public string ButtonText { get; set; } = "ABRIR";
        public SearchResultType Type { get; set; } = SearchResultType.Navigation;
        public Func<(bool Success, string Message)>? ExecuteAction { get; set; }
        public string? NavigationTag { get; set; }

        public bool IsToggle { get; set; } = false;

        private bool _isActive = false;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        public Func<bool>? CheckState { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    [SupportedOSPlatform("windows")]
    public static class SearchEngine
    {
        private static List<GlobalSearchResult> _database = new();
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;
            _database.Clear();

            // 1. NAVEGAÇÃO
            AddNav("Dashboard", "Visão geral do hardware e sistema.", "🏠", "🏠");
            AddNav("Limpeza", "Arquivos temporários e cache.", "💿", "💿");
            AddNav("Rede / DNS", "Configurações de latência e DNS.", "🌐", "🌐");
            AddNav("Drivers", "Atualização e backup de drivers.", "💾", "💾");
            AddNav("Serviços", "Gerenciador de serviços e startup.", "🛡️", "🛡️");
            AddNav("Integridade", "Scanner de segurança e vulnerabilidades.", "🛡️", "🛡️Scan");
            AddNav("Jogos", "Otimizações gaming.", "🎮", "🎮");
            AddNav("Ferramentas", "Planos de energia e utilitários.", "🛠️", "🛠️");
            AddNav("Bloatware", "Removedor de apps nativos.", "📱", "📱");
            AddNav("Tela", "Calibragem de cores e resolução.", "🖥️", "🖥️");

            // 2. REPAROS (Ações sem estado)
            var repairs = GeneralRepairManager.GetAllRepairs();
            foreach (var repair in repairs)
            {
                _database.Add(new GlobalSearchResult
                {
                    Title = repair.Name,
                    Description = $"{repair.Description}",
                    Icon = repair.Icon,
                    ButtonText = "EXECUTAR",
                    Type = SearchResultType.Action,
                    ExecuteAction = () => { repair.Execute?.Invoke(); return (true, "Comando enviado."); }
                });
            }

            // 3. INTEGRIDADE / GUARDIAN (COM TOGGLE)
            var securityTweaks = Guardian.GetAllTweaksDefinition();
            foreach (var tweak in securityTweaks)
            {
                _database.Add(new GlobalSearchResult
                {
                    Title = tweak.Name,
                    Description = $"Segurança: {tweak.Description}",
                    Icon = "🛡️",
                    Type = SearchResultType.Tweak,
                    IsToggle = true,
                    // CheckState roda a lógica pesada
                    CheckState = () => {
                        var tempStatus = Guardian.GetHarmfulTweaksWithStatus().FirstOrDefault(t => t.Name == tweak.Name)?.Status;
                        return tempStatus == TweakStatus.MODIFIED;
                    },
                    ExecuteAction = () => Guardian.ToggleTweak(tweak)
                });
            }

            // 4. BLOATWARE (COM CHECKSTATE LEVE)
            // Aqui otimizamos: não rodamos o Powershell na hora da busca, só se o usuário pedir.
            // Para não travar, Bloatware vai ficar sem Toggle na busca rápida (muito pesado verificar 50 apps),
            // ou podemos manter como Ação simples.
            var bloatApps = SystemTweaks.GetBloatwareAppsStatus(); // Isso já foi cacheado no load? Se não, pode pesar.
                                                                   // Para garantir performance, vamos adicionar bloatware como AÇÃO simples na busca, sem check state.
            foreach (var app in bloatApps)
            {
                _database.Add(new GlobalSearchResult
                {
                    Title = $"Remover {app.DisplayName}",
                    Description = "Desinstalar aplicativo nativo.",
                    Icon = "🗑️",
                    ButtonText = "REMOVER",
                    Type = SearchResultType.Action,
                    ExecuteAction = () => SystemTweaks.RemoveBloatwareApp(app.PackageName)
                });
            }

            // 5. TWEAKS ESPECÍFICOS
            AddToggle("Modo Jogo (Game Mode)", "Prioridade de GPU e afinidade.", "🎮",
                action: () => { SystemTweaks.ApplyGamingOptimizations(); return (true, "Aplicado."); },
                check: () => SystemTweaks.IsGamingOptimized()
            );

            AddToggle("Desativar MPO", "Corrige telas piscando (Multi-Plane Overlay).", "📺",
                action: () => SystemTweaks.ToggleMpo(),
                check: () => SystemTweaks.IsMpoDisabled()
            );

            AddToggle("Desativar VBS", "Aumenta FPS desativando virtualização.", "⚡",
                action: () => SystemTweaks.ToggleVbs(),
                check: () => !SystemTweaks.IsVbsEnabled()
            );

            AddToggle("Desativar Pesquisa Bing", "Remove sugestões web do Iniciar.", "🔍",
                action: () => {
                    if (SystemTweaks.IsBingDisabled()) { SystemTweaks.RevertRegistryValue(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions"); return (true, "Reativado."); }
                    else { SystemTweaks.ApplyBingTweak(); return (true, "Desativado."); }
                },
                check: () => SystemTweaks.IsBingDisabled()
            );

            AddAction("CompactOS", "Comprime o Windows.", "🗜️", () => { Toolbox.CompactOS(); return (true, "Iniciado."); });
            AddAction("Limpar Shaders", "Cache de GPU.", "🧹", () => { Toolbox.CleanShaderCaches(); return (true, "Limpo."); });
            AddAction("Flush DNS", "Cache de DNS.", "🚿", () => Toolbox.FlushDnsCache());
            AddAction("Resetar Windows Update", "Corrige erro 0x800.", "🔄", () => { var r = Toolbox.ResetWindowsUpdateComponents(); return (r.Success, "Resetado."); });

            _isInitialized = true;
        }

        private static void AddNav(string title, string desc, string icon, string tag)
        {
            _database.Add(new GlobalSearchResult { Title = title, Description = desc, Icon = icon, ButtonText = "IR PARA", Type = SearchResultType.Navigation, NavigationTag = tag });
        }

        private static void AddAction(string title, string desc, string icon, Func<(bool, string)> action)
        {
            _database.Add(new GlobalSearchResult { Title = title, Description = desc, Icon = icon, ButtonText = "EXECUTAR", Type = SearchResultType.Action, ExecuteAction = action });
        }

        private static void AddToggle(string title, string desc, string icon, Func<(bool, string)> action, Func<bool> check)
        {
            _database.Add(new GlobalSearchResult
            {
                Title = title,
                Description = desc,
                Icon = icon,
                IsToggle = true,
                Type = SearchResultType.Tweak,
                ExecuteAction = action,
                CheckState = check
            });
        }

        public static List<GlobalSearchResult> Search(string query)
        {
            if (!_isInitialized) Initialize();
            if (string.IsNullOrWhiteSpace(query)) return new List<GlobalSearchResult>();
            query = query.ToLower().Trim();

            // Busca rápida apenas em memória (Strings) - Extremamente rápido
            return _database
                .Where(x => x.Title.ToLower().Contains(query) || x.Description.ToLower().Contains(query))
                .OrderByDescending(x => x.Title.ToLower().StartsWith(query))
                .ToList();
        }
    }
}
