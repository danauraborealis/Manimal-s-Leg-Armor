using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace LegArmorMod;

// registers a default item preset for each carrier (carrier + locked plate
// children). matches how vanilla armors like PACA ship.
//
// stable MongoIds so server reloads overwrite rather than duplicate.
[Injectable(InjectionType.Singleton)]
public class LegArmorPresetService(
    GlobalTable globalTable,
    ISptLogger<LegArmorPresetService> logger)
{
    private static readonly PresetSpec[] Presets =
    {
        new(
            new("5e9c4f1d8a2b4c3d7f0e2f01"), new("5e9c4f1d8a2b4c3d7f0e2f11"),
            new("5e9c4f1d8a2b4c3d7f0e2a01"), "Class 2 Thigh Armor Standard",
            new[]
            {
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b01"), "Soft_armor_left_thigh",  new MongoId("5e9c4f1d8a2b4c3d7f0e2f12")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b02"), "Soft_armor_right_thigh", new MongoId("5e9c4f1d8a2b4c3d7f0e2f13")),
            }),
        new(
            new("5e9c4f1d8a2b4c3d7f0e2f02"), new("5e9c4f1d8a2b4c3d7f0e2f21"),
            new("5e9c4f1d8a2b4c3d7f0e2a02"), "Class 3 Thigh Armor Standard",
            new[]
            {
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b05"), "Soft_armor_left_thigh",  new MongoId("5e9c4f1d8a2b4c3d7f0e2f22")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b06"), "Soft_armor_right_thigh", new MongoId("5e9c4f1d8a2b4c3d7f0e2f23")),
            }),
        new(
            new("5e9c4f1d8a2b4c3d7f0e2f03"), new("5e9c4f1d8a2b4c3d7f0e2f31"),
            new("5e9c4f1d8a2b4c3d7f0e2a03"), "Class 2 Full Leg Armor Standard",
            new[]
            {
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b01"), "Soft_armor_left_thigh",  new MongoId("5e9c4f1d8a2b4c3d7f0e2f32")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b02"), "Soft_armor_right_thigh", new MongoId("5e9c4f1d8a2b4c3d7f0e2f33")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b03"), "Soft_armor_left_calf",   new MongoId("5e9c4f1d8a2b4c3d7f0e2f34")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b04"), "Soft_armor_right_calf",  new MongoId("5e9c4f1d8a2b4c3d7f0e2f35")),
            }),
        new(
            new("5e9c4f1d8a2b4c3d7f0e2f04"), new("5e9c4f1d8a2b4c3d7f0e2f41"),
            new("5e9c4f1d8a2b4c3d7f0e2a04"), "Class 3 Full Leg Armor Standard",
            new[]
            {
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b05"), "Soft_armor_left_thigh",  new MongoId("5e9c4f1d8a2b4c3d7f0e2f42")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b06"), "Soft_armor_right_thigh", new MongoId("5e9c4f1d8a2b4c3d7f0e2f43")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b07"), "Soft_armor_left_calf",   new MongoId("5e9c4f1d8a2b4c3d7f0e2f44")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b08"), "Soft_armor_right_calf",  new MongoId("5e9c4f1d8a2b4c3d7f0e2f45")),
            }),
        new(
            new("5e9c4f1d8a2b4c3d7f0e2f0d"), new("5e9c4f1d8a2b4c3d7f0e2fd1"),
            new("5e9c4f1d8a2b4c3d7f0e2a0d"), "Killa's Leg Armor Standard",
            new[]
            {
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b09"), "Soft_armor_left_thigh",  new MongoId("5e9c4f1d8a2b4c3d7f0e2fd2")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b0a"), "Soft_armor_right_thigh", new MongoId("5e9c4f1d8a2b4c3d7f0e2fd3")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b0b"), "Soft_armor_left_calf",   new MongoId("5e9c4f1d8a2b4c3d7f0e2fd4")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e2b0c"), "Soft_armor_right_calf",  new MongoId("5e9c4f1d8a2b4c3d7f0e2fd5")),
            }),
        // FORT Gladiator-S (Killa Edition). all 14 plate slots are locked
        // and preset-attached: 4 hard plates (Cult Termite front/back,
        // SSAPI III+ sides), 8 ceramic class-4 soft inserts, 2 ceramic
        // class-4 forearm plates.
        new(
            new("5e9c4f1d8a2b4c3d7f0e3f01"), new("5e9c4f1d8a2b4c3d7f0e3fe1"),
            new("5e9c4f1d8a2b4c3d7f0e3a01"), "FORT Gladiator-S Killa Edition Standard",
            new[]
            {
                // hard plates
                (new MongoId("656fa99800d62bcd2e024088"), "Front_plate",              new MongoId("5e9c4f1d8a2b4c3d7f0e3fec")),
                (new MongoId("656fa99800d62bcd2e024088"), "Back_plate",               new MongoId("5e9c4f1d8a2b4c3d7f0e3fed")),
                (new MongoId("6557458f83942d705f0c4962"), "Left_side_plate",          new MongoId("5e9c4f1d8a2b4c3d7f0e3fee")),
                (new MongoId("6557458f83942d705f0c4962"), "Right_side_plate",         new MongoId("5e9c4f1d8a2b4c3d7f0e3fef")),
                // ceramic class-4 soft inserts
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b03"), "Soft_armor_front",         new MongoId("5e9c4f1d8a2b4c3d7f0e3fe2")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b04"), "Soft_armor_back",          new MongoId("5e9c4f1d8a2b4c3d7f0e3fe3")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b05"), "Soft_armor_left",          new MongoId("5e9c4f1d8a2b4c3d7f0e3fe4")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b06"), "soft_armor_right",         new MongoId("5e9c4f1d8a2b4c3d7f0e3fe5")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b07"), "Collar",                   new MongoId("5e9c4f1d8a2b4c3d7f0e3fe6")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b08"), "Shoulder_l",               new MongoId("5e9c4f1d8a2b4c3d7f0e3fe7")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b09"), "Shoulder_r",               new MongoId("5e9c4f1d8a2b4c3d7f0e3fe8")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b0a"), "Groin",                    new MongoId("5e9c4f1d8a2b4c3d7f0e3fe9")),
                // ceramic class-4 forearm plates
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b01"), "Soft_armor_left_forearm",  new MongoId("5e9c4f1d8a2b4c3d7f0e3fea")),
                (new MongoId("5e9c4f1d8a2b4c3d7f0e3b02"), "Soft_armor_right_forearm", new MongoId("5e9c4f1d8a2b4c3d7f0e3feb")),
            }),
    };

    public void Register()
    {
        var presets = globalTable.ItemPresets;
        if (presets == null)
        {
            logger.Error("[LegArmor] Globals.ItemPresets unavailable; presets not registered");
            return;
        }

        foreach (var spec in Presets)
        {
            var items = new List<Item>
            {
                new() { Id = spec.RootId, Template = spec.CarrierTpl },
            };
            foreach (var (plateTpl, slotName, childId) in spec.PlateChildren)
            {
                items.Add(new Item
                {
                    Id = childId,
                    Template = plateTpl,
                    ParentId = spec.RootId.ToString(),
                    SlotId = slotName,
                });
            }

            presets[spec.PresetId] = new Preset
            {
                Id = spec.PresetId,
                Type = "Preset",
                ChangeWeaponName = false,
                Name = spec.Name,
                Parent = spec.RootId,
                Encyclopedia = spec.CarrierTpl,
                Items = items,
            };
        }
        logger.Info($"[LegArmor] registered {Presets.Length} leg armor presets");
    }

    private record PresetSpec(
        MongoId PresetId,
        MongoId RootId,
        MongoId CarrierTpl,
        string Name,
        (MongoId Tpl, string SlotName, MongoId ChildId)[] PlateChildren
    );
}
