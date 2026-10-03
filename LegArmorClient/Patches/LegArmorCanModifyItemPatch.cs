using System.Reflection;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // bypass the "InventoryError/UnknownItemManipulation" gate -
    // "Cannot interact with an unexamined item" - for items in our leg
    // armor system.
    //
    // gate path:
    //   ItemView.UpdateRemoveError -> ItemManipulator.Remove
    //     -> ItemManipulator.CanModifyItem
    //     -> controller.SearchController.GetObserverItemState(item, from)
    //     -> if Unknown, returns GClass1566 (UnknownItemManipulation)
    //
    // when the player views an unsearched bot corpse, items inside its
    // pockets are wrapped (GClass3367) and report state Unknown.
    // postfix CanModifyItem and clear the error when the item or its
    // source address belongs to our holder hierarchy.
    public class LegArmorCanModifyItemPatch : ModulePatch
    {
        // matches LegArmorHolderService.HolderTpl + Plugin.HolderTpl.
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemManipulator), nameof(ItemManipulator.CanModifyItem));
        }

        private static int _logCounter;

        [PatchPostfix]
        private static void Postfix(Item item, ItemAddress from, ref Error error, ref bool __result)
        {
            if (__result || error == null) return;

            var ownedByItem = item != null && BelongsToLegArmor(item);
            var ownedByAddr = from != null && AddressContainsHolder(from);
            if (!ownedByItem && !ownedByAddr) return;

            error = null;
            __result = true;

            if ((++_logCounter % 20) == 1)
                Plugin.LogSource?.LogInfo($"[LegArmor] CanModifyItem override: item={item?.StringTemplateId} via={(ownedByItem ? "item" : "address")} (call #{_logCounter})");
        }

        // walk up from item via CurrentAddress.Container.ParentItem.
        // matches holder itself, carrier inside it, plates inside the carrier.
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

        // walk up the source address chain. for a carrier in the holder's
        // mod_legarmor slot, addr.Container.ParentItem IS the holder.
        // also handles GClass3367-wrapped items whose item identity is
        // hidden but whose address still points to the original slot.
        private static bool AddressContainsHolder(ItemAddress addr)
        {
            var container = addr?.Container;
            for (int i = 0; i < 8 && container != null; i++)
            {
                var parent = container.ParentItem;
                if (parent == null) break;
                if (parent.StringTemplateId == HolderTpl) return true;
                container = parent.CurrentAddress?.Container;
            }
            return false;
        }
    }
}
