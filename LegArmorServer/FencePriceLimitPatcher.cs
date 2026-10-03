using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace LegArmorMod;

// FenceService.AddPresetsToAssort indexes ItemCategoryRoublePriceLimit
// by parent without TryGetValue. our LegArmor parent isnt in the vanilla
// dict, so once Fence rolls our preset the server crashes. inject a limit.
[Injectable(InjectionType.Singleton)]
public class FencePriceLimitPatcher(
    TraderConfig traderConfig,
    ISptLogger<FencePriceLimitPatcher> logger)
{
    private static readonly MongoId LegArmorParent = new("5e9c4f1d8a2b4c3d7f0e1c00");

    // headroom for the most expensive variant (L3 Full at 40000). tune if
    // Fence over/under-prices the category.
    private const double PriceLimitRoubles = 50000;

    public void Apply()
    {
        var fenceConfig = traderConfig.Fence;
        if (fenceConfig?.ItemCategoryRoublePriceLimit == null)
        {
            logger.Error("[LegArmor] Fence.ItemCategoryRoublePriceLimit not available");
            return;
        }

        if (fenceConfig.ItemCategoryRoublePriceLimit.ContainsKey(LegArmorParent)) return;
        fenceConfig.ItemCategoryRoublePriceLimit[LegArmorParent] = PriceLimitRoubles;
        logger.Info($"[LegArmor] added Fence price limit {PriceLimitRoubles} for LegArmor parent {LegArmorParent}");
    }
}
