using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Insurance;
using HarmonyLib;
using SPT.Reflection.Patching;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZLinq;

namespace Manimal.LegArmor.Patches
{
    // corpse-view leg armor injection. mirrors Trenchfoot-BeltSlot's
    // BeltSlotInjector: clone ContainersPanel's default SlotView template,
    // bind to mod_legarmor, drop into the panel's VLG-managed
    // _slotViewsContainer. Unity's VerticalLayoutGroup handles positioning -
    // no manual anchoredPosition tweaks, no per-frame CorpseSlotOffsetter,
    // no spacer math. trades the under-body-armor look for the stability
    // of native auto-layout.
    //
    // own-view (player stash) still goes through EquipmentTabShowPatch
    // since EquipmentTab is hand-positioned, not VLG.
    public static class LegArmorContainersPanelInjector
    {
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";
        private const string HolderSlotName = "mod_legarmor";

        private const string InjectedName = "LegArmorSlotView";
        private const string WrapperName = "LegArmorSlotWrapper";

        // leg armor has no inner grids - one item, fixed size. width
        // roughly matches a single inventory cell so the armband strip
        // doesnt look comically wide centered in the row.
        private const float SlotPreferredHeight = 65f;
        private const float SlotPreferredWidth = 140f;

        private static readonly FieldInfo SlotViewsContainerField =
            AccessTools.Field(typeof(ContainersPanel), "_slotViewsContainer");
        private static readonly FieldInfo DefaultSlotTemplateField =
            AccessTools.Field(typeof(ContainersPanel), "_defaultSlotTemplate");

        // armband SlotView is the slim half-slot prefab used by the
        // player-view leg armor injection. we borrow it for corpse view
        // so the icon matches the strip style instead of the square
        // container style.
        private static readonly FieldInfo ArmbandSlotField =
            AccessTools.Field(typeof(EquipmentTab), "_armbandSlot");

        public static void Inject(
            ContainersPanel panel,
            ItemContext parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompany insurance,
            bool inRaid)
        {
            try
            {
                if (panel == null || equipment == null) return;

                // corpse-loot detection: reference compare since corpse loot
                // passes the bot's equipment, not the player controller's.
                // own-view goes through EquipmentTabShowPatch for the
                // half-sized under-body-armor styling.
                var isOwnView = ReferenceEquals(equipment, inventoryController?.Inventory?.Equipment);
                if (isOwnView) return;

                var slot = GetLegArmorSlot(equipment);
                if (slot == null) return;

                var container = SlotViewsContainerField.GetValue(panel) as Transform;
                if (container == null) return;

                // armband first, fall back to default container slot if we
                // cant find an EquipmentTab nearby (corpse loot panels
                // always have one as a sibling so the fallback should
                // never fire in practice).
                var template = FindArmbandTemplate(panel.transform)
                            ?? DefaultSlotTemplateField.GetValue(panel) as SlotView;
                if (template == null) return;

                var slotView = FindOrCreate(container, template);
                if (slotView == null) return;

                slotView.Show(slot, parentContext, inventoryController, ItemUiContext.Instance, skills, insurance, !inRaid);
                SetHeaderText(slotView, "LEG ARMOR");
                ApplySlotSize(slotView.transform as RectTransform);

                // wrapper stays at the BOTTOM of the container to preserve
                // the vanilla visual order. Placement alone cannot repair
                // Trenchfoot-BeltSlot 2.0.4's childCount heuristic: the
                // wrapper still makes a four-child corpse layout look like
                // five children while the direct content ArmBand Slot is absent.
                // PackNStrapCompatibilityPatch validates that hierarchy and
                // short-circuits its mapping method when the slot is missing,
                // preventing the ComplexStashPanel.Show failure that used to
                // leave the corpse panel stuck and stack rigs.
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource?.LogError($"[LegArmor] ContainersPanel inject threw: {ex}");
            }
        }

        private static Slot GetLegArmorSlot(InventoryEquipment equipment)
        {
            var pocketsItem = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pocketsItem == null) return null;

            var holder = pocketsItem.GetAllItems()
                .AsValueEnumerable()
                .FirstOrDefault(child => child.TemplateId == HolderTpl);
            if (holder is not CompoundItem compound) return null;

            return compound.Slots.AsValueEnumerable().FirstOrDefault(s => s.ID == HolderSlotName);
        }

