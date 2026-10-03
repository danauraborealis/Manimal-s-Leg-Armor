using System.Reflection;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Bot;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Utils;

namespace LegArmorMod.HeavyKilla;

// Prefix BotGenerator.PrepareAndGenerateBot. When an incoming generation
// targets bossKilla, roll a chance to rewrite Role to heavyKilla so the bot
// uses HeavyKilla's loadout while retaining Killa's spawn behaviour.
[Injectable(InjectionType.Singleton)]
public sealed class HeavyKillaReplaceKillaPatch : AbstractPatch
{
    private static HeavyKillaReplaceKillaPatch? _instance;

    private readonly RandomUtil _randomUtil;
    private readonly ISptLogger<HeavyKillaReplaceKillaPatch> _logger;

    public HeavyKillaReplaceKillaPatch(
        RandomUtil randomUtil,
        ISptLogger<HeavyKillaReplaceKillaPatch> logger)
        : base(Manimal.LegArmor.ModInfo.Guid + ".heavy-killa")
    {
        _randomUtil = randomUtil;
        _logger = logger;
        _instance = this;
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(BotGenerator), nameof(BotGenerator.PrepareAndGenerateBot));

    [PatchPrefix]
    private static void Prefix(BotGenerationDetails botGenerationDetails)
    {
        try
        {
            if (_instance == null || botGenerationDetails?.Role == null) return;
            if (!botGenerationDetails.Role.Equals(HeavyKillaConstants.VanillaKillaRole, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (!_instance._randomUtil.GetChance100(HeavyKillaConstants.ReplaceKillaChance)) return;

            botGenerationDetails.Role = HeavyKillaConstants.BotTypeName;
            _instance._logger.Debug($"[LegArmor] swapped bossKilla -> heavyKilla (chance {HeavyKillaConstants.ReplaceKillaChance}%)");
        }
        catch (System.Exception ex)
        {
            _instance?._logger.Error($"[LegArmor] heavyKilla replace prefix failed: {ex}");
        }
    }
}
