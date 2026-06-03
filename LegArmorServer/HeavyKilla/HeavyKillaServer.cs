using System.Reflection;
using MoreBotsServer;
using MoreBotsServer.Services;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace LegArmorMod.HeavyKilla;

// registers the heavyKilla custom boss with MoreBots. data lives in
// ServerModFiles/db/bots/sharedTypes/heavykilla.json (appearance + chances
// + equipment list - a vanilla bosskilla copy you can re-skin) and
// ServerModFiles/db/bots/sharedConfig/heavykilla.jsonc (preset/durability).
//
// also attaches the bot to the existing killaTagilla faction so it
// inherits Killa's enemy relations without standing up a new faction.
[Injectable(InjectionType = InjectionType.Singleton, TypePriority = MoreBotsLoadOrder.LoadBots)]
public sealed class HeavyKillaServer(
    MoreBotsAPI moreBotsApi,
    MoreBotsCustomBotTypeService customBotTypeService,
    FactionService factionService,
    ISptLogger<HeavyKillaServer> logger
) : IOnLoad
{
    public async Task OnLoad()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // load sharedTypes + sharedConfig for the bot. second arg is the
        // primary type, third is the list of types in this shared file
        // (we ship one type per file).
        await moreBotsApi.LoadBotsShared(assembly, HeavyKillaConstants.BotTypeName,
            [HeavyKillaConstants.BotTypeName]);

        customBotTypeService.AddCustomWildSpawnTypeNames(new Dictionary<int, string>
        {
            { HeavyKillaConstants.WildSpawnTypeValue, HeavyKillaConstants.BotTypeName },
        });

        AttachToKillaFaction();

        // no dedicated BossLocationSpawn - HeavyKillaReplaceKillaPatch
        // intercepts vanilla Killa generations and rewrites them instead,
        // so heavyKilla inherits Killa's spawn slot.

        logger.Info($"[LegArmor] registered heavyKilla bot (wildSpawnType={HeavyKillaConstants.WildSpawnTypeValue})");

        await Task.CompletedTask;
    }

    private void AttachToKillaFaction()
    {
        if (!factionService.Factions.TryGetValue(HeavyKillaConstants.SharedFactionName, out var faction))
        {
            logger.Warning($"[LegArmor] faction '{HeavyKillaConstants.SharedFactionName}' not found; heavyKilla wont inherit Killa's relations");
            return;
        }

        var type = (WildSpawnType)HeavyKillaConstants.WildSpawnTypeValue;
        if (!faction.BotTypes.Contains(type))
            faction.BotTypes.Add(type);
    }
}
