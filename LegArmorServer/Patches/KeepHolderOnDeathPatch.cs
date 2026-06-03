using HarmonyLib;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace LegArmorMod.Patches;

// IsItemKeptAfterDeath falls through to "lost" for any pocket child whose
// slotId isnt pocket1..4 or doesnt contain "SpecialSlot", so the holder
// would be wiped and the player wouldnt have a slot until next session.
// pin the holder to "kept" regardless of config.
//
// per-item evaluation - carrier + plate children still hit the normal
// rules and get lost like pocket loot.
[HarmonyPatch(typeof(InRaidHelper), "IsItemKeptAfterDeath")]
public static class KeepHolderOnDeathPatch
{
    private static readonly MongoId HolderTpl = new("5e9c4f1d8a2b4c3d7f0e1a8c");

    [HarmonyPostfix]
    public static void Postfix(Item itemToCheck, ref bool __result)
    {
        if (__result) return;
        if (itemToCheck?.Template == HolderTpl) __result = true;
    }
}
