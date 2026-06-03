using System.Collections.Frozen;
using HarmonyLib;
using SPTarkov.Server.Core.Helpers;

namespace LegArmorMod.Patches;

// SPT auto-installs soft armor plates only for slot names in
// ItemHelper._softInsertIds (vanilla: "soft_armor_front", "groin", etc).
// our slot names arent there so plates stay uninstalled for any path that
// doesnt go through a preset.
//
// FrozenSet is initonly - cant mutate the field. postfix IsSoftInsertId
// to return true for our ids; that flows through ItemRequiresSoftInserts
// into AddChildSlotItems.
[HarmonyPatch(typeof(ItemHelper), nameof(ItemHelper.IsSoftInsertId))]
public static class SoftInsertWhitelistPatch
{
    public static readonly FrozenSet<string> LegSlotIds = new[]
    {
        "soft_armor_left_thigh",
        "soft_armor_right_thigh",
        "soft_armor_left_calf",
        "soft_armor_right_calf",
    }.ToFrozenSet();

    [HarmonyPostfix]
    public static void Postfix(string slotId, ref bool __result)
    {
        if (__result) return;
        if (LegSlotIds.Contains(slotId)) __result = true;
    }
}
