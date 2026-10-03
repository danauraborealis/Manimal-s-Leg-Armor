using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // bypass the "is this item examined?" gate for any item that descends
    // from our leg armor holder.
    //
    // when the user drags the carrier out of the holder's mod_legarmor
    // slot on a corpse, Slot.Examined looks up the BOT's InventoryController
    // (the holder's owner) and calls .Examined(item). bots' controllers
    // have Examined=false and the bot's profile doesnt know our tpls, so
    // the check returns false and the remove fails with GClass1576
    // ("doesn't allow removing X when it's not examined").
    //
    // since our items conceptually belong to the player (the holder is
    // injected into every bot's pockets), it's safe to force Examined=true
    // for anything in our hierarchy.
    public class LegArmorExaminedPatch : ModulePatch
    {
        // matches LegArmorHolderService.HolderTpl + Plugin.HolderTpl.
        private const string HolderTpl = "5e9c4f1d8a2b4c3d7f0e1a8c";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InventoryController), nameof(InventoryController.Examined), new[] { typeof(Item) });
        }

        private static int _logCounter;

        [PatchPostfix]
        private static void Postfix(Item item, ref bool __result)
        {
            if (item == null) return;

            var belongs = BelongsToLegArmor(item);
            if (belongs && !__result)
            {
                __result = true;
                if ((++_logCounter % 10) == 1)
                    Plugin.LogSource?.LogInfo($"[LegArmor] Examined override: tpl={item.StringTemplateId} forced=true (call #{_logCounter})");
            }
            else if (!belongs)
            {
                // log only the first few non-matches with tpl info so we can see if the right item is reaching here.
                if (_logCounter < 5)
                {
                    _logCounter++;
                    Plugin.LogSource?.LogInfo($"[LegArmor] Examined miss: tpl={item.StringTemplateId} result={__result}");
                }
            }
        }

        // walk up from item to root. matches at:
        //   - the holder itself (first iteration)
        //   - the carrier in the holder's mod_legarmor slot
        //   - soft plates inside the carrier
        private static bool BelongsToLegArmor(Item item)
        {
            var current = item;
            for (int i = 0; i < 8 && current != null; i++)
            {
                if (current.StringTemplateId == HolderTpl) return true;
                var addr = current.CurrentAddress;
                var parent = addr?.Container?.ParentItem;
                if (parent == null || parent == current) break;
                current = parent;
            }
            return false;
        }
    }
}
