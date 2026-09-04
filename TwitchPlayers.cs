using System.Reflection;
using SAINServerMod.Services;
using SAIN.Preset.Shared.Models.Preset.Personalities;
using SPTarkov.Common.Models.Logging;
using TwitchPlayers.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Utils;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace TwitchPlayers;

public sealed class TwitchPlayersMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.harmonyzt.twitchplayers";
    public string Name { get; init; } = "Twitch Players";
    public Range SptVersion { get; init; } = new("~4.1");
    public string Author { get; init; } = "harmony";
    public List<string>? Contributors { get; init; }
    public Version Version { get; init; } = new("3.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/harmonyzt/TwitchPlayers";
    public bool? IsBundleMod { get; init; }
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.Preload + 6)]
public class InitTwitchPlayers(ISptLogger<InitTwitchPlayers> logger, JsonUtil jsonUtils, ConfigService sainConfigService) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // Await SAIN
        await sainConfigService.LoadAsync();
        
        // Main path to the mod
        var modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var pathToModsFolder = Directory.GetParent(modPath)?.FullName;
        var botCallsignsPath = Path.Combine(pathToModsFolder, "BotCallsigns");
        var flagPath = Path.Combine(modPath, "Temp", "mod.ready");
        
        HandleFlagFound(modPath, botCallsignsPath);
        CleanupFlag(flagPath);
    }
    
    private static void CleanupFlag(string flagPath)
    {
        if (File.Exists(flagPath))
        {
            File.Delete(flagPath);
            //logger.Info("[Twitch Players] Cleaned up flag file.");
        }
    }

    private void HandleFlagFound(string modPath, string botCallsignsPath)
    {
        LoadAllNames(modPath, botCallsignsPath);
    }

    private async void LoadAllNames(string modPath, string botCallsignsPath)
    {
        try
        {
            var allNamesPath = Path.Combine(botCallsignsPath, "nameData", "allNames.json");

            // Skip this function if bot callsigns haven't generated allNames.json yet
            if (!File.Exists(allNamesPath))
            {
                logger.Warning("[Twitch Players] All names from Bot Callsigns were not found. Try restarting SPT Server for changes to apply.");
                return;
            }
            
            logger.Info("[Twitch Players] BotCallsigns is ready, processing names...");
            
            // If allNames.json exists, process it
            var botNameData = await jsonUtils.DeserializeFromFileAsync<BotCallsignsNames>(allNamesPath);
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

    private async void UpdateTtvFile(BotCallsignsNames botNameData, string modPath)
    {
        try
        {
            // Filter TTV names
            var ttvNames = botNameData.Names.Where(name => System.Text.RegularExpressions.Regex.IsMatch(name,
                @"twitch|ttv|YT|twiitch|chad|gigachad|youtube|_TV",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
            var updatedTtvNames = new Dictionary<string, EPersonality>();
            foreach (var name in ttvNames)
            {
                updatedTtvNames[name] = GetRandomPersonalityIdWithWeighting();
            }

            // No need to check for Names directory, it always exists for us
            var namesDir = Path.Combine(modPath, "Names");
            var ttvNamesPath = Path.Combine(namesDir, "ttv_names.json");

            if (!File.Exists(ttvNamesPath))
            {
                TtvNamesData TtvData = new TtvNamesData() { GeneratedTwitchNames = updatedTtvNames };
            
                await File.WriteAllTextAsync(ttvNamesPath, jsonUtils.Serialize(TtvData, true));
            
                logger.Info($"[Twitch Players] Updated our main file ttv_names.json with {updatedTtvNames.Count} names");

                return;
            }
            
            ApplyChangesToSain(modPath);
        }
        catch (Exception ex)
        {
            logger.Error($"[Twitch Players] Error updating TTV file: {ex.Message}");
        }
    }

    private async void ApplyChangesToSain(string modPath)
    {
        try
        {
            var ttvNamesPath = Path.Combine(modPath, "Names", "ttv_names.json");

            if (!File.Exists(ttvNamesPath))
            {
                logger.Error($"[Twitch Players] ttv_names.json file not found at {ttvNamesPath}");
                return;
            }

            var ttvData = await jsonUtils.DeserializeFromFileAsync<TtvNamesData>(ttvNamesPath);

            if (ttvData == null)
            {
                logger.Error($"[Twitch Players] ttv_names.json data was null! Report this to the developer ASAP!");
                return;
            }

            // Assign personalities inside NicknamePersonalities.json
            sainConfigService.NicknamesModel.NicknamePersonalities = ttvData.GeneratedTwitchNames;
            
            logger.Info(
                $"[Twitch Players] Successfully applied {ttvData.GeneratedTwitchNames.Count} name:personalities to SAIN!");
        }
        catch (Exception ex)
        {
            logger.Error($"[Twitch Players] Error applying changes to SAIN: {ex.Message}");
        }
    }

    private static EPersonality GetRandomPersonalityIdWithWeighting()
    {
        var personalityIds = new[] { EPersonality.Wreckless, EPersonality.SnappingTurtle };
        var weights = new[] { 0.3, 0.5 };
        var random = new Random();
        var randomValue = random.NextDouble();
        double cumulativeNum = 0.0;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulativeNum += weights[i];
            if (randomValue < cumulativeNum)
            {
                return personalityIds[i];
            }
        }

        return EPersonality.Wreckless; // fallback to Wreckless
    }
}