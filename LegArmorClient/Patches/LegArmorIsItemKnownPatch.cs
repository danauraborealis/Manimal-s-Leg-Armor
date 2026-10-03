using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // postfix the player search controller's IsItemKnown to return true for
    // any item that belongs to our leg armor system. side-effects:
    //   - ContainsUnknownItems (on the pockets) won't report our hidden
    //     grid's contents as unknowns -> "needs search" indicator only
    //     fires if there are unknowns in the visible vanilla grids.
    //   - the per-item "reveal" effect on pockets-search skips our items.
    public class LegArmorIsItemKnownPatch : ModulePatch
    {
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ActiveSearchController), nameof(ActiveSearchController.IsItemKnown), new[] { typeof(Item), typeof(ItemAddress) });
        }

        [PatchPostfix]
        private static void Postfix(Item item, ref bool __result)
        {
            if (__result || item == null) return;
            if (BelongsToLegArmor(item)) __result = true;
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
