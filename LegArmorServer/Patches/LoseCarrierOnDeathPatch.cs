using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.InRaid;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace LegArmorMod.Patches;

// GetInventoryItemsLostOnDeath only includes equipment-root, quest-raid, and
// pocket1..4 children. The carrier is in the holder slot, so append custom
// carrier items explicitly; their plate children cascade through vanilla
// inventory removal.
[Injectable(InjectionType.Singleton)]
public sealed class LoseCarrierOnDeathPatch : AbstractPatch
{
    private static readonly MongoId LegArmorParent = new("5e9c4f1d8a2b4c3d7f0e1c00");
    private static LoseCarrierOnDeathPatch? _instance;

    private readonly TemplateTable _templateTable;
    private readonly ISptLogger<LoseCarrierOnDeathPatch> _logger;

    public LoseCarrierOnDeathPatch(
        TemplateTable templateTable,
        ISptLogger<LoseCarrierOnDeathPatch> logger)
        : base(Manimal.LegArmor.ModInfo.Guid + ".lose-carrier")
    {
        _templateTable = templateTable;
        _logger = logger;
        _instance = this;
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(InRaidHelper), "GetInventoryItemsLostOnDeath");

    [PatchPostfix]
    private static void Postfix(PmcData pmcProfile, ref IEnumerable<Item> __result)
    {
        try
        {
            if (_instance == null) return;
            var items = pmcProfile?.Inventory?.Items;
            if (items == null) return;

            var dbItems = _instance._templateTable.Items;
            var existing = __result?.ToList() ?? new List<Item>();
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
            _instance?._logger.Error($"[LegArmor] LoseCarrierOnDeath postfix failed: {ex}");
        }
    }
}
