using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class HttpClient(System.Net.Http.HttpClient httpClient) : IHttpClient
{
    public async Task<string> GetStringAsync(string requestUri)
    {
        return await httpClient.GetStringAsync(requestUri);
    }
}
