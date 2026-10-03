using System.Reflection;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LegArmorMod.Patches;

// Keep the holder itself across death. Carrier and plate children continue to
// use the normal lost-on-death rules and are removed by the companion patch.
[Injectable(InjectionType.Singleton)]
public sealed class KeepHolderOnDeathPatch : AbstractPatch
{
    private static readonly MongoId HolderTpl = new("5e9c4f1d8a2b4c3d7f0e1a8c");

    public KeepHolderOnDeathPatch()
        : base(Manimal.LegArmor.ModInfo.Guid + ".keep-holder")
    {
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(InRaidHelper), "IsItemKeptAfterDeath");

    [PatchPostfix]
    private static void Postfix(Item itemToCheck, ref bool __result)
    {
        if (!__result && itemToCheck?.Template == HolderTpl)
            __result = true;
    }
}
