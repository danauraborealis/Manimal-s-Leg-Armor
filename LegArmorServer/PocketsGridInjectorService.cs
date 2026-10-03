using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace LegArmorMod;

// adds a hidden grid to every pockets template - holds one LegArmorHolder
// per profile. the rig prefab still allocates a GridView for the 5th grid
// so HideHolderGridPatch hides it client-side.
//
// also flips HideEntrails so CloneVisibleItem traverses pockets when
// building the visual-only equipment clone for PlayerModelView screens
// (Time Has Come). otherwise the holder gets stripped and the visual
// never mounts on those screens.
[Injectable(InjectionType.Singleton)]
public class PocketsGridInjectorService(
    TemplateTable templateTable,
    ISptLogger<PocketsGridInjectorService> logger)
{
    public const string HiddenGridName = "legarmor_holder_grid";
    private const string PocketsParentClass = "557596e64bdc2dc2118b4571";
    private static readonly MongoId HolderTpl = new("5e9c4f1d8a2b4c3d7f0e1a8c");

    public void Inject()
    {
        var items = templateTable.Items;
        if (items == null)
        {
            logger.Error("[LegArmor] item templates unavailable; cannot inject pockets grid");
            return;
        }

        var injected = 0;
        foreach (var (id, tpl) in items)
        {
            if (tpl.Parent != PocketsParentClass) continue;
            if (tpl.Properties == null) continue;

            // safe in singleplayer; flag only matters for MP pocket privacy.
            tpl.Properties.HideEntrails = false;

            var grids = tpl.Properties.Grids?.ToList() ?? new List<Grid>();
            if (grids.Any(g => g.Name == HiddenGridName)) continue;

            grids.Add(new Grid
            {
                // deterministic id - successive server runs reuse it.
                Id = $"{id.ToString().Substring(0, 16)}1eaa1e9a",
                Name = HiddenGridName,
                Parent = id.ToString(),
                Properties = new GridProperties
                {
                    CellsH = 1,
                    CellsV = 1,
                    MinCount = 0,
                    MaxCount = 0,
                    MaxWeight = 0,
                    IsSortingTable = false,
                    Filters = new[]
                    {
                        new GridFilter
                        {
                            Filter = new HashSet<MongoId> { HolderTpl },
                            ExcludedFilter = new HashSet<MongoId>(),
                            Locked = false,
                        },
                    },
                },
                Prototype = "55d329c24bdc2d892f8b4567",
            });
            tpl.Properties.Grids = grids;
            injected++;
        }
        logger.Info($"[LegArmor] hidden pockets grid injected into {injected} pockets template(s)");
    }
}
