using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace LegArmorMod;

// single endpoint - client looks up the holder id on inventory open.
// equip/unequip/durability all flow through vanilla inventory transactions
// since the holder slot is a real Slot, so no extra endpoints needed.
[Injectable]
public class LegArmorCallbacks(
    LegArmorHolderService holderService,
    HttpResponseUtil httpResponseUtil)
{
    public ValueTask<string> GetHolder(string url, EmptyRequestData info, MongoId sessionId, string? output)
    {
        var holderId = holderService.EnsureForProfile(sessionId);
        var payload = new HolderResponse { HolderId = holderId.ToString() };
        return new ValueTask<string>(httpResponseUtil.NoBody(payload));
    }
}

public class HolderResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("holderId")]
    public string HolderId { get; set; } = string.Empty;
}
