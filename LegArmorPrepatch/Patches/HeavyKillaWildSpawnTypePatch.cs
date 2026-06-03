using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx.Logging;
using Mono.Cecil;
using MoreBotsAPI;

namespace Manimal.LegArmor.Prepatch.Patches
{
    // injects heavyKilla into EFT's WildSpawnType enum at game load. without
    // this the EFT client doesn't know id 9472831 exists, so the server can
    // register a BossLocationSpawn referencing the bot but the client will
    // never actually instantiate it - which is why heavyKilla wasn't
    // spawning and wasn't appearing in client-side bot picker UIs.
    //
    // brain id 6 = bossKilla in the WildSpawnType enum order (counting from
    // marksman=0). heavyKilla uses the same AI brain.
    public static class HeavyKillaWildSpawnTypePatch
    {
        public const int WildSpawnTypeValue = 9472831;
        public const string BotTypeName = "heavykilla";
        public const string ScavRole = "HeavyKilla";

        // matches bossKilla's position in the WildSpawnType enum.
        private const int BossKillaBrainId = 6;

        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition assembly)
        {
            var log = Logger.CreateLogSource("LegArmor Prepatch");

            if (!MoreBotsAPIInstalled())
            {
                log.LogError("MoreBotsAPI plugin not detected. heavyKilla WildSpawnType will not be registered.");
                return;
            }

            var bot = new CustomWildSpawnType(
                WildSpawnTypeValue,
                BotTypeName,
                ScavRole,
                BossKillaBrainId,
                isBoss: true,
                isFollower: false,
                isHostileToEverybody: false);

            bot.SetCountAsBossForStatistics(true);
            bot.SetShouldUseFenceNoBossAttack(true, false);

            var settings = new SAINSettings(bot.WildSpawnTypeValue)
            {
                Name = "Heavy Killa",
                Description = "An armored Killa variant with a PKM.",
                Section = "Bosses",
                BaseBrain = "BossKilla",
                BrainsToApply = new List<string> { "BossKilla" },
                DifficultyModifier = 0.85f,
                LayersToRemove = new List<string>(),
            };
            bot.SetSAINSettings(settings);

            CustomWildSpawnTypeManager.RegisterWildSpawnType(bot, assembly);

            log.LogInfo($"Registered {BotTypeName} WildSpawnType ({WildSpawnTypeValue}).");
        }

        private static bool MoreBotsAPIInstalled()
        {
            var patcherLoc = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var bepDir = Directory.GetParent(patcherLoc)?.Parent;
            if (bepDir == null) return false;
            var modDllLoc = Path.Combine(bepDir.FullName, "plugins", "MoreBotsAPI", "MoreBotsPlugin.dll");
            return File.Exists(modDllLoc);
        }
    }
}
