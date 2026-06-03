using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // direct patch on Slot.method_2 (the private examined-gate inside
    // Slot.RemoveItem/AddItem). more targeted than InventoryController.Examined
    // - this is the exact site that produces GClass1576 ("doesn't allow
    // removing X when it's not examined").
    //
    // returns true unconditionally for slots whose ParentItem descends
    // from our leg armor holder. covers mod_legarmor slot (carrier) AND
    // soft_armor_* slots (plates inside carrier).
    public class LegArmorSlotGatePatch : ModulePatch
    {
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Slot), "method_2", new[] { typeof(Item) });
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
                Plugin.LogSource?.LogInfo($"[LegArmor] Slot.method_2 override: slotId={__instance.ID} item={item?.StringTemplateId} parent={slotParent?.StringTemplateId} (call #{_logCounter})");
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
