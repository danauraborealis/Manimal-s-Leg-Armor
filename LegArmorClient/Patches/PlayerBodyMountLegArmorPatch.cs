using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace Manimal.LegArmor.Patches
{
    // postfix PlayerBody.Init to mount the leg armor visual. vanilla only
    // mounts visuals for slots in the hardcoded SlotNames array; ours isnt
    // there so we hand-construct an EquipmentSlotClass for the holder slot.
    //
    // we do NOT add it to PlayerBody.SlotViews - vanilla raid-init code
    // iterates that dict and crashes on a synthetic-keyed entry. just hold
    // the EquipmentSlotClass alive in our own dict.
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

        // keeps EquipmentSlotClass instances alive (per-PlayerBody) so GC
        // doesnt collect them. stale entries get disposed at next Init.
        private static readonly Dictionary<PlayerBody, PlayerBody.EquipmentSlotClass> _liveSlots = new();

        // EquipmentSlotClass.Dispose() also calls DestroyCurrentModel, which
        // returns the GameObject to the pool - we cant call it to release
        // the binding without losing the visual. reflect Action_0/_2 and
        // invoke them directly.
        private static readonly FieldInfo Action0Field =
            AccessTools.Field(typeof(PlayerBody.EquipmentSlotClass), "Action_0");
        private static readonly FieldInfo Action2Field =
            AccessTools.Field(typeof(PlayerBody.EquipmentSlotClass), "Action_2");

        protected override MethodBase GetTargetMethod()
        {
            // long-form Init - takes InventoryEquipment, used by all
            // PlayerModelView contexts that show equipment.
            return AccessTools.Method(
                typeof(PlayerBody),
                nameof(PlayerBody.Init),
                new[]
                {
                    typeof(GClass2197),
                    typeof(InventoryEquipment),
                    typeof(BindableStateClass<Item>),
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

            // dispose EquipmentSlotClasses for destroyed PlayerBodies.
            // multiple stale bindings firing concurrently on item moves
            // caused the stash carrier to fade in/out by stalling the
            // inventory transaction. Init is outside the update window so
            // disposing here cant trigger the reentrancy crash.
            //
            // Unity-destroyed objects == null via the overloaded operator,
            // but the dict uses ReferenceEquals - check operator explicitly.
            var stale = _liveSlots.Keys.Where(b => b == null).ToList();
            foreach (var b in stale)
            {
                if (_liveSlots.TryGetValue(b, out var sc))
                {
                    try { sc.Dispose(); } catch { /* best effort */ }
                }
                _liveSlots.Remove(b);
            }

            var pocketsItem = equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem as CompoundItem;
            if (pocketsItem == null) return;

            // walk pockets contents - dont rely on the grid name in case
            // the holder location was repaired since this body was made.
            Item holder = null;
            foreach (var child in pocketsItem.GetAllItems())
            {
                if (child.TemplateId == HolderTpl) { holder = child; break; }
            }
            if (holder is not CompoundItem holderCompound) return;

            var slot = holderCompound.Slots.FirstOrDefault(s => s.ID == HolderSlotName);
            if (slot == null) return;

            // bone is mostly bookkeeping - the prefab's bone bindings
            // decide actual mesh placement.
            var bone = body.PlayerBones?.HolsterPistol;

            // re-Init can fire for the same body (stash refresh after raid).
            // dispose the prior EquipmentSlotClass so its phantom GameObject
            // doesnt linger when the slot is now empty (carrier lost on
            // death). safe because we already released the binding right
            // after construction - Dispose's unbind is a no-op.
            if (_liveSlots.TryGetValue(body, out var prev))
            {
                try { prev.Dispose(); } catch { /* best effort */ }
                _liveSlots.Remove(body);
            }

            if (slot.ContainedItem != null)
            {
                MountNow(body, slot, bone);
                return;
            }

            // empty slot at init - common on Time Has Come where the body
            // is built before equipment resolves. subscribe to the plain
            // C# OnAddOrRemoveItem event (not the reactive bindable that
            // caused the inventory fade) and mount when the item arrives.
            // handler unsubscribes if the body dies first.
            System.Action<Item> handler = null;
            handler = (Item item) =>
            {
                if (body == null)
                {
                    slot.OnAddOrRemoveItem -= handler;
                    return;
                }
                if (item == null) return;
                slot.OnAddOrRemoveItem -= handler;
                if (_liveSlots.ContainsKey(body)) return;
                try { MountNow(body, slot, bone); }
                catch (System.Exception ex) { Plugin.LogSource?.LogError($"[LegArmor] deferred mount failed: {ex}"); }
            };
            slot.OnAddOrRemoveItem += handler;
            Plugin.LogSource?.LogInfo("[LegArmor] holder slot empty at init; subscribed for later mount");
        }

        private static void MountNow(PlayerBody body, Slot slot, Transform bone)
        {
            // ArmorVest type hint routes through EquipmentSlotClass's armor
            // visual loader. constructor binds to ContainedItem and kicks
            // off the LoadingJob; the visual lands via the async load.
            var slotClass = new PlayerBody.EquipmentSlotClass(
                body, slot, bone, EquipmentSlot.ArmorVest, null, null, false);

            // release the binding right away - persistent binding made the
            // stash carrier fade in/out by stalling the inventory transaction
            // when the user moved items in/out of the slot.
            ReleaseBinding(slotClass, Action0Field);
            ReleaseBinding(slotClass, Action2Field);

            _liveSlots[body] = slotClass;

            Plugin.LogSource?.LogInfo("[LegArmor] mounted leg armor slot");
        }

        // invoke + null so EquipmentSlotClass.Dispose doesnt re-invoke.
        private static void ReleaseBinding(PlayerBody.EquipmentSlotClass slotClass, FieldInfo field)
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
