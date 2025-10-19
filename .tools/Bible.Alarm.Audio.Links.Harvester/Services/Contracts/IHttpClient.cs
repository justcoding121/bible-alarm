using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvester.Services.Contracts;

public interface IHttpClient
{
    Task<string> GetStringAsync(string requestUri);
}
