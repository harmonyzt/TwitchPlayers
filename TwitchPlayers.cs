using System.Reflection;
using SAINServerMod.Models.Preset.Personalities;
using SAINServerMod.Services;
using TwitchPlayers.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace TwitchPlayers;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.harmonyzt.twitchplayers";
    public override string Name { get; init; } = "Twitch Players";
    public override string Author { get; init; } = "harmony";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("3.0.7");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class InitTwitchPlayers(ISptLogger<InitTwitchPlayers> logger, JsonUtil jsonUtils, ConfigService sainConfigService) : IOnLoad
{
    public async Task OnLoad()
    {
        // Await SAIN
        await sainConfigService.LoadAsync();
        
        // Main path to the mod
        var modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var pathToModsFolder = Directory.GetParent(modPath)?.FullName;
        var botCallsignsPath = Path.Combine(pathToModsFolder, "BotCallsigns");
        
        if (Directory.Exists(modPath))
        {
            HandleFlagFound(modPath, botCallsignsPath);
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
            // Find Nickname Personalities.json
            string baseGamePath = Path.GetFullPath(Path.Combine(modPath, @"..\..\..\.."));
            string sainPersonalitiesPath = Path.Combine(baseGamePath,
                "BepInEx", "plugins", "SAIN", "NicknamePersonalities.json");

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
            // TODO: Fix
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