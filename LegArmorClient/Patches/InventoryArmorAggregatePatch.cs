using System.Collections.Generic;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // GetPutOnArmorsNonAlloc is the single funnel EFT uses to collect worn
    // ArmorComponents for damage / explosion / hit-zone resolution. one
    // postfix gets our leg armor into all of those pipelines.
    //
    // reads from LegArmorState.HolderSlot which EquipmentTabShowPatch
    // populates on inventory open. if the player is hit before that
    // (eg during loading) the slot is null and we no-op - acceptable.
    public class InventoryArmorAggregatePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Inventory), nameof(Inventory.GetPutOnArmorsNonAlloc));
        }

        // throttle the log; this is hot during damage events.
        private static int _logCounter;

        [PatchPostfix]
        private static void Postfix(List<ArmorComponent> armorComponents)
        {
            var slot = LegArmorState.HolderSlot;
            var contained = slot?.ContainedItem;
            if (contained == null) return;

            // walk children too to pick up plates inside the carrier.
            var before = armorComponents.Count;
            contained.GetItemComponentsInChildrenNonAlloc(armorComponents, true);
            var added = armorComponents.Count - before;

            if (added > 0 && (++_logCounter % 30) == 1)
            {
                Plugin.LogSource?.LogInfo($"[LegArmor] aggregated {added} ArmorComponent(s) from leg armor (call #{_logCounter})");
            }
        }
    }
}
