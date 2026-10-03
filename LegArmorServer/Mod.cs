using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using SPTarkov.Reflection.Patching;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace LegArmorMod;


public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = Manimal.LegArmor.ModInfo.Guid;
    public string Name { get; init; } = Manimal.LegArmor.ModInfo.ServerName;
    public string Author { get; init; } = Manimal.LegArmor.ModInfo.Author;
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(Manimal.LegArmor.ModInfo.Version);
    public SemanticVersioning.Range SptVersion { get; init; } = new(Manimal.LegArmor.ModInfo.SptVersion);
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new SemanticVersioning.Range("~3.0.6") },
        { "com.morebotsapi.tacticaltoaster", new SemanticVersioning.Range("~2.1.1") }
    };
    public string? Url { get; init; } = Manimal.LegArmor.ModInfo.SourceUrl;
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public class LegArmorServer(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    PocketsGridInjectorService pocketsGridInjector,
    LegArmorPresetService presetService,
    FencePriceLimitPatcher fencePriceLimit,
    LegArmorBotsConfigService botsConfig,
    IEnumerable<IRuntimePatch> patches) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assembly assembly = Assembly.GetExecutingAssembly();

        // parents before items - items reference our parent id.
        await wttCommon.CustomItemParentService.CreateCustomParents(assembly);
        cancellationToken.ThrowIfCancellationRequested();
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
        cancellationToken.ThrowIfCancellationRequested();
        await wttCommon.CustomLocaleService.CreateCustomLocales(assembly);
        cancellationToken.ThrowIfCancellationRequested();

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

        // Enable only this assembly's patches. SPT enforces ownership, so
        // another mod's patch loader cannot activate these on our behalf.
        foreach (var patch in patches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (patch.GetType().Assembly == assembly)
                patch.Enable();
        }
    }
}
