using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Manimal.LegArmor.Patches
{
    // EquipItemWindow.Show does Enum.TryParse<EquipmentSlot>(slot.ID) and
    // throws on unknown ids. swap to "ArmorVest" for the duration of Show
    // and restore in postfix.
    //
    // safe: the parsed enum only gates Dogtag (special UI) and the weapon
    // layout; ArmorVest hits neither. the item list comes from the slot's
    // filter, not the enum.
    public class EquipItemWindowSlotIdPatch : ModulePatch
    {
        private const string OurSlotId = "mod_legarmor";
        private const string SubstituteId = "ArmorVest";

        // cached - this fires on every header click.
        private static readonly FieldInfo SlotIdBackingField =
            AccessTools.Field(typeof(Slot), "<ID>k__BackingField");

        protected override MethodBase GetTargetMethod()
        {
            // explicit params - Show is overloaded.
            return AccessTools.Method(
                typeof(EquipItemWindow),
                nameof(EquipItemWindow.Show),
                new[]
                {
                    typeof(Slot),
                    typeof(InventoryController),
                    typeof(IEftSession),
                    typeof(SkillManager),
                    typeof(Vector3),
                });
        }

        [PatchPrefix]
        private static void Prefix(Slot slot, out string __state)
        {
            __state = null;
            if (slot == null || slot.ID != OurSlotId) return;
            if (SlotIdBackingField == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] could not find Slot.ID backing field; equip window will throw");
                return;
            }
            __state = (string)SlotIdBackingField.GetValue(slot);
            SlotIdBackingField.SetValue(slot, SubstituteId);
        }

        [PatchPostfix]
        private static void Postfix(Slot slot, string __state)
        {
            if (__state == null) return;
            SlotIdBackingField?.SetValue(slot, __state);
        }
    }
}
