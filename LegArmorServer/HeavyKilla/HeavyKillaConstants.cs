namespace LegArmorMod.HeavyKilla;

// names + wild-spawn-type id for the heavyKilla custom boss. WildSpawnType
// is a vanilla enum; values above ~700000 are unused so we pick a unique
// number that wont collide with other mods (matches MitsuruMod's pattern).
internal static class HeavyKillaConstants
{
    public const string BotTypeName = "heavykilla";
    public const int WildSpawnTypeValue = 9472831;

    // existing faction shared by Killa + Tagilla. adding our spawn type
    // here gives heavyKilla the same enemy/friend relations as Killa
    // without standing up a brand-new faction.
    public const string SharedFactionName = "killaTagilla";

    // chance (0-100) that an incoming bossKilla generation is intercepted
    // and rewritten to heavyKilla instead. 50 = half of Killa spawns turn
    // into HeavyKilla; lower = rarer variant, higher = more common.
    public const int ReplaceKillaChance = 50;

    // the vanilla SPT role name we replace. lowercase to match SPT's
    // internal role string convention.
    public const string VanillaKillaRole = "bosskilla";
}
