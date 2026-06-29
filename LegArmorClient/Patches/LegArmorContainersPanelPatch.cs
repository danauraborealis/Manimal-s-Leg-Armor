using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            ItemContextAbstractClass parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
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
                MoveWrapperToTop(slotView.transform);
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

            Item holder = null;
            foreach (var child in pocketsItem.GetAllItems())
            {
                if (child.TemplateId == HolderTpl) { holder = child; break; }
            }
            if (holder is not CompoundItem compound) return null;

            return compound.Slots.FirstOrDefault(s => s.ID == HolderSlotName);
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

        // hoist the WRAPPER (not the SlotView) to sibling index 0 - the
        // SlotView is now nested inside the wrapper, not a direct child
        // of the containers VLG.
        private static void MoveWrapperToTop(Transform slotTransform)
        {
            var wrapper = slotTransform.parent;
            if (wrapper != null && wrapper.name == WrapperName)
                wrapper.SetAsFirstSibling();
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
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ContainersPanel), nameof(ContainersPanel.Show));
        }

        [PatchPostfix]
        private static void Postfix(
            ContainersPanel __instance,
            ItemContextAbstractClass parentContext,
            InventoryEquipment equipment,
            InventoryController inventoryController,
            SkillManager skills,
            InsuranceCompanyClass insurance,
            bool inRaid)
        {
            LegArmorContainersPanelInjector.Inject(__instance, parentContext, equipment, inventoryController, skills, insurance, inRaid);
        }
    }
}
