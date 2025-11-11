using System.Net.Http;
using System.Threading.Tasks;

namespace Bible.Alarm.Shared.Helpers;

public class DownloadUtility
{
    public static async Task<string> GetAsync(string harvestLink)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(harvestLink);
        return await response.Content.ReadAsStringAsync();
    }
}