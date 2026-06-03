using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.UI;

namespace Manimal.LegArmor.Patches
{
    // postfix EquipmentTab.Show: resolve the holder slot in the player's
    // pockets, clone the armband SlotView for styling, reposition into
    // the layout, and call Show on the clone. vanilla then drives drag/
    // drop, tooltips, durability bar.
    //
    // we also reposition the holster + scabbard + weapon slots so the leg
    // armor doesnt overlap them - tunable constants below.
    public class EquipmentTabShowPatch : ModulePatch
    {
        // matches LegArmorHolderService.HolderTpl. used to find a holder
        // in any equipment tree (player or bot corpse).
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        // visual template: armband SlotView is the slim half-slot style.
        private static readonly FieldInfo VisualTemplateField =
            AccessTools.Field(typeof(EquipmentTab), "_armbandSlot");

        // anchor template: parent + X column come from the body armor slot.
        private static readonly FieldInfo AnchorTemplateField =
            AccessTools.Field(typeof(EquipmentTab), "_armorSlot");

        // slots whose Y gets retuned to fit the leg armor in. holster/
        // scabbard align with the weapon rows.
        private static readonly FieldInfo HolsterSlotField =
            AccessTools.Field(typeof(EquipmentTab), "_holsterSlot");
        private static readonly FieldInfo ScabbardSlotField =
            AccessTools.Field(typeof(EquipmentTab), "_scabbardSlot");
        private static readonly FieldInfo PrimaryWeaponSlotField =
            AccessTools.Field(typeof(EquipmentTab), "_primaryWeaponSlot");
        private static readonly FieldInfo SecondaryWeaponSlotField =
            AccessTools.Field(typeof(EquipmentTab), "_seconaryWeaponSlot");


        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EquipmentTab), nameof(EquipmentTab.Show));
        }

        [PatchPostfix]
        private static void Postfix(
            EquipmentTab __instance,
            ItemContextAbstractClass equipmentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
            bool inRaid)
        {
            try
            {
                Attach(__instance, equipmentContext, inventoryController, skills, insurance, inRaid, equipment);
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource?.LogError($"[LegArmor] EquipmentTab postfix failed: {ex}");
            }
        }

        private static void Attach(
            EquipmentTab tab,
            ItemContextAbstractClass equipmentContext,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
            bool inRaid,
            InventoryEquipment equipment)
        {
            if (equipment == null) return;

            // find the holder by tpl in whatever equipment we're rendering.
            // player view -> finds the player's holder. corpse loot ->
            // finds the bot's holder (injected by LegArmorBotInjectorService).
            var pocketsItem = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pocketsItem == null) return;

            Item holder = null;
            foreach (var child in pocketsItem.GetAllItems())
            {
                if (child.TemplateId == HolderTpl) { holder = child; break; }
            }
            if (holder is not CompoundItem compound) return;

            var slot = compound.Slots.FirstOrDefault(s => s.ID == "mod_legarmor");
            if (slot == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] holder has no mod_legarmor slot; check the holder JSON");
                return;
            }

            // only mirror to LegArmorState when this is the player's own view.
            // state is consumed by patches that act on player gear (visual
            // mount, etc); corpse views must not overwrite it.
            var isOwnView = ReferenceEquals(equipment, inventoryController?.Inventory?.Equipment);
            if (isOwnView)
                LegArmorState.Bind(holder.Id.ToString(), holder, slot);

            var visualTemplate = VisualTemplateField.GetValue(tab) as SlotView;
            var anchorTemplate = AnchorTemplateField.GetValue(tab) as SlotView;
            if (visualTemplate == null || anchorTemplate == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] visual or anchor slot template was null on EquipmentTab");
                return;
            }

            // shift surrounding slots and get the leg armor's target Y.
            // corpse-loot / other-inventory views use a tighter layout where
            // per-field deltas miss tactical rig + pockets; uniform shift fixes it.
            var legArmorY = isOwnView
                ? RelayoutOnce(tab, anchorTemplate)
                : RelayoutCorpseView(tab, anchorTemplate);

            EnsureClonedSlotView(tab.transform, visualTemplate, anchorTemplate, legArmorY, slot, equipmentContext, inventoryController, skills, insurance, inRaid);
        }

        // per-slot original Y. without this we'd either re-read the shifted
        // value (drift) or skip the relayout for fresh tab instances
        // (in-raid character menu opens a separate EquipmentTab from the
        // stash, with its own RectTransforms - the old static legArmorY
        // cache made us bail before shifting those).
        private static readonly System.Collections.Generic.Dictionary<int, float> _origY = new();

        // ---- TUNABLE LAYOUT CONSTANTS ----
        // all are in anchoredPosition.y units (Unity UI). negative = lower
        // on screen. tune these and rebuild to nudge the layout.
        //
        // LegArmorYOffset:   shift applied to the holster's original Y to
        //                    place the leg armor slot. 0 = leg armor sits
        //                    exactly where the holster used to be.
        // PrimaryWeaponYDelta: shift applied to the primary weapon (and
        //                    holster, since holster aligns with primary)
        //                    relative to the original primary weapon Y.
        //                    negative = move down.
        // SecondaryWeaponYDelta: same idea for secondary weapon and scabbard.
        // ----------------------------------
        private const float LegArmorYOffset = 10f;
        private const float PrimaryWeaponYDelta = -68f;
        private const float SecondaryWeaponYDelta = -68f;

        // corpse-loot view config lookups, keyed by the slot's GameObject
        // name under "Containers Scrollview/Content". weapon row slots
        // return a Y offset (negative = down); container slots return a
        // spacer height (positive = pushes slot down).
        private static readonly System.Collections.Generic.Dictionary<string, System.Func<float>> _corpseWeaponOffsets =
            new()
            {
                // primary row: weapon + holster share an offset.
                ["FirstPrimaryWeapon Slot"]  = () => Manimal.LegArmor.LegArmorConfig.PrimaryRowOffsetY,
                ["Holster Slot"]             = () => Manimal.LegArmor.LegArmorConfig.PrimaryRowOffsetY,
                ["SecondPrimaryWeapon Slot"] = () => Manimal.LegArmor.LegArmorConfig.SecondaryRowOffsetY,
                ["Scabbard Slot"]            = () => Manimal.LegArmor.LegArmorConfig.SecondaryRowOffsetY,
            };

        private static readonly System.Collections.Generic.Dictionary<string, System.Func<float>> _corpseContainerSpacerHeights =
            new()
            {
                ["TacticalVest Slot"] = () => Manimal.LegArmor.LegArmorConfig.TacticalRigSpacerHeight,
                ["Backpack Slot"]     = () => Manimal.LegArmor.LegArmorConfig.BackpackSpacerHeight,
                ["Pockets Slot"]      = () => Manimal.LegArmor.LegArmorConfig.PocketsSpacerHeight,
            };

        // per-slot fine-tune offsets applied by a watchdog (CorpseSlotOffsetter)
        // attached to each VLG slot. allows negative values so the user can
        // pull slots up tighter than vanilla VLG placement.
        private static readonly System.Collections.Generic.Dictionary<string, System.Func<float>> _corpseContainerSlotOffsets =
            new()
            {
                ["TacticalVest Slot"] = () => Manimal.LegArmor.LegArmorConfig.TacticalRigSlotOffsetY,
                ["Backpack Slot"]     = () => Manimal.LegArmor.LegArmorConfig.BackpackSlotOffsetY,
                ["Pockets Slot"]      = () => Manimal.LegArmor.LegArmorConfig.PocketsSlotOffsetY,
            };

        // runs for every EquipmentTab instance. capturing each slot's original Y
        // on first sight (per RectTransform InstanceID) lets re-Shows on the same
        // tab and new tabs (stash vs in-raid character menu) both relayout
        // correctly without drift.
        private static float RelayoutOnce(EquipmentTab tab, SlotView bodyArmorSlot)
        {
            var holsterRt = GetRectTransform(tab, HolsterSlotField);
            var scabbardRt = GetRectTransform(tab, ScabbardSlotField);
            var primaryRt = GetRectTransform(tab, PrimaryWeaponSlotField);
            var secondaryRt = GetRectTransform(tab, SecondaryWeaponSlotField);

            if (holsterRt == null || scabbardRt == null || primaryRt == null || secondaryRt == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] one or more slot RectTransforms missing");
                return 0f;
            }

            var origHolsterY = OriginalY(holsterRt);
            var origPrimaryY = OriginalY(primaryRt);
            var origSecondaryY = OriginalY(secondaryRt);
            OriginalY(scabbardRt); // record for completeness

            var legArmorY = origHolsterY + LegArmorYOffset;
            var newPrimaryY = origPrimaryY + PrimaryWeaponYDelta;
            var newSecondaryY = origSecondaryY + SecondaryWeaponYDelta;

            // holster aligns with primary row, scabbard with secondary - X stays.
            SetY(primaryRt, newPrimaryY);
            SetY(secondaryRt, newSecondaryY);
            SetY(holsterRt, newPrimaryY);
            SetY(scabbardRt, newSecondaryY);

            return legArmorY;
        }

        // corpse-loot view relayout. the slots we care about all live under
        // "Containers Scrollview/Content"; walk it and apply per-name
        // offsets from the BepInEx config. body armor + leg armor + other
        // upper-body slots are NOT touched - they stay where vanilla puts
        // them.
        private static float RelayoutCorpseView(EquipmentTab tab, SlotView bodyArmorSlot)
        {
            var holsterRt = GetRectTransform(tab, HolsterSlotField);
            if (holsterRt == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] corpse relayout: holster RT missing");
                return 0f;
            }

            // legArmorY is still derived from the (un-shifted) holster Y so
            // our cloned slot view sits in the correct row.
            var legArmorY = OriginalY(holsterRt) + LegArmorYOffset;

            var scrollview = FindContainersScrollview(tab.transform);
            if (scrollview == null)
            {
                Plugin.LogSource?.LogWarning("[LegArmor] corpse relayout: Containers Scrollview not found within 6 ancestor levels");
                return legArmorY;
            }

            foreach (var rt in scrollview.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt == null) continue;
                var name = rt.gameObject.name;

                if (_corpseContainerSpacerHeights.TryGetValue(name, out var spacerFn))
                {
                    // VLG-managed slot: inject a spacer GameObject before it.
                    // positive config value = taller spacer = slot pushed down.
                    EnsureSpacerBefore(rt, Mathf.Max(0f, spacerFn()));
                    var le = rt.GetComponent<LayoutElement>();
                    if (le != null && le.ignoreLayout) le.ignoreLayout = false;

                    // attach the per-slot fine-tune watchdog (CorpseSlotOffsetter).
                    // VLG places the slot at its natural Y each frame; the
                    // watchdog re-applies the configured delta on top so
                    // small +/- adjustments work without breaking reflow.
                    if (_corpseContainerSlotOffsets.TryGetValue(name, out var slotOffsetFn))
                    {
                        var offsetter = rt.GetComponent<CorpseSlotOffsetter>();
                        if (offsetter == null) offsetter = rt.gameObject.AddComponent<CorpseSlotOffsetter>();
                        offsetter.OffsetFn = slotOffsetFn;
                    }
                }
                else if (_corpseWeaponOffsets.TryGetValue(name, out var offsetFn))
                {
                    // weapon-row slot inside Gear Panel Template (no VLG):
                    // direct anchoredPosition manipulation. negative Y = down.
                    int id = rt.GetInstanceID();
                    if (!_corpseSlotBaseline.TryGetValue(id, out var baseline))
                    {
                        baseline = rt.anchoredPosition;
                        _corpseSlotBaseline[id] = baseline;
                    }
                    var le = rt.GetComponent<LayoutElement>();
                    if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
                    if (!le.ignoreLayout) le.ignoreLayout = true;
                    rt.anchoredPosition = new Vector2(baseline.x, baseline.y + offsetFn());
                }
            }

            return legArmorY;
        }

        // baseline cache for non-VLG slots only (weapon rows). VLG-managed
        // slots don't need it - their spacer drives the offset.
        private static readonly System.Collections.Generic.Dictionary<int, Vector2> _corpseSlotBaseline = new();

        // creates (or reuses) a named GameObject before slotRt in its parent's
        // sibling order with a LayoutElement at the given preferred height.
        // height = 0 effectively zeroes the offset.
        private static void EnsureSpacerBefore(RectTransform slotRt, float height)
        {
            var parent = slotRt.parent;
            if (parent == null) return;

            var spacerName = "LegArmorSpacer_" + slotRt.gameObject.name;
            Transform spacer = null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == spacerName) { spacer = c; break; }
            }

            if (spacer == null)
            {
                var go = new GameObject(spacerName, typeof(RectTransform), typeof(LayoutElement));
                go.transform.SetParent(parent, false);
                spacer = go.transform;
            }

            // park the spacer at the end first so SetSiblingIndex always
            // moves from later -> earlier. that way SetSiblingIndex(slotIdx)
            // shifts the slot forward by one and the spacer correctly lands
            // right before the slot. without this, if the spacer was already
            // immediately before the slot, SetSiblingIndex(slotIdx) would
            // swap them (Unity moves spacer to slotIdx and pushes slot to
            // slotIdx-1, putting spacer AFTER the slot).
            spacer.SetAsLastSibling();
            spacer.SetSiblingIndex(slotRt.GetSiblingIndex());

            var le = spacer.GetComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0;
        }

        private static Transform FindContainersScrollview(Transform start)
        {
            // climb up to 6 levels; thats enough to reach the Complex Loot
            // Panel ancestor of both EquipmentTab and the scrollview.
            var cur = start;
            for (int i = 0; i < 6 && cur != null; i++)
            {
                foreach (var rt in cur.GetComponentsInChildren<RectTransform>(true))
                {
                    if (rt.name == "Containers Scrollview") return rt;
                }
                cur = cur.parent;
            }
            return null;
        }

        // cache the first-seen Y per RectTransform so subsequent shifts use
        // the pre-shift baseline.
        private static float OriginalY(RectTransform rt)
        {
            int id = rt.GetInstanceID();
            if (_origY.TryGetValue(id, out var y)) return y;
            y = rt.anchoredPosition.y;
            _origY[id] = y;
            return y;
        }

        private static RectTransform GetRectTransform(EquipmentTab tab, FieldInfo field)
        {
            if (field?.GetValue(tab) is not SlotView view) return null;
            return view.GetComponent<RectTransform>();
        }

        // idempotent - setting the slot's anchored Y to the same value on
        // re-Shows is a no-op, so we dont need a per-slot guard.
        private static void SetY(RectTransform rt, float newY)
        {
            if (rt == null) return;
            var pos = rt.anchoredPosition;
            if (Mathf.Approximately(pos.y, newY)) return;
            pos.y = newY;
            rt.anchoredPosition = pos;
        }

        // tagged by name so reopens reuse the existing clone.
        private const string ClonedSlotName = "LegArmorSlotView";

        private static void EnsureClonedSlotView(
            Transform tabTransform,
            SlotView visualTemplate,
            SlotView anchorTemplate,
            float legArmorY,
            Slot slot,
            ItemContextAbstractClass equipmentContext,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
            bool inRaid)
        {
            var existing = tabTransform.Find(ClonedSlotName);
            SlotView legSlotView;
            if (existing != null)
            {
                legSlotView = existing.GetComponent<SlotView>();
            }
            else
            {
                // armband visual parented under body armor's parent for the
                // same layout column. Y comes from the relayout pass.
                var clone = Object.Instantiate(visualTemplate.gameObject, anchorTemplate.transform.parent);
                clone.name = ClonedSlotName;
                legSlotView = clone.GetComponent<SlotView>();

                var rt = clone.GetComponent<RectTransform>();
                var anchorRt = anchorTemplate.GetComponent<RectTransform>();
                if (rt != null && anchorRt != null)
                {
                    rt.anchoredPosition = new Vector2(anchorRt.anchoredPosition.x, legArmorY);
                }

                // Object.Instantiate copies the source's contained item view
                // (eg the armband deadskull). Show on an empty slot wont
                // clear it so it lingers as a ghost - kill it manually.
                ClearSlotPlace(legSlotView);
            }

            if (legSlotView == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] cloned object has no SlotView component");
                return;
            }

            // canClickOnHeader=!inRaid mirrors vanilla; the > header opens
            // EquipItemWindow which would Enum.Parse our slot id and throw.
            // EquipItemWindowSlotIdPatch substitutes "ArmorVest" to keep
            // the parse happy.
            legSlotView.Show(slot, equipmentContext, inventoryController, ItemUiContext.Instance, skills, insurance, !inRaid);
        }

        private static void ClearSlotPlace(SlotView view)
        {
            // only kill ItemView GameObjects - other children of _slotPlace
            // are the slot's background graphics and must stay.
            var itemViews = view.GetComponentsInChildren<ItemView>(true);
            foreach (var iv in itemViews)
            {
                if (iv != null) Object.DestroyImmediate(iv.gameObject);
            }
        }
    }
}
