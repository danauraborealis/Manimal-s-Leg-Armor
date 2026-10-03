using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Profile;

namespace LegArmorMod.Patches;

// CreateProfile is asynchronous. Wrap its ValueTask so holder injection runs
// after the profile has been saved and reloaded, before its id is returned.
[Injectable(InjectionType.Singleton)]
public sealed class ProfileCreateHolderInjectPatch : AbstractPatch
{
    private static ProfileCreateHolderInjectPatch? _instance;

    private readonly LegArmorHolderService _holderService;
    private readonly ISptLogger<ProfileCreateHolderInjectPatch> _logger;

    public ProfileCreateHolderInjectPatch(
        LegArmorHolderService holderService,
        ISptLogger<ProfileCreateHolderInjectPatch> logger)
        : base(Manimal.LegArmor.ModInfo.Guid + ".profile-create")
    {
        _holderService = holderService;
        _logger = logger;
        _instance = this;
    }

    protected override MethodBase? GetTargetMethod() =>
        AccessTools.Method(typeof(CreateProfileService), nameof(CreateProfileService.CreateProfile));

    [PatchPostfix]
    private static void Postfix(MongoId sessionId, ref ValueTask<string> __result)
    {
        __result = WrapAsync(__result, sessionId);
    }

    private static async ValueTask<string> WrapAsync(ValueTask<string> original, MongoId sessionId)
    {
        var result = await original;
        try
        {
            _instance?._holderService.EnsureForProfile(sessionId);
            _instance?._holderService.FlagExaminedForProfile(sessionId);
        }
        catch (System.Exception ex)
        {
            _instance?._logger.Error($"[LegArmor] holder inject on profile create failed for {sessionId}: {ex}");
        }
        return result;
    }
}
