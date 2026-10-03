using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Utils;

namespace LegArmorMod;

// injects the LegArmorHolder into a generated bot's pockets and (rolled by
// role tier from bots.json) attaches a leg armor variant with its soft plates.
// mirrors how LegArmorHolderService does it for the player profile.
//
// runs from BotGenerateHolderInjectPatch postfix after BotGenerator finishes.
[Injectable(InjectionType.Singleton)]
public class LegArmorBotInjectorService(
    RandomUtil randomUtil,
    LegArmorBotsConfigService configService,
    ISptLogger<LegArmorBotInjectorService> logger)
{
    public void InjectIntoBot(BotBase bot, string? role)
    {
        if (bot?.Inventory?.Items == null) return;

        var equipmentId = bot.Inventory.Equipment?.ToString();
        if (string.IsNullOrEmpty(equipmentId)) return;

        var pockets = bot.Inventory.Items.FirstOrDefault(i =>
            i.ParentId == equipmentId && i.SlotId == "Pockets");
        if (pockets == null) return;

        // already has a holder (shouldn't happen, but guards against
        // double-fires if our hook gets re-applied somehow).
        if (bot.Inventory.Items.Any(i => i.Template == LegArmorHolderService.HolderTpl))
            return;

        // SPT's loot generator can place random items in any pocket grid
        // including the hidden one we added. clear anything in that grid
        // (recursively, taking children too) so the holder always fits.
        ClearHiddenGrid(bot.Inventory.Items, pockets.Id.ToString());

        var holderId = new MongoId();
        bot.Inventory.Items.Add(new Item
        {
            Id = holderId,
            Template = LegArmorHolderService.HolderTpl,
            ParentId = pockets.Id.ToString(),
            SlotId = PocketsGridInjectorService.HiddenGridName,
            Location = new ItemLocation { X = 0, Y = 0, R = ItemRotation.Horizontal, IsSearched = true },
        });

        var typeConfig = SelectTypeConfig(role);
        if (!randomUtil.GetChance100(typeConfig.SpawnChance)) return;

        var (name, variant) = PickWeightedFromConfig(typeConfig);
        if (variant == null) return;

        AddCarrierWithPlates(bot, holderId, variant);
        logger.Debug($"[LegArmor] bot {role} got {name}");
    }

    private LegArmorBotsConfigService.BotTypeConfig SelectTypeConfig(string? role)
    {
        var c = configService.Config;
        if (string.IsNullOrEmpty(role)) return c.Scav;

        // exact-role override first (case-insensitive match against the dict keys).
        foreach (var kv in c.Overrides)
        {
            if (string.Equals(kv.Key, role, System.StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        var r = role.ToLowerInvariant();
        if (r.Contains("boss")) return c.Boss;
        if (r.Contains("raider") || r.Contains("cultist") || r.Contains("sectant") || r.Contains("follower")
            || r is "pmcbot" or "exusec" or "rogue")
            return c.Raider;
        if (r.Contains("usec") || r.Contains("bear") || r.Contains("pmc"))
            return c.Pmc;
        return c.Scav;
    }

    private (string Name, LegArmorBotsConfigService.VariantConfig? Variant) PickWeightedFromConfig(LegArmorBotsConfigService.BotTypeConfig typeConfig)
    {
        var catalog = configService.Config.Variants;

        // build the candidate pool: catalog entries with a >0 weight.
        var total = 0;
        foreach (var (name, _) in catalog)
        {
            if (typeConfig.Variants.TryGetValue(name, out var w) && w > 0)
                total += w;
        }
        if (total <= 0) return (string.Empty, null);

        var roll = randomUtil.GetInt(0, total - 1);
        var cumulative = 0;
        foreach (var (name, variant) in catalog)
        {
            if (!typeConfig.Variants.TryGetValue(name, out var w) || w <= 0) continue;
            cumulative += w;
            if (roll < cumulative) return (name, variant);
        }
        return (string.Empty, null); // unreachable given total>0
    }

    // slotted items don't normally carry a Location, but setting one with
    // IsSearched=true is harmless if ignored and might be what the client
    // checks before allowing the player to move the item off a corpse.
    private static readonly ItemLocation PreSearchedSlot =
        new() { X = 0, Y = 0, R = ItemRotation.Horizontal, IsSearched = true };

    // removes every item (and all descendants) parented to the pockets via
    // the hidden grid. used to keep the holder's grid clear of loot-gen
    // placements so the holder always has a spot.
    private static void ClearHiddenGrid(List<Item> items, string pocketsId)
    {
        var stale = items
            .Where(i => i.ParentId == pocketsId && i.SlotId == PocketsGridInjectorService.HiddenGridName)
            .ToList();
        if (stale.Count == 0) return;

        var toRemove = new HashSet<string>();
        foreach (var item in stale) CollectDescendants(items, item.Id.ToString(), toRemove);
        items.RemoveAll(i => toRemove.Contains(i.Id.ToString()));
    }

    private static void CollectDescendants(List<Item> items, string parentId, HashSet<string> sink)
    {
        sink.Add(parentId);
        foreach (var child in items.Where(i => i.ParentId == parentId).ToList())
            CollectDescendants(items, child.Id.ToString(), sink);
    }

    private static void AddCarrierWithPlates(BotBase bot, MongoId holderId, LegArmorBotsConfigService.VariantConfig variant)
    {
        var carrierId = new MongoId();
        bot.Inventory.Items.Add(new Item
        {
            Id = carrierId,
            Template = new MongoId(variant.CarrierTpl),
            ParentId = holderId.ToString(),
            SlotId = "mod_legarmor",
            Location = PreSearchedSlot,
        });
        foreach (var plate in variant.Plates)
        {
            bot.Inventory.Items.Add(new Item
            {
                Id = new MongoId(),
                Template = new MongoId(plate.Tpl),
                ParentId = carrierId.ToString(),
                SlotId = plate.Slot,
                Location = PreSearchedSlot,
            });
        }
    }
}
