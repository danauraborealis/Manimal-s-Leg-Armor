using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace LegArmorMod.Patches;

// GetInventoryItemsLostOnDeath only adds items parented under equipment
// or QuestRaidItems plus items in pocket1..4 grids. our carrier lives in
// the holder slot, so it falls through and survives even though
// IsItemKeptAfterDeath returns false for it.
//
// postfix appends every item whose tpl has the LegArmor parent. plates
// cascade via InventoryHelper.RemoveItem.
//
// wired by hand because the patch needs DatabaseService - InRaidHelper's
// primary-constructor field isnt reachable via Harmony ___field syntax.
[Injectable(InjectionType.Singleton)]
public class LoseCarrierOnDeathPatch(
    DatabaseService databaseService,
    ISptLogger<LoseCarrierOnDeathPatch> logger)
{
    private static readonly MongoId LegArmorParent = new("5e9c4f1d8a2b4c3d7f0e1c00");
    private static LoseCarrierOnDeathPatch? _instance;

    public void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(InRaidHelper), "GetInventoryItemsLostOnDeath");
        if (target == null)
        {
            logger.Error("[LegArmor] InRaidHelper.GetInventoryItemsLostOnDeath not found; carrier wont be lost on death");
            return;
        }

        var postfix = new HarmonyMethod(typeof(LoseCarrierOnDeathPatch), nameof(PostfixStatic));
        harmony.Patch(target, postfix: postfix);
        _instance = this;
    }

    public static void PostfixStatic(PmcData pmcProfile, ref IEnumerable<Item> __result)
    {
        try
        {
            if (_instance == null) return;
            var items = pmcProfile?.Inventory?.Items;
            if (items == null) return;

            var dbItems = _instance.databaseService.GetTables().Templates?.Items;
            if (dbItems == null) return;

            var existing = __result.ToList();
            var existingIds = new HashSet<string>(existing.Select(i => i.Id.ToString()));
            var parentString = LegArmorParent.ToString();

            foreach (var item in items)
            {
                if (!dbItems.TryGetValue(item.Template, out var tpl)) continue;
                if (tpl?.Parent != parentString) continue;
                if (existingIds.Add(item.Id.ToString())) existing.Add(item);
            }

            __result = existing;
        }
        catch (System.Exception ex)
        {
            _instance?.logger.Error($"[LegArmor] LoseCarrierOnDeath postfix failed: {ex}");
        }
    }

    private DatabaseService databaseService { get; } = databaseService;
    private ISptLogger<LoseCarrierOnDeathPatch> logger { get; } = logger;
}
