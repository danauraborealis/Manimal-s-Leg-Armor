using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    public class InventoryArmorAggregatePatch : ModulePatch
    {
        private const string HolderSlotName = "mod_legarmor";
        private static readonly List<ArmorComponent> Own = new List<ArmorComponent>();
        private static int _logCounter;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Inventory), nameof(Inventory.GetPutOnArmorsNonAlloc));
        }

        [PatchPostfix]
        private static void Postfix(Inventory __instance, List<ArmorComponent> armorComponents)
        {
            var equipment = __instance?.Equipment;
            if (equipment == null || armorComponents == null) return;

            var pockets = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pockets == null) return;

            Own.Clear();
            foreach (var child in pockets.GetAllItems())
            {
                var holder = child as CompoundItem;
                if (holder == null || holder.StringTemplateId != Plugin.HolderTpl) continue;
                foreach (var slot in holder.Slots)
                {
                    if (slot.ID != HolderSlotName || slot.ContainedItem == null) continue;
                    slot.ContainedItem.GetItemComponentsInChildrenNonAlloc(Own, true);
                }
            }

            var added = 0;
            foreach (var component in Own)
            {
                if (armorComponents.Contains(component)) continue;
                armorComponents.Add(component);
                added++;
            }
            Own.Clear();

            if (added > 0 && (++_logCounter % 30) == 1)
            {
                Plugin.LogSource?.LogInfo($"[LegArmor] aggregated {added} ArmorComponent(s) from leg armor (call #{_logCounter})");
            }
        }
    }
}
