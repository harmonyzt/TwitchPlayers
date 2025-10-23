using System.Reflection;
using System.Text.Json;
using TwitchPlayers.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;

namespace TwitchPlayers;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.harmonyzt.twitchplayers";
    public override string Name { get; init; } = "Twitch Players";
    public override string Author { get; init; } = "harmony";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("3.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class InitTwitchPlayers(ISptLogger<InitTwitchPlayers> logger, ModHelper modHelper)
    : IOnLoad
{
    public Task OnLoad()
    {
        // Main path to the mod
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        
        if (Directory.Exists(modPath))
        {
            FlagChecker(modPath);
        }

        return Task.CompletedTask;
    }

    // TODO: Remove this bs
    private void FlagChecker(string modPath)
    {

        var pathToModsFolder = Directory.GetParent(modPath)?.FullName;
        
        var botCallsignsPath = Path.Combine(pathToModsFolder, "BotCallsigns");
        
        //var tempPath = Path.Combine(modPath, "Temp");
        
        if (!Directory.Exists(botCallsignsPath))
        {
            logger.Warning("[Twitch Players Validator] 'BotCallsigns' folder is missing/was renamed. Make sure you have installed this mod's dependencies. MOD WILL NOT WORK.");
            return;
        }

        logger.Success("[Twitch Players Validator] Waiting for flag...");

        //var namesReadyPath = Path.Combine(tempPath, "mod.ready");

        // Check if it already exists and delete
        //if (File.Exists(namesReadyPath))
        //{
        //    File.Delete(namesReadyPath);
        //}

        // Start monitoring
        //var fileWatcher = new FileSystemWatcher(tempPath, "mod.ready")
        //{
        //    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime
        //};
        
        HandleFlagFound(modPath, botCallsignsPath);
    }

    private void HandleFlagFound(string modPath, string botCallsignsPath)
    {
        logger.Info("[Twitch Players Validator] Processing names..");

        LoadAllNames(modPath, botCallsignsPath);
        ApplyChangesToSain(modPath);
    }
    
    private void LoadAllNames(string modPath, string botCallsignsPath)
    {
        try
        {
            var allNamesPath = Path.Combine(botCallsignsPath, "nameData", "allNames.json");
            
            if (!File.Exists(allNamesPath))
            {
                logger.Error($"[Twitch Players] Could not find allNames.json at {allNamesPath}");
                return;
            }

            var jsonData = File.ReadAllText(allNamesPath);
            var botNameData = JsonSerializer.Deserialize<BotCallsignsNames>(jsonData);

            if (botNameData?.Names != null)
            {
                UpdateTtvFile(botNameData, modPath);
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[Twitch Players] Error loading names: {ex.Message}");
        }
    }

    private void UpdateTtvFile(BotCallsignsNames botNameData, string modPath)
    {
        try
        {
            // Filter TTV names
            var ttvNames = botNameData.Names
                .Where(name => System.Text.RegularExpressions.Regex.IsMatch(
                    name, @"twitch|ttv|twiitch|chad|gigachad|youtube|_TV", 
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .ToList();

            var updatedTtvNames = new Dictionary<string, int>();
            
            foreach (var name in ttvNames)
            {
                updatedTtvNames[name] = GetRandomPersonalityIdWithWeighting();
            }

            // No need to check for Names directory, it always exists for us
            var namesDir = Path.Combine(modPath, "Names");
            var ttvNamesPath = Path.Combine(namesDir, "ttv_names.json");
            var ttvData = new { generatedTwitchNames = updatedTtvNames };

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ttvNamesPath, JsonSerializer.Serialize(ttvData, jsonOptions));

            logger.Info($"[Twitch Players] Updated our main file ttv_names.json with {updatedTtvNames.Count} names");


            ApplyChangesToSain(modPath);
        }
        catch (Exception ex)
        {
            logger.Error($"[Twitch Players] Error updating TTV file: {ex.Message}");
        }
    }
    
    private void ApplyChangesToSain(string modPath)
    {
        try
        {

            // Find Nickname Personalities.json
            string baseGamePath = Path.GetFullPath(Path.Combine(modPath, @"..\..\..\.."));
            string sainPersonalitiesPath = Path.Combine(baseGamePath, 
                "BepInEx", "plugins", "SAIN", "Personalities", "NicknamePersonalities.json");

            logger.Info($"[Twitch Players] SAIN personalities file detected at: {sainPersonalitiesPath}");

            var ttvNamesPath = Path.Combine(modPath, "Names", "ttv_names.json");
            if (!File.Exists(ttvNamesPath))
            {
                logger.Error($"[Twitch Players] TTV names file not found at {ttvNamesPath}");
                return;
            }

            var ttvJson = File.ReadAllText(ttvNamesPath);
            var ttvData = JsonSerializer.Deserialize<TtvNamesData>(ttvJson);

            if (ttvData?.GeneratedTwitchNames == null)
            {
                logger.Warning("[Twitch Players] No TTV names found to apply to SAIN");
                return;
            }

            // Read existing SAIN data
            var sainJson = File.ReadAllText(sainPersonalitiesPath);
            var sainData = JsonSerializer.Deserialize<SainPersonalityData>(sainJson);

            if (sainData == null) return;

            foreach (var kvp in ttvData.GeneratedTwitchNames)
            {
                 // Here assign personalities
                // sainData.NicknamePersonalityMatches[kvp.Key] = kvp.Value;
            }

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(sainPersonalitiesPath, JsonSerializer.Serialize(sainData, jsonOptions));
                
            logger.Info($"[Twitch Players] Successfully applied {ttvData.GeneratedTwitchNames.Count} personalities to SAIN!");
        }
        catch (Exception ex)
        {
            logger.Error($"[Twitch Players] Error applying changes to SAIN: {ex.Message}");
        }
    }
    
    private static int GetRandomPersonalityIdWithWeighting()
    {
        // Only choose ID 1 (Wreckless) and 3 (GigaChad)
        //  SAIN ENUM
        //  public enum EPersonality
        //  {
        //    None,             0
        //    Wreckless,        1  
        //    SnappingTurtle,   2
        //    GigaChad,         3
        //    Chad,             4
        //    Rat,              5
        //    Timmy,            6
        //    Coward,           7
        //    Normal            8
        //  }
        //
        //
        var personalityIds = new[] { 1, 3 }; // Wreckless, GigaChad
        var weights = new[] { 0.3, 0.5 };
    
        var random = new Random();
        var randomValue = random.NextDouble();
        double cumulative = 0.0;
    
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (randomValue < cumulative)
            {
                return personalityIds[i];
            }
        }
    
        return personalityIds[0]; // fallback to Wreckless
    }
}