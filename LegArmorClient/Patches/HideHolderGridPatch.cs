using System.Reflection;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace Manimal.LegArmor.Patches
{
    // ContainedGridsView allocates a GridView for every grid on the item,
    // including our hidden 5th pocket grid - it renders as an empty cell
    // that pushes the rest of the layout. find the bound GridView by id
    // and disable its GameObject so layout collapses.
    public class HideHolderGridPatch : ModulePatch
    {
        // matches PocketsGridInjectorService.HiddenGridName on the server.
        private const string HiddenGridName = "legarmor_holder_grid";

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(ContainedGridsView),
                nameof(ContainedGridsView.Show),
                new[]
                {
                    typeof(EFT.InventoryLogic.CompoundItem),
                    typeof(ItemContextAbstractClass),
                    typeof(GridView[]),
                    typeof(SlotView[]),
                    typeof(TraderControllerClass),
                    typeof(FilterPanel),
                    typeof(ItemUiContext),
                    typeof(bool),
                });
        }

        [PatchPostfix]
        private static void Postfix(GridView[] gridViews)
        {
            if (gridViews == null) return;
            foreach (var view in gridViews)
            {
                if (view == null || view.Grid == null) continue;
                if (view.Grid.ID == HiddenGridName)
                {
                    view.gameObject.SetActive(false);
                }
            }
        }
    }
}
