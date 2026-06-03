using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // GridItemView (and the SlotItemView that extends it) sets IsSearched
    // in NewGridItemView based on whether the item's container is a grid
    // marker (GInterface215) AND the search controller "knows" the item.
    //
    // for items inside the bot's pockets - including our hidden grid -
    // the container is GInterface215 and the player search controller's
    // IsItemKnown returns false until the player explicitly searches the
    // pockets. that makes our leg armor un-clickable on corpse views.
    //
    // bypass: postfix NewGridItemView and force IsSearched=true when the
    // item descends from the leg armor holder. only affects leg-armor
    // items; everything else in the corpse stays gated as vanilla.
    public class LegArmorIsSearchedPatch : ModulePatch
    {
        // matches LegArmorHolderService.HolderTpl + Plugin.HolderTpl.
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GridItemView), nameof(GridItemView.NewGridItemView));
        }

        [PatchPostfix]
        private static void Postfix(GridItemView __instance, Item item)
        {
            if (__instance == null || item == null) return;
            if (__instance.IsSearched) return;
            if (!BelongsToLegArmor(item)) return;

            __instance.IsSearched = true;
        }

        // walks up from item to the topmost ancestor. returns true if any
        // step matches the holder tpl. covers:
        //  - the holder itself (first iteration)
        //  - the carrier item inside the holder's mod_legarmor slot
        //  - any soft insert plate inside the carrier
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
