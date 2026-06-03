using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // alt-click "quick equip" goes through FindSlotToPickUp (extension
    // method on GClass3373). vanilla's hardcoded item-type -> slot
    // cascade doesn't know about our mod_legarmor slot - leg armors
    // fall through, return null, "No free slot for that item".
    //
    // postfix: when vanilla returned null, walk pockets -> hidden grid ->
    // holder -> mod_legarmor and ask the slot directly. the slot's own
    // filter decides eligibility, so no type list needed here.
    public class LegArmorFindSlotPatch : ModulePatch
    {
        // matches LegArmorHolderService.HolderTpl + Plugin.HolderTpl.
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";
        private const string LegArmorSlotName = "mod_legarmor";

        protected override MethodBase GetTargetMethod()
        {
            var t = AccessTools.TypeByName("GClass3373");
            if (t == null)
            {
                Plugin.LogSource?.LogWarning("[LegArmor] GClass3373 not found; quick-equip-into-leg-armor disabled");
                return null;
            }
            return AccessTools.Method(t, "FindSlotToPickUp", new[] { typeof(InventoryEquipment), typeof(Item) });
        }

        [PatchPostfix]
        private static void Postfix(InventoryEquipment equipment, Item item, ref ItemAddress __result)
        {
            if (__result != null) return;
            if (equipment == null || item == null) return;

            var slot = FindLegArmorSlot(equipment);
            if (slot == null) return;
            if (slot.ContainedItem != null) return;

            var address = slot.FindLocationForItem(item, out _);
            if (address != null) __result = address;
        }

        // pockets -> hidden holder grid -> holder -> mod_legarmor.
        // returns null at any missing step (no holder = no fix).
        private static Slot FindLegArmorSlot(InventoryEquipment equipment)
        {
            var pockets = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pockets == null) return null;

            CompoundItem holder = null;
            foreach (var child in pockets.GetAllItems())
            {
                if (child.StringTemplateId == HolderTpl) { holder = child as CompoundItem; break; }
            }
            if (holder == null) return null;

            return holder.Slots?.FirstOrDefault(s => s != null && s.ID == LegArmorSlotName);
        }
    }
}
