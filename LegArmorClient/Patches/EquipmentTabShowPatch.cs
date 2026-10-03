using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Insurance;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using ZLinq;

namespace Manimal.LegArmor.Patches
{
    // postfix EquipmentTab.Show for the player's OWN equipment view only:
    // resolve the holder slot in pockets, clone the armband SlotView,
    // reposition into the layout, and call Show on the clone. vanilla
    // then drives drag/drop, tooltips, durability bar.
    //
    // we also reposition the holster + scabbard + weapon slots so the leg
    // armor doesnt overlap them - tunable constants below.
    //
    // corpse / other-inventory views are NOT handled here. they go through
    // LegArmorContainersPanelPatch which drops the slot into the
    // ContainersPanel's VLG (Unity auto-layout). EquipmentTab is hand-
    // positioned and the corpse view's outer scrollview was constantly
    // resetting our manual offsets - the VLG path is much more stable.
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
            ItemContext equipmentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompany insurance,
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
            ItemContext equipmentContext,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompany insurance,
            bool inRaid,
            InventoryEquipment equipment)
        {
            if (equipment == null) return;

            // find the holder by tpl in whatever equipment we're rendering.
            // player view -> finds the player's holder. corpse loot ->
            // finds the bot's holder (injected by LegArmorBotInjectorService).
            var pocketsItem = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pocketsItem == null) return;

            var holder = pocketsItem.GetAllItems()
                .AsValueEnumerable()
                .FirstOrDefault(child => child.TemplateId == HolderTpl);
            if (holder is not CompoundItem compound) return;

            var slot = compound.Slots.AsValueEnumerable().FirstOrDefault(s => s.ID == "mod_legarmor");
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

            // corpse / other-inventory views go through
            // LegArmorContainersPanelPatch instead.
            if (!isOwnView) return;

            var visualTemplate = VisualTemplateField.GetValue(tab) as SlotView;
            var anchorTemplate = AnchorTemplateField.GetValue(tab) as SlotView;
            if (visualTemplate == null || anchorTemplate == null)
            {
                Plugin.LogSource?.LogError("[LegArmor] visual or anchor slot template was null on EquipmentTab");
                return;
            }

            var legArmorY = RelayoutOnce(tab, anchorTemplate);

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
            ItemContext equipmentContext,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompany insurance,
            bool inRaid)
        {
            // reuse must look in the SAME parent we instantiate into - the
            // body armor slot's parent, NOT the tab root. Transform.Find only
            // checks direct children, and the clone isnt a direct child of the
            // tab, so searching tabTransform never found it -> a fresh SlotView
            // got created every Show and they stacked (intensifying drop
            // shadow, old icon ghosting underneath the new empty clones).
            var slotParent = anchorTemplate.transform.parent;
            var existing = slotParent.Find(ClonedSlotName);
            SlotView legSlotView;
            if (existing != null)
            {
                legSlotView = existing.GetComponent<SlotView>();
            }
            else
            {
                // armband visual parented under body armor's parent for the
                // same layout column. Y comes from the relayout pass.
                var clone = Object.Instantiate(visualTemplate.gameObject, slotParent);
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

            // SlotView.Show renames the GameObject to "<slotId> Slot"
            // ("mod_legarmor Slot"), wiping the ClonedSlotName tag we set on
            // create. that defeated reuse-by-name -> a fresh clone every open
            // -> stacking (intensifying drop shadow). restore our tag so the
            // next Show's Find reuses this same clone.
            legSlotView.gameObject.name = ClonedSlotName;

            // the cloned armband slot's empty placeholder is the ArmBand
            // silhouette (the faded "ghost" the user saw - it's a background
            // Image, not an ItemView, so ClearSlotPlace never touched it).
            // disabling the Image hides it for good: vanilla SetSlotGraphics
            // only toggles the GameObject active, so a disabled Image stays
            // hidden across fill/empty cycles.
            foreach (var img in legSlotView.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img != null && img.sprite != null && img.sprite.name == "ArmBand")
                    img.enabled = false;
            }

            // vanilla SlotView.Show on an EMPTY slot only swaps the empty
            // graphics - it never kills a leftover ItemView. with the clone
            // now correctly reused, a previously-shown armor icon would
            // otherwise linger once the slot empties. safe to clear here: this
            // runs on inventory-open, not inside a drag or an inventory
            // transaction (clearing from a slot event was what broke
            // OnBeginDrag earlier).
            if (slot.ContainedItem == null)
                ClearSlotPlace(legSlotView);
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
