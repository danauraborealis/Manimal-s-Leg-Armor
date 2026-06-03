using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;

namespace LegArmorMod.Patches;

// postfix BotGenerator.GenerateBot - this fires for both regular wave bots
// (PrepareAndGenerateBot -> GenerateBot) AND player scavs (PlayerScavGenerator
// calls GenerateBot directly, skipping PrepareAndGenerateBot). hooking the
// inner method catches all paths.
//
// wired by hand from Mod.cs because PatchAll cant inject the service.
[Injectable(InjectionType.Singleton)]
public class BotGenerateHolderInjectPatch(
    LegArmorBotInjectorService injector,
    ISptLogger<BotGenerateHolderInjectPatch> logger)
{
    public void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(BotGenerator), "GenerateBot");
        if (target == null)
        {
            logger.Error("[LegArmor] BotGenerator.GenerateBot not found; bots will not get leg armor");
            return;
        }

        var postfix = new HarmonyMethod(typeof(BotGenerateHolderInjectPatch), nameof(PostfixStatic));
        harmony.Patch(target, postfix: postfix);

        _instance = this;
    }

    private static BotGenerateHolderInjectPatch? _instance;

    // GenerateBot returns the same BotBase instance it received, but the
    // method signature has the bot as a parameter named `bot` (not __result).
    // both __result and the `bot` parameter point to the same object so
    // either works; we use __result for clarity.
    public static void PostfixStatic(BotGenerationDetails botGenerationDetails, BotBase __result)
    {
        try
        {
            if (_instance == null || __result == null) return;
            _instance.injector.InjectIntoBot(__result, botGenerationDetails?.Role);
        }
        catch (System.Exception ex)
        {
            _instance?.logger.Error($"[LegArmor] bot leg armor inject failed: {ex}");
        }
    }

    private LegArmorBotInjectorService injector { get; } = injector;
    private ISptLogger<BotGenerateHolderInjectPatch> logger { get; } = logger;
}
