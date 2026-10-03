using System.Reflection;
using System.Text.Json;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace LegArmorMod;

// loads config/bots.json from next to the mod dll. exposes typed config to
// the bot injector. falls back to baked-in defaults if the file is missing
// or malformed so the mod still works on a fresh install.
[Injectable(InjectionType.Singleton)]
public class LegArmorBotsConfigService(ISptLogger<LegArmorBotsConfigService> logger)
{
    public BotsConfig Config { get; private set; } = Default();

    public void Load()
    {
        try
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) return;
            var path = Path.Combine(dir, "config", "bots.json");
            if (!File.Exists(path))
            {
                logger.Warning($"[LegArmor] config not found at {path}; using defaults");
                return;
            }
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<BotsConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (parsed == null)
            {
                logger.Error("[LegArmor] bots.json parsed to null; using defaults");
                return;
            }
            Config = parsed;
            logger.Info("[LegArmor] bots.json loaded");
        }
        catch (System.Exception ex)
        {
            logger.Error($"[LegArmor] failed to load bots.json: {ex.Message}; using defaults");
        }
    }

    private static BotsConfig Default() => new()
    {
        Variants = new Dictionary<string, VariantConfig>
        {
            ["L2_Thighs"] = new()
            {
                CarrierTpl = "5e9c4f1d8a2b4c3d7f0e2a01",
                Plates =
                {
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b01", Slot = "Soft_armor_left_thigh" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b02", Slot = "Soft_armor_right_thigh" },
                },
            },
            ["L3_Thighs"] = new()
            {
                CarrierTpl = "5e9c4f1d8a2b4c3d7f0e2a02",
                Plates =
                {
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b05", Slot = "Soft_armor_left_thigh" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b06", Slot = "Soft_armor_right_thigh" },
                },
            },
            ["L2_Full"] = new()
            {
                CarrierTpl = "5e9c4f1d8a2b4c3d7f0e2a03",
                Plates =
                {
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b01", Slot = "Soft_armor_left_thigh" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b02", Slot = "Soft_armor_right_thigh" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b03", Slot = "Soft_armor_left_calf" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b04", Slot = "Soft_armor_right_calf" },
                },
            },
            ["L3_Full"] = new()
            {
                CarrierTpl = "5e9c4f1d8a2b4c3d7f0e2a04",
                Plates =
                {
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b05", Slot = "Soft_armor_left_thigh" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b06", Slot = "Soft_armor_right_thigh" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b07", Slot = "Soft_armor_left_calf" },
                    new() { Tpl = "5e9c4f1d8a2b4c3d7f0e2b08", Slot = "Soft_armor_right_calf" },
                },
            },
        },
        Scav = new BotTypeConfig
        {
            SpawnChance = 15,
            Variants = new Dictionary<string, int>
            {
                ["L2_Thighs"] = 4, ["L2_Full"] = 1, ["L3_Thighs"] = 0, ["L3_Full"] = 0,
            },
        },
        Pmc = new BotTypeConfig
        {
            SpawnChance = 25,
            Variants = new Dictionary<string, int>
            {
                ["L2_Thighs"] = 2, ["L2_Full"] = 3, ["L3_Thighs"] = 1, ["L3_Full"] = 2,
            },
        },
        Raider = new BotTypeConfig
        {
            SpawnChance = 35,
            Variants = new Dictionary<string, int>
            {
                ["L2_Thighs"] = 0, ["L2_Full"] = 1, ["L3_Thighs"] = 1, ["L3_Full"] = 4,
            },
        },
        Boss = new BotTypeConfig
        {
            SpawnChance = 50,
            Variants = new Dictionary<string, int>
            {
                ["L2_Thighs"] = 0, ["L2_Full"] = 0, ["L3_Thighs"] = 1, ["L3_Full"] = 4,
            },
        },
    };

    public class BotsConfig
    {
        // catalog of every variant the bot weight tables can reference.
        // keyed by display name; add an entry here to expose a new carrier
        // to bot generation.
        public Dictionary<string, VariantConfig> Variants { get; set; } = new();

        public BotTypeConfig Scav { get; set; } = new();
        public BotTypeConfig Pmc { get; set; } = new();
        public BotTypeConfig Raider { get; set; } = new();
        public BotTypeConfig Boss { get; set; } = new();

        // exact-role overrides (case-insensitive). takes precedence over the
        // bucket fallback. e.g. "bossKilla" -> distinct chance + weights.
        public Dictionary<string, BotTypeConfig> Overrides { get; set; } = new();
    }

    public class BotTypeConfig
    {
        public int SpawnChance { get; set; }
        public Dictionary<string, int> Variants { get; set; } = new();
    }

    public class VariantConfig
    {
        public string CarrierTpl { get; set; } = "";
        public List<PlateConfig> Plates { get; set; } = new();
    }

    public class PlateConfig
    {
        public string Tpl { get; set; } = "";
        public string Slot { get; set; } = "";
    }
}
