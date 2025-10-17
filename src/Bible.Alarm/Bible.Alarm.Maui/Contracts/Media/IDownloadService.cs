using System;
using System.Threading.Tasks;

namespace Bible.Alarm.Services.Contracts
{
    public interface IDownloadService : IDisposable
    {
        Task<byte[]> DownloadAsync(string url, string alternativeUrl = null);
        Task<bool> FileExists(string url);
    }
}
