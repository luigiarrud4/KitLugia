using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace KitLugia.Core
{
    // --- ENUMS ESSENCIAIS ---
    public enum TweakType { Registry, Service, Mouse, Bcd, PageFile, GpuInterruptPriority }
    public enum TweakStatus { OK, MODIFIED, ERROR, NOT_FOUND }
    public enum StartupStatus { Disabled, Enabled, Elevated }
    public enum ActionType { BuiltIn, GenericCommand, Script }
    public enum ServiceSafetyLevel { Safe, Caution, Dangerous, Unknown }

    // --- MODELO DO BOTÃO DE REPARO (Novo AIO) ---
    public class RepairAction
    {
        // Identificação Única (útil para logs)
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Texto do Botão
        public string Name { get; set; } = string.Empty;

        // Tooltip e Texto Descritivo
        public string Description { get; set; } = string.Empty;

        // Categoria para o Menu Lateral (ex: "Internet", "Explorer")
        public string Category { get; set; } = "Geral";

        // Ícone/Emoji para a Interface
        public string Icon { get; set; } = "🔧";

        // O código C# que será executado ao clicar
        public Action? Execute { get; set; }

        // Se true, a GUI exibirá um popup de "Tem certeza?"
        public bool IsDangerous { get; set; } = false;

        // Flag de interface (se está marcado num modo batch, por exemplo)
        public bool IsSelected { get; set; } = false;

        // Se true, indica que abre uma janela externa (ex: sfc /scannow)
        public bool IsSlow { get; set; } = false;
    }

    // --- DEMAIS DTOs DO SISTEMA ---

    public class StartupAppDetails
    {
        public string Name { get; set; }
        public string FullCommand { get; set; }
        public string Location { get; set; }
        public StartupStatus Status { get; set; }

        public StartupAppDetails(string name, string fullCommand, string location, StartupStatus status)
        {
            Name = name; FullCommand = fullCommand; Location = location; Status = status;
        }
    }

    public class ServiceInfo
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string StartMode { get; set; }
        public ServiceSafetyLevel Safety { get; set; }

        public ServiceInfo(string name, string display, string desc, string status, string startMode, ServiceSafetyLevel safety)
        {
            Name = name; DisplayName = display; Description = desc; Status = status; StartMode = startMode; Safety = safety;
        }
    }

    // --- RECORDS (Imutáveis para leitura rápida) ---
    public record PowerPlanInfo(string Guid, string Name, bool IsActive);
    public record BloatwareApp(string DisplayName, string PackageName, bool IsInstalled, string StoreId = "");
    public record PerformanceEvent(int EventId, string ItemName, long TimeTaken, string EventType, DateTime? TimeOfEvent, string SubType = "");
    public record InstalledProgram(string Name, string Publisher, string Version);
    public record DriverInfo(string DeviceName, string Provider, string Version, DateTime DriverDate);
    public record ScheduledTaskInfo(string Path, string Name, string Description, bool IsEnabled);
    public record SystemStats(string CpuName, float CpuLoad, float CpuTemp, string GpuName, float GpuTemp, double GpuVramUsed, double RamUsed, double RamTotal, string OsName, TimeSpan Uptime, List<StorageInfo> StorageDevices);
    public record StorageInfo(string Name, string HealthStatus, float Temp, string DriveLetter);

    public record BootAnalysisResult
    {
        public string ServiceStatusMessage { get; set; } = string.Empty;
        public PerformanceEvent? TotalTimeEvent { get; set; }
        public List<PerformanceEvent> SlowStartupItems { get; set; } = new();
        public List<PerformanceEvent> HighImpactApps { get; set; } = new();
    }

    [SupportedOSPlatform("windows")]
    public class ScannableTweak
    {
        public string Name { get; set; } = string.Empty;
        public TweakType Type { get; set; } = TweakType.Registry;
        public string Category { get; set; } = string.Empty;
        public TweakStatus Status { get; set; }
        public string? KeyPath { get; set; }
        public string? ValueName { get; set; }
        public object? HarmfulValue { get; set; }
        public object? DefaultValue { get; set; }
        public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.DWord;
        public string? ServiceName { get; set; }
        public string? HarmfulStartMode { get; set; }
        public string? DefaultStartMode { get; set; }
    }
}