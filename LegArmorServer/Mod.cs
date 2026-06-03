using System.Reflection;
using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace LegArmorMod;


public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = Manimal.LegArmor.ModInfo.Guid;
    public override string Name { get; init; } = Manimal.LegArmor.ModInfo.ServerName;
    public override string Author { get; init; } = Manimal.LegArmor.ModInfo.Author;
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new(Manimal.LegArmor.ModInfo.Version);
    public override SemanticVersioning.Range SptVersion { get; init; } = new(Manimal.LegArmor.ModInfo.SptVersion);
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new SemanticVersioning.Range("~2.0.20") },
        { "com.morebotsapi.tacticaltoaster", new SemanticVersioning.Range(">=2.0.0") }
    };
    public override string? Url { get; init; } = "";
    public override bool? IsBundleMod { get; init; } = true;
    public override string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 2)]
public class LegArmorServer(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    PocketsGridInjectorService pocketsGridInjector,
    LegArmorPresetService presetService,
    FencePriceLimitPatcher fencePriceLimit,
    LegArmorBotsConfigService botsConfig,
    Patches.GameStartHolderInjectPatch gameStartPatch,
    Patches.LoseCarrierOnDeathPatch loseCarrierPatch,
    Patches.BotGenerateHolderInjectPatch botInjectPatch,
    HeavyKilla.HeavyKillaReplaceKillaPatch heavyKillaReplacePatch) : IOnLoad
{
    public async Task OnLoad()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();

        // parents before items - items reference our parent id.
        await wttCommon.CustomItemParentService.CreateCustomParents(assembly);
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
        await wttCommon.CustomLocaleService.CreateCustomLocales(assembly);

        // adds the hidden grid + flips HideEntrails. must run AFTER custom
        // items so the holder tpl is known to the grid filter.
        pocketsGridInjector.Inject();

        // GiveUI / trader / preset spawn paths clone from globals.ItemPresets;
        // without entries here, leg armor carriers spawn with empty slots.
        presetService.Register();

        // Fence indexes ItemCategoryRoublePriceLimit[parent] without
        // TryGetValue - our parent must exist or Fence assort gen crashes.
        fencePriceLimit.Apply();

        // load bot spawn config so the bot inject patch can consult it.
        botsConfig.Load();

        // PatchAll picks up [HarmonyPatch]-attributed classes.
        // body-armor-slot rejection lives client-side (SPT's SlotFilter DTO
        // doesnt expose ExcludedFilter).
        var harmony = new Harmony(Manimal.LegArmor.ModInfo.Guid);
        harmony.PatchAll(assembly);

        // these need DI services so they cant ride PatchAll.
        gameStartPatch.Apply(harmony);
        loseCarrierPatch.Apply(harmony);
        botInjectPatch.Apply(harmony);
        heavyKillaReplacePatch.Apply(harmony);
    }
}
