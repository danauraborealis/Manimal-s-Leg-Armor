using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;

namespace LegArmorMod;

// injects the LegArmorHolder into the player's pockets hidden grid. living
// inside pockets means the holder is raid-loaded, insurable, and (with the
// KeepHolderOnDeathPatch override) sticks around across death.
//
// idempotent: returns existing id and repairs stale parent / slotId /
// location from older architectures.
//
// also flags our custom tpls in the profile's Encyclopedia so they're
// treated as examined when looted from bot corpses (the client refuses
// inventory moves on unknown tpls otherwise, even with ExaminedByDefault).
[Injectable(InjectionType.Singleton)]
public class LegArmorHolderService(
    ProfileHelper profileHelper,
    DatabaseService databaseService,
    ISptLogger<LegArmorHolderService> logger)
{
    public static readonly MongoId HolderTpl = new("5e9c4f1d8a2b4c3d7f0e1a8c");
    private static readonly MongoId LegArmorParentClass = new("5e9c4f1d8a2b4c3d7f0e1c00");
    private static readonly MongoId SoftInsertParentClass = new("65649eb40bf0ed77b8044453");

    public void FlagExaminedForProfile(MongoId sessionId)
    {
        var pmc = profileHelper.GetPmcProfile(sessionId);
        if (pmc?.Encyclopedia == null) return;

        // walk every db item; flag any that descends from our leg-armor
        // parent class, is a soft insert with our naming prefix, or is the
        // holder itself (clones vanilla armor parent so the parent filter
        // misses it). covers the holder, every carrier (incl. future
        // variants), and every soft plate without hardcoding tpl lists.
        var added = 0;
        foreach (var (tpl, tpl_) in databaseService.GetItems())
        {
            var isOurs = tpl == HolderTpl
                || tpl_.Parent == LegArmorParentClass.ToString()
                || (tpl_.Parent == SoftInsertParentClass.ToString() && tpl.ToString().StartsWith("5e9c4f1d8a2b4c3d7f0e2b", System.StringComparison.Ordinal));
            if (!isOurs) continue;
            if (!pmc.Encyclopedia.ContainsKey(tpl))
            {
                pmc.Encyclopedia[tpl] = false;
                added++;
            }
        }
        if (added > 0)
            logger.Info($"[LegArmor] flagged {added} leg-armor tpl(s) as examined for profile {sessionId}");
    }

    public MongoId EnsureForProfile(MongoId sessionId)
    {
        var pmc = profileHelper.GetPmcProfile(sessionId);
        if (pmc?.Inventory?.Items == null)
        {
            logger.Warning($"[LegArmor] profile {sessionId} has no inventory; cannot inject holder");
            return MongoId.Empty();
        }

        var equipmentId = pmc.Inventory.Equipment?.ToString();
        if (string.IsNullOrEmpty(equipmentId))
        {
            logger.Error($"[LegArmor] profile {sessionId} has no equipment id");
            return MongoId.Empty();
        }
        var pockets = pmc.Inventory.Items.FirstOrDefault(i =>
            i.ParentId == equipmentId && i.SlotId == "Pockets");
        if (pockets == null)
        {
            logger.Error($"[LegArmor] profile {sessionId} has no Pockets item under equipment");
            return MongoId.Empty();
        }
        var pocketsIdStr = pockets.Id.ToString();

        var existing = pmc.Inventory.Items.FirstOrDefault(i => i.Template == HolderTpl);
        if (existing != null)
        {
            // repair holders from older architectures (QuestStashItems / SortingTable parents).
            var repaired = false;
            if (existing.ParentId != pocketsIdStr) { existing.ParentId = pocketsIdStr; repaired = true; }
            if (existing.SlotId != PocketsGridInjectorService.HiddenGridName)
            {
                existing.SlotId = PocketsGridInjectorService.HiddenGridName;
                repaired = true;
            }
            if (existing.Location == null)
            {
                existing.Location = new ItemLocation { X = 0, Y = 0, R = ItemRotation.Horizontal, IsSearched = true };
                repaired = true;
            }
            if (repaired) logger.Info($"[LegArmor] repaired stale holder {existing.Id} for profile {sessionId}");
            return existing.Id;
        }

        var holder = new Item
        {
            Id = new MongoId(),
            Template = HolderTpl,
            ParentId = pocketsIdStr,
            SlotId = PocketsGridInjectorService.HiddenGridName,
            Location = new ItemLocation
            {
                X = 0,
                Y = 0,
                R = ItemRotation.Horizontal,
                IsSearched = true,
            },
        };
        pmc.Inventory.Items.Add(holder);
        logger.Info($"[LegArmor] injected holder {holder.Id} into pockets of profile {sessionId}");
        return holder.Id;
    }
}
