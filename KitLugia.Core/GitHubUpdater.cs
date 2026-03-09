using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO.Compression;

namespace KitLugia.Core
{
    public static class GitHubUpdater
    {
        private static readonly string GitHubRepo = "luigiarrud4/KitLugia";
        public static readonly string ApiUrl = $"https://api.github.com/repos/{GitHubRepo}/releases/latest"; // 🔥 Público
        public static readonly HttpClient _httpClient = new(); // 🔥 Público para acesso da UpdatePage

        static GitHubUpdater()
        {
            // 🔥 Adiciona User-Agent para evitar erro 403 Forbidden
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "KitLugia-Updater/2.5.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        // 🔥 Opções de serialização para garantir parsing correto
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, // 🔥 GitHub API usa snake_case
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        public class ReleaseInfo
        {
            public string TagName { get; set; } = "";
            public string Name { get; set; } = "";
            public string Body { get; set; } = "";
            public bool Prerelease { get; set; }
            public DateTime PublishedAt { get; set; }
            public Asset[] Assets { get; set; } = Array.Empty<Asset>();
        }

        public class Asset
        {
            public string Name { get; set; } = "";
            public string BrowserDownloadUrl { get; set; } = "";
            public long Size { get; set; }
        }

        public static async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                Logger.Log("🔍 Verificando atualizações no GitHub...");
                
                var response = await _httpClient.GetAsync(ApiUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log($"❌ Erro ao buscar release: {response.StatusCode}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                Logger.Log($"📦 Versão atual: {GetCurrentVersion()}");
                
                var release = JsonSerializer.Deserialize<ReleaseInfo>(json, JsonOptions);
                
                if (release == null)
                {
                    Logger.Log("❌ Erro ao desserializar release");
                    return false;
                }

                var latestVersion = ParseVersion(release.TagName);
                Logger.Log($"🚀 Versão latest: {latestVersion}");

                var currentVersion = GetCurrentVersion();

                Logger.Log($"📦 Versão atual: {currentVersion}");
                Logger.Log($"🚀 Versão latest: {latestVersion}");

                if (latestVersion > currentVersion)
                {
                    Logger.Log($"✅ Nova versão disponível: {release.TagName}");
                    Logger.Log($"📝 Notas: {release.Name}");
                    return true;
                }

                Logger.Log("✅ KitLugia está atualizado!");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Erro ao verificar atualizações: {ex.Message}");
                return false;
            }
        }

        public static async Task<bool> DownloadAndInstallUpdateAsync()
        {
            try
            {
                Logger.Log("🔄 Baixando atualização...");

                // 1. Obter informações do release
                var response = await _httpClient.GetAsync(ApiUrl);
                var json = await response.Content.ReadAsStringAsync();
                Logger.Log($"📄 JSON recebido: {json.Substring(0, Math.Min(200, json.Length))}...");
                
                var release = JsonSerializer.Deserialize<ReleaseInfo>(json, JsonOptions);

                if (release?.Assets == null || release.Assets.Length == 0)
                {
                    Logger.Log("❌ Nenhum arquivo encontrado no release");
                    return false;
                }

                Logger.Log($"📦 Assets encontrados: {release.Assets.Length}");
                foreach (var assetItem in release.Assets)
                {
                    Logger.Log($"📁 Asset: {assetItem.Name} - {assetItem.Size / 1024 / 1024}MB");
                }

                // 2. Encontrar o asset correto (KITLUGIA2.zip ou KITLUGIA2.rar)
                var asset = Array.Find(release.Assets, a => 
                    a.Name.Equals("KITLUGIA2.zip", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Equals("KITLUGIA2.rar", StringComparison.OrdinalIgnoreCase));

                if (asset == null)
                {
                    Logger.Log("❌ Asset KITLUGIA2.zip/rar não encontrado");
                    Logger.Log($"📋 Assets disponíveis: {string.Join(", ", release.Assets.Select(a => a.Name))}");
                    return false;
                }

                // 3. Download
                var tempDir = Path.GetTempPath();
                var fileExtension = asset.Name.EndsWith(".zip") ? ".zip" : ".rar";
                var updatePath = Path.Combine(tempDir, $"KitLugia_Update{fileExtension}");
                var extractDir = Path.Combine(tempDir, "KitLugia_Update");

                Logger.Log($"📥 Baixando {asset.Name} ({asset.Size / 1024 / 1024}MB)...");

                var downloadResponse = await _httpClient.GetAsync(asset.BrowserDownloadUrl);
                await using (var fileStream = File.Create(updatePath))
                {
                    await downloadResponse.Content.CopyToAsync(fileStream);
                }

                Logger.Log("✅ Download concluído!");

                // 4. Extração
                Logger.Log("📂 Extraindo arquivos...");
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                if (fileExtension == ".zip")
                {
                    ZipFile.ExtractToDirectory(updatePath, extractDir);
                }
                else
                {
                    // Para RAR, precisamos de uma biblioteca específica ou usar 7-Zip
                    Logger.Log("⚠️ Arquivo RAR detectado - recomendado usar ZIP para melhor compatibilidade");
                    // Por enquanto, tenta extrair como ZIP (pode funcionar se for ZIP com extensão errada)
                    try
                    {
                        ZipFile.ExtractToDirectory(updatePath, extractDir);
                    }
                    catch
                    {
                        Logger.Log("❌ Falha ao extrair arquivo RAR. Use formato ZIP para melhor compatibilidade.");
                        File.Delete(updatePath);
                        return false;
                    }
                }
                
                File.Delete(updatePath);

                // 5. Encontrar o executável
                var exePath = FindExecutable(extractDir);
                if (string.IsNullOrEmpty(exePath))
                {
                    Logger.Log("❌ Executável não encontrado no pacote");
                    return false;
                }

                // 6. Preparar atualização
                Logger.Log("🔧 Preparando instalação...");
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                var updateScript = CreateUpdateScript(exePath, currentExe, extractDir);

                // 7. Iniciar atualização
                Logger.Log("🚀 Iniciando atualização automática...");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = $"/c {updateScript}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                // 8. Fechar aplicação atual
                try
                {
                    var currentProcess = Process.GetCurrentProcess();
                    var processes = Process.GetProcessesByName("KitLugia.GUI");
                    foreach (var process in processes)
                    {
                        if (process.Id != currentProcess.Id)
                        {
                            process.Kill();
                        }
                    }
                    currentProcess.Kill();
                }
                catch (Exception ex)
                {
                    Logger.Log($"Erro ao fechar aplicação: {ex.Message}");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Erro na atualização: {ex.Message}");
                return false;
            }
        }

        private static Version GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version ?? new Version("1.0.0");
            }
            catch
            {
                return new Version("1.0.0");
            }
        }

        private static Version ParseVersion(string tag)
        {
            try
            {
                // Remove 'v' se existir
                var cleanTag = tag.StartsWith("v") ? tag.Substring(1) : tag;
                return Version.Parse(cleanTag);
            }
            catch
            {
                return new Version("1.0.0");
            }
        }

        private static string FindExecutable(string directory)
        {
            try
            {
                Logger.Log($"🔍 Procurando executável em: {directory}");
                
                // Nomes possíveis do executável
                var exeNames = new[] { "KITLUGIA2.exe", "KitLugia.GUI.exe", "KitLugia.exe" };
                
                // Procura em subpastas comuns
                var searchPaths = new[]
                {
                    Path.Combine(directory, "KitLugia.GUI", "bin", "Release", "net8.0-windows"),
                    Path.Combine(directory, "KitLugia.GUI", "bin", "Debug", "net8.0-windows"),
                    Path.Combine(directory, "bin", "Release"),
                    Path.Combine(directory, "bin", "Debug"),
                    Path.Combine(directory, "KITLUGIA2", "bin", "Debug", "net8.0-windows"),
                    Path.Combine(directory, "KITLUGIA2", "bin", "Release", "net8.0-windows"),
                    directory
                };

                foreach (var path in searchPaths)
                {
                    if (Directory.Exists(path))
                    {
                        Logger.Log($"📁 Verificando pasta: {path}");
                        foreach (var exeName in exeNames)
                        {
                            var exe = Path.Combine(path, exeName);
                            if (File.Exists(exe))
                            {
                                Logger.Log($"✅ Executável encontrado: {exe}");
                                return exe;
                            }
                        }
                    }
                }

                // Busca recursiva
                Logger.Log("🔍 Busca recursiva por executáveis...");
                foreach (var exeName in exeNames)
                {
                    var files = Directory.GetFiles(directory, exeName, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        Logger.Log($"✅ Executável encontrado (recursivo): {files[0]}");
                        return files[0];
                    }
                }

                Logger.Log("❌ Nenhum executável encontrado");
                return "";
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Erro ao procurar executável: {ex.Message}");
                return "";
            }
        }

        private static string CreateUpdateScript(string newExePath, string currentExePath, string extractDir)
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), "update_kitlugia.bat");
            var currentDir = Path.GetDirectoryName(currentExePath);
            
