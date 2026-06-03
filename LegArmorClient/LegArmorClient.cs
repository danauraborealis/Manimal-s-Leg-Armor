using System.Threading.Tasks;
using Newtonsoft.Json;
using SPT.Common.Http;

namespace Manimal.LegArmor
{
    // single GET - returns the holder item id (server creates it if missing).
    // everything else is profile JSON managed by EFT.
    internal static class LegArmorClient
    {
        public class HolderResponse
        {
            [JsonProperty("holderId")]
            public string HolderId;
        }

        public static async Task<string> GetHolderIdAsync()
        {
            try
            {
                var json = await RequestHandler.GetJsonAsync("/legarmor/holder");
                var resp = JsonConvert.DeserializeObject<HolderResponse>(json);
                return resp?.HolderId;
            }
            catch (System.Exception ex)
            {
                Plugin.LogSource?.LogError($"[LegArmor] GET /legarmor/holder failed: {ex.Message}");
                return null;
            }
        }
    }
}
