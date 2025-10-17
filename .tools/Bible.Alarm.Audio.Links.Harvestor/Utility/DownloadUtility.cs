using System.Net.Http;
using System.Threading.Tasks;

namespace AudioLinkHarvester.Utility
{
    internal class DownloadUtility
    {
        internal static async Task<string> GetAsync(string harvestLink)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(harvestLink);
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
}
