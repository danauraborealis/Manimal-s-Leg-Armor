using System.Reflection;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;

namespace LegArmorMod.Patches;

// Inject the holder at /client/game/start so it is present in the profile
// before the client loads its inventory.
[Injectable(InjectionType.Singleton)]
public sealed class GameStartHolderInjectPatch : AbstractPatch
{
    private static GameStartHolderInjectPatch? _instance;

    private readonly LegArmorHolderService _holderService;
    private readonly ISptLogger<GameStartHolderInjectPatch> _logger;

    public GameStartHolderInjectPatch(
        LegArmorHolderService holderService,
        ISptLogger<GameStartHolderInjectPatch> logger)
        : base(Manimal.LegArmor.ModInfo.Guid + ".game-start")
    {
        _holderService = holderService;
        _logger = logger;
        _instance = this;
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(GameController), nameof(GameController.GameStart));

    [PatchPostfix]
    private static void Postfix(MongoId sessionId)
    {
        try
        {
            _instance?._holderService.EnsureForProfile(sessionId);
            _instance?._holderService.FlagExaminedForProfile(sessionId);
        }
        catch (System.Exception ex)
        {
            _instance?._logger.Error($"[LegArmor] holder inject on GameStart failed for {sessionId}: {ex}");
        }
    }
}
