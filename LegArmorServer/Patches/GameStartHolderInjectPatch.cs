using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace LegArmorMod.Patches;

// inject the holder at /client/game/start so its in the profile json
// before the client loads its inventory. lazy injection on first
// /legarmor/holder hit is too late - the client has already fetched.
//
// wired by hand from Mod.cs because PatchAll cant inject the service.
[Injectable(InjectionType.Singleton)]
public class GameStartHolderInjectPatch(
    LegArmorHolderService holderService,
    ISptLogger<GameStartHolderInjectPatch> logger)
{
    public void Apply(Harmony harmony)
    {
        var target = AccessTools.Method(typeof(GameController), nameof(GameController.GameStart));
        if (target == null)
        {
            logger.Error("[LegArmor] GameController.GameStart not found; holder will not auto-inject");
            return;
        }

        var postfix = new HarmonyMethod(typeof(GameStartHolderInjectPatch), nameof(PostfixStatic));
        harmony.Patch(target, postfix: postfix);

        _instance = this;
    }

    private static GameStartHolderInjectPatch? _instance;

    public static void PostfixStatic(MongoId sessionId)
    {
        try
        {
            _instance?.holderService.EnsureForProfile(sessionId);
            _instance?.holderService.FlagExaminedForProfile(sessionId);
        }
        catch (System.Exception ex)
        {
            _instance?.logger.Error($"[LegArmor] holder inject on GameStart failed for {sessionId}: {ex}");
        }
    }

    private LegArmorHolderService holderService { get; } = holderService;
    private ISptLogger<GameStartHolderInjectPatch> logger { get; } = logger;
}
