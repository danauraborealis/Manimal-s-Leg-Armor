using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // direct patch on Slot.Examined, the narrow examined-gate used by both
    // Slot.CheckConditions and Slot.RemoveItemInternal in the current client.
    //
    // returns true unconditionally for slots whose ParentItem descends
    // from our leg armor holder. covers mod_legarmor slot (carrier) AND
    // soft_armor_* slots (plates inside carrier).
    public class LegArmorSlotGatePatch : ModulePatch
    {
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(Slot),
                nameof(Slot.Examined),
                new[] { typeof(Item) });
        }

        private static int _logCounter;

        [PatchPostfix]
        private static void Postfix(Slot __instance, Item item, ref bool __result)
        {
            if (__result || __instance == null) return;

            var slotParent = __instance.ParentItem;
            var matchedViaSlot = slotParent != null && BelongsToLegArmor(slotParent);
            var matchedViaItem = item != null && BelongsToLegArmor(item);
            if (!matchedViaSlot && !matchedViaItem) return;

            __result = true;
            if ((++_logCounter % 10) == 1)
                Plugin.LogSource?.LogInfo($"[LegArmor] Slot.Examined override: slotId={__instance.ID} item={item?.StringTemplateId} parent={slotParent?.StringTemplateId} (call #{_logCounter})");
        }

        private static bool BelongsToLegArmor(Item item)
        {
            var current = item;
            for (int i = 0; i < 8 && current != null; i++)
            {
                if (current.StringTemplateId == HolderTpl) return true;
                var addr = current.CurrentAddress;
                var parent = addr?.Container?.ParentItem;
                if (parent == null || parent == current) break;
                current = parent;
            }
            return false;
        }
    }
}
