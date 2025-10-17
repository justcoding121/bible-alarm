using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Services.Contracts;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    /// <summary>
    /// Download service
    /// </summary>
    public class DownloadService : IDownloadService
    {
        private readonly int downloadRetryAttempts = 3;
        private readonly int fileExistsCheckRetryAttempts = 3;

        private readonly int timeOutSeconds = 3;

        private HttpMessageHandler handler;
        public DownloadService(HttpMessageHandler handler)
        {
            this.handler = handler;
        }

        /// <summary>
        /// Dowload the file from the Url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<byte[]> DownloadAsync(string url, string alternativeUrl = null)
        {
            return await RetryHelper.Retry(async () =>
            {
                try
                {
                    using var client = new HttpClient(handler, false);
                    return await client.GetByteArrayAsync(url);
                }
                catch
                {
                    if (alternativeUrl == null)
                    {
                        throw;
                    }

                    using var client = new HttpClient(handler, false);
                    return await client.GetByteArrayAsync(alternativeUrl);
                }

            }, downloadRetryAttempts);
        }


        public void Dispose()
        {
            handler.Dispose();
        }

        public async Task<bool> FileExists(string url)
        {
            return await RetryHelper.Retry(async () =>
            {
                using var client = new HttpClient(handler, false)
                {
                    Timeout = TimeSpan.FromSeconds(timeOutSeconds)
                };

                Func<Task<bool>> getRequest = async () =>
                {
                    var result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));
                    var statusCode = result.StatusCode;

                    if (statusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }

                    return false;
                };

                try
                {
                    HttpResponseMessage result = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                    var statusCode = result.StatusCode;

                    if (statusCode == HttpStatusCode.Accepted
                    || statusCode == HttpStatusCode.OK)
                    {
                        return true;
                    }

                    return await getRequest();
                }
                catch
                {
                    return await getRequest();
                }

            }, fileExistsCheckRetryAttempts);
        }
    }
}