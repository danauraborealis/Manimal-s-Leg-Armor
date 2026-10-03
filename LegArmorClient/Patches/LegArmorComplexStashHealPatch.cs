using System.Reflection;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // second layer of the stuck-corpse-panel containment, one level above
    // the ContainersPanel force-close.
    //
    // ComplexStashPanel.Show instantiates a fresh EquipmentTab (_equipmentPanel)
    // every call and only destroys it in Close. when a third-party postfix
    // throws out of ComplexStashPanel.Show (Trenchfoot-BeltSlot 2.0.4 NREs in
    // getComplexLootUI_Mappings), ItemsPanel.Show aborts BEFORE registering
    // ComplexStashPanel.Close for disposal - so each corpse open orphans the
    // previous EquipmentTab and full equipment sets stack down the loot panel.
    //
    // heal: if a stale _equipmentPanel exists at Show entry, run the missed
    // Close first (vanilla's own defensive pattern - PlayerEquipmentWindow
    // does Close-before-Show). Close also cascades into _containersPanel
    // where our finalizer tears down the leg armor wrapper.
    public static class ComplexStashHealer
    {
        private static readonly FieldInfo EquipmentTabField =
            AccessTools.Field(typeof(ComplexStashPanel), "_equipmentPanel");

        public static void HealIfStale(ComplexStashPanel panel, string caller)
        {
            try
            {
                if (panel == null || EquipmentTabField == null) return;
                if (EquipmentTabField.GetValue(panel) is not EquipmentTab stale || stale == null) return;

                Plugin.LogSource?.LogError($"[LegArmor][life] stale corpse EquipmentTab found at {caller} - a mod threw out of ComplexStashPanel.Show and its Close was never registered; forcing Close");
                panel.Close();
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource?.LogError($"[LegArmor] ComplexStashPanel heal failed: {ex}");
            }
        }
    }

    // heals corpse -> corpse: every corpse view funnels through this Show.
    public class LegArmorComplexStashShowHealPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ComplexStashPanel), nameof(ComplexStashPanel.Show));
        }

        [PatchPrefix]
        private static void Prefix(ComplexStashPanel __instance)
        {
            ComplexStashHealer.HealIfStale(__instance, "ComplexStashPanel.Show");
        }
    }

    // heals corpse -> stash/container: those paths never re-Show the complex
    // panel, so a stale one would otherwise linger on screen (the "persists
    // in the stash" report).
    public class LegArmorItemsPanelShowHealPatch : ModulePatch
    {
        private static readonly FieldInfo ComplexStashPanelField =
            AccessTools.Field(typeof(ItemsPanel), "_complexStashPanel");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemsPanel), nameof(ItemsPanel.Show));
        }

        [PatchPrefix]
        private static void Prefix(ItemsPanel __instance)
        {
            var panel = ComplexStashPanelField?.GetValue(__instance) as ComplexStashPanel;
            ComplexStashHealer.HealIfStale(panel, "ItemsPanel.Show");
        }
    }
}
