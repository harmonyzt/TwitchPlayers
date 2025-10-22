using System.Reflection;
using System.Text.Json;
using TwitchPlayersNames.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace TwitchPlayers;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.harmonyzt.twitchplayers";
    public override string Name { get; init; } = "Twitch Players";
    public override string Author { get; init; } = "harmony";
    public override List<string>? Contributors { get; init; };
    public override SemanticVersioning.Version Version { get; init; } = new("3.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class TwitchPlayers : IOnLoad
{
    private readonly ISptLogger<TwitchPlayers> _logger;
    private readonly DatabaseService _databaseService;
    private readonly ModHelper _modHelper;
    private readonly string _modPath;
    private readonly string _tempPath;
    private readonly string _botCallsignsPath

    public TwitchPlayers(ISptLogger<TwitchPlayers> logger, DatabaseService databaseService, ModHelper modHelper)
    {
        _logger = logger;
        _databaseService = databaseService;
        _modHelper = modHelper;

        _modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        _tempPath = Path.Combine(_modPath, "Temp/");
        
        // BotCallsigns Mod
        _botCallsignsPath = Path.Combine(_modPath, "../../user/mods/BotCallsigns");
    }

    public Task OnLoad()
    {
        FlagChecker();
        return Task.CompletedTask;
    }

    private void FlagChecker()
    {
        if (!Directory.Exists(_botCallsignsPath))
        {
            _logger.LogError(
                "[Twitch Players Validator] 'BotCallsigns' folder is missing/was renamed. Make sure you have installed this mod's dependencies. MOD WILL NOT WORK.");
            return;
        }

        _logger.LogInformation("[Twitch Players Validator] Waiting for flag...");

        var namesReadyPath = Path.Combine(_tempPath, "mod.ready");

        // Check if it already exists and delete
        if (File.Exists(namesReadyPath))
        {
            File.Delete(namesReadyPath);
        }

        // Start monitoring for flag file
        var fileWatcher = new FileSystemWatcher(_tempPath, "mod.ready")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
        };

        fileWatcher.Created += (sender, e) =>
        {
            _logger.LogInformation("[Twitch Players Validator] Detected flag file from BotCallsigns mod");
            File.Delete(e.FullPath);
            HandleFlagFound();
            fileWatcher.Dispose();
        };

        fileWatcher.EnableRaisingEvents = true;
    }

    private void HandleFlagFound()
    {
        _logger.LogInformation("[Twitch Players Validator] Handling flag found - processing names");

        LoadAllNames();
        ApplyChangesToSAIN();
    }
    
     private void LoadAllNames()
    {
        try
        {
            var allNamesPath = Path.Combine(_botCallsignsPath, "nameData", "allNames.json");
            
            if (!File.Exists(allNamesPath))
            {
                _logger.LogError($"[Twitch Players] Could not find allNames.json at {allNamesPath}");
                return;
            }

            var jsonData = File.ReadAllText(allNamesPath);
            var botNameData = JsonSerializer.Deserialize<BotNameData>(jsonData);

            if (botNameData?.Names != null)
            {
                UpdateTTVFile(botNameData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Twitch Players] Error loading names: {ex.Message}");
        }
    }

    private void UpdateTTVFile(BotNameData botNameData)
    {
        try
        {
            // Filter TTV names
            var ttvNames = botNameData.Names
                .Where(name => System.Text.RegularExpressions.Regex.IsMatch(
                    name, @"twitch|ttv|twiitch|chad|gigachad|youtube|_TV", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .ToList();

            var updatedTTVNames = new Dictionary<string, string>();
            
            foreach (var name in ttvNames)
            {
                updatedTTVNames[name] = GetRandomPersonalityWithWeighting();
            }

            var ttvNamesPath = Path.Combine(_modPath, "Names", "ttv_names.json");
            var ttvData = new { generatedTwitchNames = updatedTTVNames };

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ttvNamesPath, JsonSerializer.Serialize(ttvData, jsonOptions));

            _logger.LogInformation("[Twitch Players] Updated our main file ttv_names.json");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Twitch Players] Error updating TTV file: {ex.Message}");
        }
    }

    private string GetRandomPersonalityWithWeighting()
    {
        // Random personality selection (very basic for now)
        var personalities = new[] { "Wreckless", "Gigachad" };
        var random = new Random();
        
        return personalities[random.Next(personalities.Length)];
    }

    private void ApplyChangesToSAIN()
    {
        try
        {
            var sainPersonalitiesPath = Path.Combine(_modPath, "../../BepInEx/plugins/SAIN/Personalities/NicknamePersonalities.json");
            
            if (!File.Exists(sainPersonalitiesPath))
            {
                _logger.LogWarning("[Twitch Players] Couldn't find SAIN's personalities file. If you have just updated SAIN to the latest, launch the game client at least once for this mod to work.");
                return;
            }

            _logger.LogInformation("[Twitch Players] SAIN personalities file detected!");

            var sainJson = File.ReadAllText(sainPersonalitiesPath);
            var sainData = JsonSerializer.Deserialize<SAINPersonalityData>(sainJson);

            var ttvNamesPath = Path.Combine(_modPath, "names", "ttv_names.json");

            var ttvJson = File.ReadAllText(ttvNamesPath);
            var ttvData = JsonSerializer.Deserialize<TTVNamesData>(ttvJson);

            Dictionary<string, string> combinedNames = new();

            if (ttvData?.GeneratedTwitchNames != null)
            {
                foreach (var kvp in ttvData.GeneratedTwitchNames)
                {
                    combinedNames[kvp.Key] = kvp.Value;
                }
            }

            if (sainData != null)
            {
                sainData.NicknamePersonalityMatches = combinedNames;
                
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(sainPersonalitiesPath, JsonSerializer.Serialize(sainData, jsonOptions));
                
                _logger.LogInformation("[Twitch Players] Personalities data was written to SAIN file successfully!");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Twitch Players] Error applying changes to SAIN: {ex.Message}");
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        var dir = new DirectoryInfo(sourceDir);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        DirectoryInfo[] dirs = dir.GetDirectories();

        Directory.CreateDirectory(destinationDir);

        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
    }
  
public record BotNameData
{
    public List<string> Names { get; set; } = new();
}

public record PresetInfo
{
    public DateTime DateCreated { get; set; }
}

public record SAINPersonalityData
{
    public Dictionary<string, string> NicknamePersonalityMatches { get; set; } = new();
}

public record TTVNamesData
{
    public Dictionary<string, string> GeneratedTwitchNames { get; set; } = new();
}

public record YourNamesData
{
    public Dictionary<string, string> CustomNames { get; set; } = new();
}
}