            var script = $@"
@echo off
title KitLugia Auto-Updater v2.5.0
echo.
echo ========================================
echo    KitLugia Auto-Updater v2.5.0
echo ========================================
echo.
echo [INFO] Executavel atual: {currentExePath}
echo [INFO] Novo executavel: {newExePath}
echo [INFO] Diretorio de extracao: {extractDir}
echo [INFO] Diretorio atual: {currentDir}
echo.

echo Aguardando KitLugia fechar...
timeout /t 3 /nobreak >nul

echo Verificando se executavel existe...
if exist ""{newExePath}"" (
    echo [OK] Executavel encontrado
) else (
    echo [ERRO] Executavel nao encontrado: {newExePath}
    pause
    exit /b 1
)

echo Copiando novo executavel...
copy ""{newExePath}"" ""{currentExePath}"" /Y
if errorlevel 1 (
    echo [ERRO] Falha ao copiar executavel
    pause
    exit /b 1
)

echo Copiando outros arquivos necessarios...
xcopy ""{Path.GetDirectoryName(newExePath)}\*"" ""{currentDir}"" /E /H /C /I /Y /EXCLUDE:update_exclude.txt

echo Limpando arquivos temporários...
rd /s /q ""{extractDir}""
del ""%~f0""

echo Iniciando nova versao...
start "" ""{currentExePath}""

echo ========================================
echo    Atualizacao concluida com sucesso!
echo ========================================
timeout /t 2 /nobreak >nul
";

            // Cria arquivo de exclusão para não copiar arquivos desnecessários
            var excludePath = Path.Combine(Path.GetTempPath(), "update_exclude.txt");
            File.WriteAllText(excludePath, ".pdb\r\n.xml\r\n.pdb\r\nuser.config\r\n.log\r\n.tmp");
            
            File.WriteAllText(scriptPath, script);
            return scriptPath;
        }

        // Método para verificar periodicamente
        public static async Task StartAutoUpdateCheck()
        {
            try
            {
                // Verifica a cada 24 horas
                while (true)
                {
                    await Task.Delay(TimeSpan.FromHours(24));
                    
                    if (await CheckForUpdatesAsync())
                    {
                        Logger.Log("🔄 Atualização disponível! Use a opção 'Atualizar' no menu.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"❌ Erro no auto-update check: {ex.Message}");
            }
        }
    }
}
