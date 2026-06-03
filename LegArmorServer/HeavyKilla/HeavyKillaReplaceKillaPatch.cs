using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace LegArmorMod.HeavyKilla;

// prefix BotGenerator.PrepareAndGenerateBot. when an incoming generation
// targets bossKilla, roll a chance to rewrite the Role to heavyKilla so
// the bot generates with HeavyKilla's loadout instead. inherits all of
// Killa's spawn behaviour (zones, escort, base chance) - no need for a
// dedicated BossLocationSpawn entry.
//
// wired by hand from Mod.cs because PatchAll cant inject services.
[Injectable(InjectionType.Singleton)]
public class HeavyKillaReplaceKillaPatch(
    RandomUtil randomUtil,
    ISptLogger<HeavyKillaReplaceKillaPatch> logger)
{
    public void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(BotGenerator), nameof(BotGenerator.PrepareAndGenerateBot));
        if (target == null)
        {
            logger.Error("[LegArmor] BotGenerator.PrepareAndGenerateBot not found; heavyKilla replacement disabled");
            return;
        }

        var prefix = new HarmonyMethod(typeof(HeavyKillaReplaceKillaPatch), nameof(PrefixStatic));
        harmony.Patch(target, prefix: prefix);

        _instance = this;
    }

    private static HeavyKillaReplaceKillaPatch? _instance;

    public static void PrefixStatic(BotGenerationDetails botGenerationDetails)
    {
        try
        {
            if (_instance == null || botGenerationDetails?.Role == null) return;
            if (!botGenerationDetails.Role.Equals(HeavyKillaConstants.VanillaKillaRole, System.StringComparison.OrdinalIgnoreCase))
                return;

            if (!_instance.randomUtil.GetChance100(HeavyKillaConstants.ReplaceKillaChance)) return;

            botGenerationDetails.Role = HeavyKillaConstants.BotTypeName;
            _instance.logger.Debug($"[LegArmor] swapped bossKilla -> heavyKilla (chance {HeavyKillaConstants.ReplaceKillaChance}%)");
        }
        catch (System.Exception ex)
        {
            _instance?.logger.Error($"[LegArmor] heavyKilla replace prefix failed: {ex}");
        }
    }

    private RandomUtil randomUtil { get; } = randomUtil;
    private ISptLogger<HeavyKillaReplaceKillaPatch> logger { get; } = logger;
}
