using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Infrastructure;

public class HttpClient : IHttpClient
{
    private readonly System.Net.Http.HttpClient _httpClient;

    public HttpClient(System.Net.Http.HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetStringAsync(string requestUri)
    {
        return await _httpClient.GetStringAsync(requestUri);
    }
}
