using System.Threading;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils;

namespace LegArmorMod;

// custom URL prefix outside the /client/ tree so it cant collide with
// future BSG routes.
[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public class LegArmorRouter(LegArmorCallbacks legArmorCallbacks, JsonUtil jsonUtil)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction<EmptyRequestData>(
                "/legarmor/holder",
                async (url, info, sessionId, output, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await legArmorCallbacks.GetHolder(url, info, sessionId, output);
                }
            ),
        ]
    ) { }
