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

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsLocalizedNameWithoutTracks_WhenCategoryMediaMissing()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var json = "{\"" + cat + "\":{\"" + nm + "\":\"Dramas only\"}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Equal("Dramas only", localized);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsLocalizedNameWithoutTracks_WhenCategoryMediaNotArray()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"X\",\""
            + media
            + "\":{}}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Equal("X", localized);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_ReturnsLocalizedNameWithoutTracks_WhenCategoryMediaIsEmptyArray()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"Empty list\",\""
            + media
            + "\":[]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Equal("Empty list", localized);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_CombinesParentCategoryNameWhenCategoryNameIsDramas()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var pc = AppConstants.Media.PubMediaJson.ParentCategory;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var dramas = AppConstants.Media.BiblePublicationCategoryDramas;
        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\""
            + dramas
            + "\",\""
            + pc
            + "\":{\""
            + nm
            + "\":\"Audio Shows\"},\""
            + media
            + "\":[]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Equal("Audio Shows " + dramas, localized);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_FiltersTracksWhenPrimaryCategoryDoesNotMatchNonAggregatorPublication()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var nk = AppConstants.Media.PubMediaJson.NaturalKey;
        var pri = AppConstants.Media.PubMediaJson.PrimaryCategory;
        var files = AppConstants.Media.PubMediaJson.Files;
        var pdu = AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var dramaKey = AppConstants.Media.BiblePublicationCodeDramasGoodNews;
        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"Talks\",\""
            + media
            + "\":[{\"" + nk + "\":\"k1\",\"" + pri + "\":\""
            + dramaKey
            + "\",\"" + files + "\":[{\"" + pdu + "\":\"https://cdn/t.mp4\"}]}]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            normalizedPublicationCode: "studiotalks",
            "E",
            CancellationToken.None);

        Assert.Equal("Talks", localized);
        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_AcceptsSeriesBibleBooksPrimaryCategoryWhenPublicationIsBibleBooks()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var nk = AppConstants.Media.PubMediaJson.NaturalKey;
        var pri = AppConstants.Media.PubMediaJson.PrimaryCategory;
        var files = AppConstants.Media.PubMediaJson.Files;
        var pdu = AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl;
        var title = AppConstants.Media.PubMediaJson.Title;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var seriesBooks = AppConstants.Media.MediatorCategoryKeySeriesBibleBooks;

        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"Bible Books\",\""
            + media
            + "\":[{\"" + nk + "\":\"bk1\",\"" + pri + "\":\""
            + seriesBooks
            + "\",\"" + files + "\":[{\"" + pdu + "\":\"https://cdn/b.mp4\"}],\"" + title + "\":\"Genesis\"}]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (localized, tracks) = await sut.FetchCategoryAndTracksAsync(
            normalizedPublicationCode: "biblebooks",
            "E",
            CancellationToken.None);

        Assert.Equal("Bible Books", localized);
        var t = Assert.Single(tracks);
        Assert.Equal("1", t.TrackCode);
        Assert.Equal("Genesis", t.Title);
        Assert.Equal("https://cdn/b.mp4", t.Url);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_SkipsMediaItemWithoutNaturalKey()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var pc = AppConstants.Media.PubMediaJson.PrimaryCategory;
        var files = AppConstants.Media.PubMediaJson.Files;
        var pdu = AppConstants.Media.PubMediaJson.ProgressiveDownloadUrl;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var dramaKey = AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"D\",\""
            + media
            + "\":[{\""
            + pc
            + "\":\""
            + dramaKey
            + "\",\""
            + files
            + "\":[{\"" + pdu + "\":\"https://cdn/x.mp4\"}]}]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (_, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Empty(tracks);
    }

    [Fact]
    public async Task FetchCategoryAndTracks_SkipsMediaItemWithoutProgressiveDownloadUrl()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var media = AppConstants.Media.PubMediaJson.CategoryMedia;
        var nk = AppConstants.Media.PubMediaJson.NaturalKey;
        var pc = AppConstants.Media.PubMediaJson.PrimaryCategory;
        var files = AppConstants.Media.PubMediaJson.Files;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var dramaKey = AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        var json =
            "{\""
            + cat
            + "\":{\""
            + nm
            + "\":\"D\",\""
            + media
            + "\":[{\"" + nk + "\":\"k1\",\""
            + pc
            + "\":\""
            + dramaKey
            + "\",\""
            + files
            + "\":[{}]}]}}";

        using var http = new HttpClient(new JsonHandler(json));
        var sut = new MediatorApiClient(http, TestLogging.CreateLogger());

        var (_, tracks) = await sut.FetchCategoryAndTracksAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E",
            CancellationToken.None);

        Assert.Empty(tracks);
    }
}
