using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Customization;
using EFT.InventoryLogic;
using Diz.Binding;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using ZLinq;

namespace Manimal.LegArmor.Patches
{
    // postfix PlayerBody.Init to mount the leg armor visual. vanilla only
    // mounts visuals for slots in the hardcoded SlotNames array; ours isnt
    // there so we hand-construct a SlotView for the holder slot.
    //
    // we do NOT add it to PlayerBody.SlotViews - vanilla raid-init code
    // iterates that dict and crashes on a synthetic-keyed entry. just hold
    // the SlotView alive in our own dict.
    //
    // mesh placement is decided by the prefab's bone bindings, not the
    // parent transform, so PACA's prefab still renders on the torso. once
    // a leg-bound prefab exists this hook automatically uses it.
    //
    // known limitation: the mounted visual shows in third-person preview
    // bodies but is invisible to the player's own first-person camera.
    // the renderers spawn on a layer the FP camera culls and lack the
    // HotObject component EFT uses for its FP shader-replacement pass.
    // we tried polling/fixing both at runtime; neither restored FP
    // visibility and the diagnostics added latency to every spawn. living
    // with the FP gap rather than carrying that complexity.
    public class PlayerBodyMountLegArmorPatch : ModulePatch
    {
        // matches LegArmorHolderService.HolderTpl.
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";
        private const string HolderSlotName = "mod_legarmor";

        // keeps SlotView instances alive (per-PlayerBody) so GC
        // doesnt collect them. stale entries get disposed at next Init.
        private static readonly Dictionary<PlayerBody, PlayerBody.SlotView> _liveSlots = new();

        // per-body slot-change handler so we can unsubscribe on re-Init.
        // without it the slot accumulates a handler per Init and every
        // transition fires N times.
        private static readonly Dictionary<PlayerBody, System.Action<Item>> _slotChangeHandlers = new();

        // SlotView.Dispose() also calls DestroyCurrentModel, which
        // returns the GameObject to the pool - we cant call Dispose to
        // release the bindings without losing the visual. reflect the two
        // bind unsubscribe fields and invoke them directly. Keep the inner
        // subscription intact: it tracks child changes for the mounted
        // model and is released by the normal Dispose path.
        private static readonly FieldInfo UnsubscribeField =
            AccessTools.Field(typeof(PlayerBody.SlotView), "_unsubscribe");
        private static readonly FieldInfo BackpackBindUnsubscribeField =
            AccessTools.Field(typeof(PlayerBody.SlotView), "_backpackBindUnsubscribe");

        protected override MethodBase GetTargetMethod()
        {
            // long-form Init - takes InventoryEquipment, used by all
            // PlayerModelView contexts that show equipment.
            return AccessTools.Method(
                typeof(PlayerBody),
                nameof(PlayerBody.Init),
                new[]
                {
                    typeof(BodyCustomization),
                    typeof(InventoryEquipment),
                    typeof(BindableState<Item>),
                    typeof(int),
                    typeof(EPlayerSide),
                    typeof(string),
                    typeof(System.Collections.Generic.Dictionary<EquipmentSlot, Transform>),
                    typeof(bool),
                });
        }

        [PatchPostfix]
        private static void Postfix(PlayerBody __instance, InventoryEquipment equipment)
        {
            try
            {
                MountIfPresent(__instance, equipment);
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource?.LogError($"[LegArmor] PlayerBody mount failed: {ex}");
            }
        }

        private static void MountIfPresent(PlayerBody body, InventoryEquipment equipment)
        {
            if (body == null || equipment == null) return;

            // dispose SlotViews for destroyed PlayerBodies.
            // multiple stale bindings firing concurrently on item moves
            // caused the stash carrier to fade in/out by stalling the
            // inventory transaction. Init is outside the update window so
            // disposing here cant trigger the reentrancy crash.
            //
            // Unity-destroyed objects == null via the overloaded operator,
            // but the dict uses ReferenceEquals - check operator explicitly.
            var stale = _liveSlots.Keys.AsValueEnumerable().Where(b => b == null).ToList();
            foreach (var b in stale)
            {
                if (_liveSlots.TryGetValue(b, out var sc))
                {
                    try { sc.Dispose(); } catch { /* best effort */ }
                }
                _liveSlots.Remove(b);
            }
            // mirror cleanup for the handler dict so dead bodies dont
            // pile up there either. handler refs become GC-eligible once
            // the slot itself goes away.
            var staleHandlers = _slotChangeHandlers.Keys.AsValueEnumerable().Where(b => b == null).ToList();
            foreach (var b in staleHandlers) _slotChangeHandlers.Remove(b);

            var pocketsItem = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pocketsItem == null) return;

