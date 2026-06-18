#nullable enable

using System.Net;
using System.Text;
using Bible.Alarm.Cataloger.Utility;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class DownloadUtilityTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public async Task StubDownloadUtility_records_requested_url_and_returns_stubbed_body()
    {
        const string expectedUrl = "https://example.test/catalog";
        const string expectedBody = """{"languages":[]}""";
        var sut = new StubDownloadUtility(SilentLogger, url =>
        {
            Assert.Equal(expectedUrl, url);
            return Task.FromResult(expectedBody);
        });

        var result = await sut.GetAsync(expectedUrl);

        Assert.Equal(expectedBody, result);
        Assert.Single(sut.RequestedUrls);
        Assert.Equal(expectedUrl, sut.RequestedUrls[0]);
    }

    [Fact]
    public async Task GetAsync_returns_response_body_from_local_http_listener()
    {
        using var listener = StartListener(out var baseUrl, "{\"status\":\"ok\"}");
        var sut = new DownloadUtility(SilentLogger);

        var result = await sut.GetAsync($"{baseUrl}catalog.json");

        Assert.Contains("ok", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_throws_http_request_exception_when_server_returns_not_found()
    {
        using var listener = StartListener(out var baseUrl, "missing", HttpStatusCode.NotFound);
        var sut = new DownloadUtility(SilentLogger);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sut.GetAsync($"{baseUrl}missing.json"));

        Assert.Contains("Response status code", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_retries_server_busy_then_succeeds()
    {
        var attempts = 0;
        using var listener = StartListener(
            out var baseUrl,
            _ =>
            {
                attempts++;
                return attempts < 2
                    ? ("busy", HttpStatusCode.ServiceUnavailable)
                    : ("{\"ok\":true}", HttpStatusCode.OK);
            });
        var sut = new DownloadUtility(SilentLogger);

        var result = await sut.GetAsync($"{baseUrl}retry.json");

        Assert.Contains("ok", result, StringComparison.Ordinal);
        Assert.Equal(2, attempts);
    }

    private static HttpListener StartListener(
        out string baseUrl,
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return StartListener(out baseUrl, _ => (body, statusCode));
    }

    private static HttpListener StartListener(
        out string baseUrl,
        Func<HttpListenerContext, (string Body, HttpStatusCode StatusCode)> respond)
    {
        var listener = new HttpListener();
        var port = GetFreePort();
        baseUrl = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(baseUrl);
        listener.Start();

        _ = Task.Run(async () =>
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException)
                {
                    break;
                }

                var (body, statusCode) = respond(context);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = (int)statusCode;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                context.Response.Close();
            }
        });

        return listener;
    }

    private static int GetFreePort()
    {
        using var socket = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        socket.Start();
        var port = ((System.Net.IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }
}
