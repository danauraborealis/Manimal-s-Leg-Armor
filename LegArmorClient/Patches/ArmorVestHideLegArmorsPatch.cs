using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // EquipItemWindow.method_4 returns the items shown in the popup list.
    // for the body armor slot the list pulls in our leg armors (via the
    // soft-armor parent chain) and renders them greyed out. ArmorVestReject
    // already blocks them from being equipped; this patch keeps them out
    // of the list entirely.
    //
    // method_4 also fires while EquipItemWindowSlotIdPatch has temporarily
    // renamed our holder slot to "ArmorVest" - the parent-tpl guard makes
    // sure we only filter the real body armor slot.
    public class ArmorVestHideLegArmorsPatch : ModulePatch
    {
        private const string LegArmorParentId = "5e9c4f1d8a2b4c3d7f0e1c00";
        private const string ArmorVestSlotId = "ArmorVest";
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        private static readonly FieldInfo Slot0Field =
            AccessTools.Field(typeof(EquipItemWindow), "slot_0");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EquipItemWindow), "method_4");
        }

        [PatchPostfix]
        private static void Postfix(EquipItemWindow __instance, ref IEnumerable<Item> __result)
        {
            if (__result == null) return;
            if (Slot0Field?.GetValue(__instance) is not Slot slot) return;
            if (slot.ID != ArmorVestSlotId) return;
            if (slot.ParentItem?.TemplateId == HolderTpl) return;

            __result = __result.Where(item => !IsLegArmor(item)).ToList();
        }

        private static bool IsLegArmor(Item item)
        {
            for (var t = item?.Template; t != null; t = t.Parent)
            {
                if (t.ParentId.HasValue && t.ParentId.Value.ToString() == LegArmorParentId) return true;
            }
            return false;
        }
    }
}