            // walk pockets contents - dont rely on the grid name in case
            // the holder location was repaired since this body was made.
            var holder = pocketsItem.GetAllItems()
                .AsValueEnumerable()
                .FirstOrDefault(child => child.TemplateId == HolderTpl);
            if (holder is not CompoundItem holderCompound) return;

            var slot = holderCompound.Slots.AsValueEnumerable().FirstOrDefault(s => s.ID == HolderSlotName);
            if (slot == null) return;

            // bone is mostly bookkeeping - the prefab's bone bindings
            // decide actual mesh placement.
            var bone = body.PlayerBones?.HolsterPistol;

            // re-Init can fire for the same body (stash refresh after raid).
            // dispose the prior SlotView so its phantom GameObject
            // doesnt linger when the slot is now empty (carrier lost on
            // death). safe because we already released the binding right
            // after construction - Dispose's unbind is a no-op.
            if (_liveSlots.TryGetValue(body, out var prev))
            {
                try { prev.Dispose(); } catch { /* best effort */ }
                _liveSlots.Remove(body);
            }
            // unsubscribe any prior handler on this body so re-Init doesnt
            // double-fire on subsequent slot changes.
            if (_slotChangeHandlers.TryGetValue(body, out var oldHandler))
            {
                try { slot.OnAddOrRemoveItem -= oldHandler; } catch { /* best effort */ }
                _slotChangeHandlers.Remove(body);
            }

            // single persistent handler covers every transition:
            //   empty -> filled  : mount (handles deferred-fill case)
            //   filled -> empty  : dispose so the visual disappears when
            //                      the armor is looted off a corpse
            //   filled -> filled : skipped via _liveSlots guard
            // binding was released at mount, so Dispose only fires
            // UnsubscribeFromInner/DestroyCurrentModel - no transaction
            // reentrancy from the released outer bindings.
            //
            // Slot fires OnAddOrRemoveItem with the AFFECTED item on both
            // add and remove (Slot.cs RemoveItemInternal passes
            // containedItem AFTER nulling ContainedItem), so the handler
            // param doesnt tell us the new state - read slot.ContainedItem.
            System.Action<Item> handler = null;
            handler = (Item _) =>
            {
                if (body == null)
                {
                    slot.OnAddOrRemoveItem -= handler;
                    _slotChangeHandlers.Remove(body);
                    return;
                }
                if (slot.ContainedItem == null)
                {
                    if (_liveSlots.TryGetValue(body, out var sc))
                    {
                        try { sc.Dispose(); } catch { /* best effort */ }
                        _liveSlots.Remove(body);
                    }
                    return;
                }
                if (_liveSlots.ContainsKey(body)) return;
                try { MountNow(body, slot, bone); }
                catch (System.Exception ex) { Plugin.LogSource?.LogError($"[LegArmor] add-handler mount failed: {ex}"); }
            };
            slot.OnAddOrRemoveItem += handler;
            _slotChangeHandlers[body] = handler;

            if (slot.ContainedItem != null)
                MountNow(body, slot, bone);
        }

        private static void MountNow(PlayerBody body, Slot slot, Transform bone)
        {
            // ArmorVest type hint routes through SlotView's armor
            // visual loader. constructor binds to ContainedItem and kicks
            // off the LoadingJob; the visual lands via the async load.
            var slotClass = new PlayerBody.SlotView(
                body, slot, bone, EquipmentSlot.ArmorVest, null, null, false);

            // release the binding right away - persistent binding made the
            // stash carrier fade in/out by stalling the inventory transaction
            // when the user moved items in/out of the slot.
            ReleaseBinding(slotClass, UnsubscribeField);
            ReleaseBinding(slotClass, BackpackBindUnsubscribeField);

            _liveSlots[body] = slotClass;

            Plugin.LogSource?.LogInfo("[LegArmor] mounted leg armor slot");
        }

        // invoke + null so SlotView.Dispose doesnt re-invoke.
        private static void ReleaseBinding(PlayerBody.SlotView slotClass, FieldInfo field)
        {
            if (field == null) return;
            if (field.GetValue(slotClass) is System.Action unbind)
            {
                try { unbind(); } catch { /* best effort */ }
            }
            field.SetValue(slotClass, null);
        }
    }
}