        // wrapper-row pattern: VLG's childControlWidth would stretch our
        // armband-strip to row width. instead we put the SlotView inside
        // a HorizontalLayoutGroup wrapper that doesnt control width and
        // centers its child - the wrapper fills the row, the SlotView
        // sits centered at its preferred width.
        //
        // idempotent: look up by wrapper name, return the SlotView inside.
        private static SlotView FindOrCreate(Transform container, SlotView template)
        {
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child.name != WrapperName) continue;
                return child.GetComponentInChildren<SlotView>(true);
            }

            var wrapperGo = new GameObject(WrapperName, typeof(RectTransform));
            wrapperGo.transform.SetParent(container, false);

            var hlg = wrapperGo.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // wrapper drives the row height; SlotView LayoutElement drives
            // its own width/height inside the wrapper.
            var wrapperLe = wrapperGo.AddComponent<UnityEngine.UI.LayoutElement>();
            wrapperLe.preferredHeight = SlotPreferredHeight;
            wrapperLe.minHeight = SlotPreferredHeight;

            var clone = Object.Instantiate(template.gameObject, wrapperGo.transform, false);
            clone.name = InjectedName;
            return clone.GetComponent<SlotView>();
        }

        // size the SlotView inside the wrapper. wrapper's HLG doesnt
        // control width, so preferredWidth on the SlotView's
        // LayoutElement actually takes effect.
        private static void ApplySlotSize(RectTransform slotRt)
        {
            if (slotRt == null) return;
            var le = slotRt.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = slotRt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredHeight = SlotPreferredHeight;
            le.minHeight = SlotPreferredHeight;
            le.preferredWidth = SlotPreferredWidth;
            le.minWidth = SlotPreferredWidth;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
        }

        // walks up from the ContainersPanel transform looking for an
        // EquipmentTab sibling. corpse loot wraps both in a common parent
        // (usually "Complex Loot Panel" or similar). six levels is plenty.
        private static SlotView FindArmbandTemplate(Transform panelTransform)
        {
            var cur = panelTransform;
            for (int i = 0; i < 6 && cur != null; i++)
            {
                var tab = cur.GetComponentInChildren<EquipmentTab>(true);
                if (tab != null)
                    return ArmbandSlotField?.GetValue(tab) as SlotView;
                cur = cur.parent;
            }
            return null;
        }

        // called from the Close finalizer. closes the injected SlotView
        // (which unregisters it from the corpse's item owner + the player's
        // controller and kills its item view) then destroys the wrapper.
        // matches vanillas create-on-Show / destroy-on-Close cadence for its
        // own slot views - without this the wrapper survived Close and,
        // because ItemsPanel reuses the SAME ContainersPanel instance for
        // corpse loot AND the stash, the last corpse's leg armor slot leaked
        // into every later screen.
        public static void Teardown(ContainersPanel panel)
        {
            if (panel == null) return;
            var container = SlotViewsContainerField.GetValue(panel) as Transform;
            if (container == null) return;

            // reverse - destroying while iterating. also sweeps any extra
            // stale wrappers left over from pre-fix sessions.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (child.name != WrapperName) continue;

                var sv = child.GetComponentInChildren<SlotView>(true);
                if (sv != null)
                {
                    // SlotView.Close no-ops if it was never Shown.
                    try { sv.Close(); }
                    catch (System.Exception ex) { Plugin.LogSource?.LogError($"[LegArmor] injected SlotView.Close threw: {ex}"); }
                }
                Object.DestroyImmediate(child.gameObject);
            }
        }

        // path is headerPanel(0) -> slotViewHeader(1) -> text(2). TMP_Text
        // base avoids a UnityEngine.UI assembly dep.
        private static void SetHeaderText(SlotView slotView, string text)
        {
            try
            {
                var t = slotView.transform;
                if (t.childCount < 1) return;
                var headerPanel = t.GetChild(0);
                if (headerPanel.childCount < 2) return;
                var slotViewHeader = headerPanel.GetChild(1);
                if (slotViewHeader.childCount < 3) return;
                var slotName = slotViewHeader.GetChild(2);
                var tmp = slotName.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = text;
            }
            catch { /* best effort */ }
        }
    }

    public class LegArmorContainersPanelPatch : ModulePatch
    {
        private static readonly FieldInfo TrackedSlotViewsField =
            AccessTools.Field(typeof(ContainersPanel), "_slotViews");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Show));
        }

        // TEMP lifecycle diagnostic. a non-empty tracked dict at Show entry
        // is the smoking gun for the stuck-corpse-panel report: it means
        // Close was skipped since the previous Show, and vanillas
        // _slotViews.Add is about to duplicate-key (swallowed by EFTs
        // async HandleExceptions, so it never reaches the BepInEx log).
        [PatchPrefix]
        private static void Prefix(ContainersPanel __instance)
        {
            try
            {
                var count = (TrackedSlotViewsField?.GetValue(__instance)
                    as System.Collections.Generic.Dictionary<EquipmentSlot, SlotView>)?.Count ?? -1;
                if (count > 0)
                {
                    Plugin.LogSource?.LogError($"[LegArmor][life] Show entered with {count} tracked slot view(s) still present (panel={__instance.GetInstanceID()}) - Close was SKIPPED; forcing Close before Show");

                    // self-heal: without this, vanillas _slotViews.Add
                    // duplicate-keys (swallowed by EFTs async
                    // HandleExceptions - invisible in the BepInEx log) and
                    // the panel wedges with one extra rig per re-Show.
                    // Close-before-Show is vanillas own defensive pattern
                    // (PlayerEquipmentWindow.Show does exactly this). our
                    // Close finalizer also tears down the wrapper here.
                    __instance.Close();
                }
                else
                {
                    Plugin.LogSource?.LogInfo($"[LegArmor][life] Show panel={__instance.GetInstanceID()} tracked={count}");
                }
            }
            catch { /* diag only */ }
        }

        [PatchPostfix]
        private static void Postfix(
            ContainersPanel __instance,
            ItemContext parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompany insurance,
            bool inRaid)
        {
            LegArmorContainersPanelInjector.Inject(__instance, parentContext, equipment, inventoryController, skills, insurance, inRaid);
        }
    }

    // teardown of our wrapper on panel close + recovery when vanilla Close
    // throws. vanilla Close iterates _slotViews closing/destroying its
    // tracked slot views; if one Close throws, the foreach aborts with
    // _slotViews still populated and base.Close never runs. the panel then
    // sticks on screen and every later Show dies on _slotViews.Add
    // duplicate-key right after adding the rig (TacticalVest is first in
    // vanillas slot array) - the reported "stuck panel + one extra rig per
    // corpse, until server restart".
    //
    // finalizer, not postfix: postfixes are skipped when the method throws,
    // and the throw path is exactly the one that needs cleanup.
    public class LegArmorContainersPanelClosePatch : ModulePatch
    {
        private static readonly FieldInfo TrackedSlotViewsField =
            AccessTools.Field(typeof(ContainersPanel), "_slotViews");
        private static readonly FieldInfo DogtagSlotViewField =
            AccessTools.Field(typeof(ContainersPanel), "_dogtagSlotView");

        // guards the recovery re-Close call so the finalizer doesnt recurse.
        private static bool _reentering;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Close));
        }

        [PatchFinalizer]
        private static System.Exception Finalizer(ContainersPanel __instance, System.Exception __exception)
        {
            if (_reentering) return __exception;

            // our wrapper dies with the panel, success or not - Close is the
            // end of life for this cycle's slot views.
            try { LegArmorContainersPanelInjector.Teardown(__instance); }
            catch (System.Exception ex) { Plugin.LogSource?.LogError($"[LegArmor] wrapper teardown failed: {ex}"); }

            if (__exception == null) return null;

            // evidence first - this stack tells us WHICH slot views Close
            // threw, which is the root cause were still hunting.
            Plugin.LogSource?.LogError($"[LegArmor] ContainersPanel.Close threw mid-cleanup, recovering: {__exception}");

            try
            {
                // finish what the aborted foreach didnt: destroy every
                // still-tracked slot view and clear the dict so the next
                // Show doesnt duplicate-key.
                if (TrackedSlotViewsField?.GetValue(__instance) is System.Collections.Generic.Dictionary<EquipmentSlot, SlotView> tracked)
                {
                    foreach (var sv in tracked.Values.AsValueEnumerable().ToList())
                    {
                        if (sv == null) continue;
                        try { sv.Close(); } catch { /* already logged the first throw */ }
                        try { Object.DestroyImmediate(sv.gameObject); } catch { /* best effort */ }
                    }
                    tracked.Clear();
                }
                if (DogtagSlotViewField?.GetValue(__instance) is SlotView dogtag && dogtag != null)
                {
                    try { dogtag.Close(); } catch { /* best effort */ }
                    try { Object.DestroyImmediate(dogtag.gameObject); } catch { /* best effort */ }
                    DogtagSlotViewField.SetValue(__instance, null);
                }

                // re-run Close with emptied state so vanillas base.Close
                // housekeeping (hide gameobject etc) actually executes.
                _reentering = true;
                try { __instance.Close(); }
                finally { _reentering = false; }
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource?.LogError($"[LegArmor] forced panel cleanup failed: {ex}");
            }

            // swallow - the wedged-panel cascade is strictly worse than a
            // swallowed (and fully logged) close error.
            return null;
        }
    }
}
