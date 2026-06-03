using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // ArmorVest equipment slot accepts by parent class - our LegArmor
    // parent chains up to soft-armor, so the carrier passes vanilla
    // CheckCompatibility. SPT's SlotFilter DTO has no ExcludedFilter to
    // patch server-side; do it client-side via Slot.CheckCompatibility
    // postfix.
    public class ArmorVestRejectLegArmorPatch : ModulePatch
    {
        // matches db/CustomParents/LegArmorParent.json.
        private const string LegArmorParentId = "5e9c4f1d8a2b4c3d7f0e1c00";
        private const string ArmorVestSlotId = "ArmorVest";

        // EquipItemWindowSlotIdPatch temporarily renames our holder slot to
        // "ArmorVest" - guard by parent item tpl so we dont reject leg
        // armors during the equip window's compatibility scan.
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Slot), nameof(Slot.CheckCompatibility));
        }

        [PatchPostfix]
        private static void Postfix(Slot __instance, Item item, ref bool __result)
        {
            if (!__result) return;
            if (__instance == null || __instance.ID != ArmorVestSlotId) return;
            if (__instance.ParentItem?.TemplateId == HolderTpl) return;
            if (item?.Template == null) return;

            for (var t = item.Template; t != null; t = t.Parent)
            {
                if (t.ParentId.HasValue && t.ParentId.Value.ToString() == LegArmorParentId)
                {
                    __result = false;
                    return;
                }
            }
        }
    }
}
