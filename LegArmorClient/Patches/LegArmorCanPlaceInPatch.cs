using System.Reflection;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // bypass the "InventoryError/TransferToUnknownLocation" gate -
    // "Cannot change the contents of an unsearched container" - when
    // placing items INTO a destination that belongs to our leg armor
    // system (e.g. putting a plate back into the carrier on a corpse).
    //
    // gate is at ItemManipulator.CanTransferTo, the destination-
    // side counterpart to CanModifyItem. produces GClass1565 if the
    // search controller reports the destination container as unsearched.
    public class LegArmorCanPlaceInPatch : ModulePatch
    {
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemManipulator), nameof(ItemManipulator.CanTransferTo));
        }

        private static int _logCounter;

        [PatchPostfix]
        private static void Postfix(ItemAddress to, ref Error error, ref bool __result)
        {
            if (__result || error == null) return;
            if (!AddressContainsHolder(to)) return;

            error = null;
            __result = true;

            if ((++_logCounter % 20) == 1)
                Plugin.LogSource?.LogInfo($"[LegArmor] CanPlaceIn override fired (call #{_logCounter})");
        }

        // walk up the destination address chain. matches any address whose
        // container's parent (or any ancestor) is the holder.
        private static bool AddressContainsHolder(ItemAddress addr)
        {
            var container = addr?.Container;
            for (int i = 0; i < 8 && container != null; i++)
            {
                var parent = container.ParentItem;
                if (parent == null) break;
                if (parent.StringTemplateId == HolderTpl) return true;
                container = parent.CurrentAddress?.Container;
            }
            return false;
        }
    }
}
