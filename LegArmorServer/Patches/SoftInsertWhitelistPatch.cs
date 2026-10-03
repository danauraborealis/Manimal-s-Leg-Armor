using System.Collections.Frozen;
using System.Reflection;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.Items;

namespace LegArmorMod.Patches;

// SPT auto-installs soft armor plates only for known slot ids. Extend that
// helper's answer for the four leg insert slots used by this mod.
[Injectable(InjectionType.Singleton)]
public sealed class SoftInsertWhitelistPatch : AbstractPatch
{
    public static readonly FrozenSet<string> LegSlotIds = new[]
    {
        "soft_armor_left_thigh",
        "soft_armor_right_thigh",
        "soft_armor_left_calf",
        "soft_armor_right_calf",
    }.ToFrozenSet();

    public SoftInsertWhitelistPatch()
        : base(Manimal.LegArmor.ModInfo.Guid + ".soft-inserts")
    {
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(ItemHelper), nameof(ItemHelper.IsSoftInsertId));

    [PatchPostfix]
    private static void Postfix(string slotId, ref bool __result)
    {
        if (!__result && LegSlotIds.Contains(slotId))
            __result = true;
    }
}
