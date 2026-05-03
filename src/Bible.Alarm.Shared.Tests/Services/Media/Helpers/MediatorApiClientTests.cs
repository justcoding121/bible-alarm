#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorApiClientTests
{
    private sealed class JsonHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }

    private sealed class FailHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    [Fact]
    public void Constructor_Throws_When_HttpClient_Null()
    {
        Assert.Throws<ArgumentNullException>(() => new MediatorApiClient(null!, TestLogging.CreateLogger()));
    }

    [Fact]
    public void Constructor_Throws_When_Logger_Null()
    {
        using var http = new HttpClient(new FailHandler());
        Assert.Throws<ArgumentNullException>(() => new MediatorApiClient(http, null!));
    }

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsEmpty_WhenPublicationNotCanonicalMediatorCode()
    {
        using var http = new HttpClient(new FailHandler());
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (name, tracks) = await sut.FetchCategoryAndTracksAsync(
            normalizedPublicationCode: "not-listed-mediator-code-zzz",
            normalizedLanguageCode: "E",
            CancellationToken.None);

        Assert.Null(name);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsLocalizedNameAndTracks_FromMinimalValidJson()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var nk = AppConstants.Media.PubMediaJson.NaturalKey;
        var pc = AppConstants.Media.PubMediaJson.PrimaryCategory;
        var files = AppConstants.Media.PubMediaJson.Files;
        var pdu = AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl;
        var title = AppConstants.Media.PubMediaJson.Title;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var dramaKey = AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"Dramas show\",\""
            + media
            + "\":[{\""
            + nk
            + "\":\"k1\",\""
            + pc
            + "\":\""
            + dramaKey
            + "\",\""
            + files
            + "\":[{\""
            + pdu
            + "\":\"https://cdn/mediator/track.mp4\"}],\""
            + title
            + "\":\"Pilot\"}]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Equal("Dramas show", localized);
        var t = Assert.Single(tracks);
        Assert.Equal("1", t.TrackCode);
        Assert.Equal("Pilot", t.Title);
        Assert.Equal("https://cdn/mediator/track.mp4", t.Url);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsEmptyTracks_WhenHttpFails()
    {
        using var http = new HttpClient(new FailHandler());
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (_, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsEmptyTracks_WhenRootMissingCategory_Object()
    {
        using var http = new HttpClient(new JsonHandler("{}"));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (_, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Empty(tracks);
    }
}
