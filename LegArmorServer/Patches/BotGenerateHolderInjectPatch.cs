using System.Reflection;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;

namespace LegArmorMod.Patches;

// Postfix BotGenerator.GenerateBot. This fires for both regular wave bots
// (PrepareAndGenerateBot -> GenerateBot) and player scavs, whose generator
// calls GenerateBot directly.
[Injectable(InjectionType.Singleton)]
public sealed class BotGenerateHolderInjectPatch : AbstractPatch
{
    private static BotGenerateHolderInjectPatch? _instance;

    private readonly LegArmorBotInjectorService _injector;
    private readonly ISptLogger<BotGenerateHolderInjectPatch> _logger;

    public BotGenerateHolderInjectPatch(
        LegArmorBotInjectorService injector,
        ISptLogger<BotGenerateHolderInjectPatch> logger)
        : base(Manimal.LegArmor.ModInfo.Guid + ".bot-generation")
    {
        _injector = injector;
        _logger = logger;
        _instance = this;
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(BotGenerator), "GenerateBot");

    [PatchPostfix]
    private static void Postfix(BotGenerationDetails botGenerationDetails, BotBase __result)
    {
        try
        {
            if (_instance == null || __result == null) return;
            _instance._injector.InjectIntoBot(__result, botGenerationDetails?.Role);
        }
        catch (System.Exception ex)
        {
            _instance?._logger.Error($"[LegArmor] bot leg armor inject failed: {ex}");
        }
    }
}
