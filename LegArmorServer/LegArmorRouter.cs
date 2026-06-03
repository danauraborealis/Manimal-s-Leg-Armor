using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace LegArmorMod;

// custom URL prefix outside the /client/ tree so it cant collide with
// future BSG routes.
[Injectable]
public class LegArmorRouter(LegArmorCallbacks legArmorCallbacks, JsonUtil jsonUtil)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/legarmor/holder",
                async (url, info, sessionId, output) => await legArmorCallbacks.GetHolder(url, info, sessionId, output)
            ),
        ]
    ) { }
