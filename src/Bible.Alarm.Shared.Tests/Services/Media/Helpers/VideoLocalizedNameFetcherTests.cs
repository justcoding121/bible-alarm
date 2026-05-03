#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Services.Media.Helpers;
using Bible.Alarm.Shared.Tests.Support;
using Serilog;

namespace Bible.Alarm.Shared.Tests;

public sealed class VideoLocalizedNameFetcherTests
{
    private static VideoLocalizedNameFetcher CreateSut(HttpMessageHandler handler, ILogger? logger = null) =>
        new(new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) }, logger ?? TestLogging.CreateLogger());

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private static string CategoryNameJson(string name)
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        return "{\"" + cat + "\":{\"" + nm + "\":\"" + name + "\"}}";
    }

    [Fact]
    public void Constructor_Throws_When_HttpClient_Is_Null()
        => Assert.Throws<ArgumentNullException>(() => new VideoLocalizedNameFetcher(null!, TestLogging.CreateLogger()));

    [Fact]
    public void Constructor_Throws_When_Logger_Is_Null()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var http = new HttpClient(handler);
        Assert.Throws<ArgumentNullException>(() => new VideoLocalizedNameFetcher(http, null!));
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Code_Is_Not_AMediatorMapped_VideoPublication()
    {
        using var handler = new StubHandler(_ =>
            throw new InvalidOperationException("HTTP must not run for unmapped publication codes."));
        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            normalizedPublicationCode: "not-mapped-to-mediator",
            normalizedLanguageCode: "E");

        Assert.Null(result);
    }

    [Fact]
    public async Task Fetch_Returns_Localized_Name_When_Response_Has_Category_Name()
    {
        var expectedSuffix = $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/E/"
            + AppConstants.Media.BiblePublicationCodeDramasGoodNews;

        using var handler = new StubHandler(request =>
        {
            Assert.True(
                request.RequestUri?.AbsoluteUri.Contains(expectedSuffix, StringComparison.OrdinalIgnoreCase) == true,
                $"Unexpected request URI: {request.RequestUri}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CategoryNameJson("Dramas localized title")),
            };
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Equal("Dramas localized title", result);
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_All_Http_Attempts_Fail()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Null(result);
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Category_Name_Property_Is_Absent_Or_Empty()
    {
        var catKey = AppConstants.Media.PubMediaJson.Category;

        using var handlerMissingName = new StubHandler(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"" + catKey + "\":{}}"),
            };
        });

        var sutMissing = CreateSut(handlerMissingName);

        Assert.Null(await sutMissing.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E"));

        using var handlerEmpty = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CategoryNameJson("")),
        });

        var sutEmpty = CreateSut(handlerEmpty);

        Assert.Null(await sutEmpty.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            "E"));
    }

    [Fact]
    public async Task Fetch_Returns_Localized_Name_With_Decoded_Html_And_NBSP()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(CategoryNameJson("Video&nbsp;Titles&nbsp;A")),
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Equal("Video Titles A", result);
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Http_Handler_Throws_Non_Transient_Exception()
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("simulated Transport failure"));

        var sut = CreateSut(handler);

        Assert.Null(await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E"));
    }

    [Fact]
    public async Task Fetch_Returns_Null_When_Response_Is_Not_Valid_Json()
    {
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json-at-all"),
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            AppConstants.Media.NormalizedPublicationCodeDramasGoodNews,
            normalizedLanguageCode: "E");

        Assert.Null(result);
    }

    public static TheoryData<string, string> MediatorMappedVideoPublicationPairs { get; } =
        new()
        {
            { AppConstants.Media.NormalizedPublicationCodeVODMoviesBibleTimes, AppConstants.Media.BiblePublicationCodeVODMoviesBibleTimes },
            { AppConstants.Media.NormalizedPublicationCodeVODMoviesModernDay, AppConstants.Media.BiblePublicationCodeVODMoviesModernDay },
            { AppConstants.Media.NormalizedPublicationCodeVODMoviesAnimated, AppConstants.Media.BiblePublicationCodeVODMoviesAnimated },
            { AppConstants.Media.NormalizedPublicationCodeVODMoviesExtras, AppConstants.Media.BiblePublicationCodeVODMoviesExtras },
            { AppConstants.Media.NormalizedPublicationCodeSeriesDigForTreasures, AppConstants.Media.BiblePublicationCodeSeriesDigForTreasures },
            { AppConstants.Media.NormalizedPublicationCodeSeriesBJFLessons, AppConstants.Media.BiblePublicationCodeSeriesBJFLessons },
        };

    [Theory]
    [MemberData(nameof(MediatorMappedVideoPublicationPairs))]
    public async Task Fetch_HitsMediatorPath_ForEachMappedVideoPublication(string normalizedPublicationCode, string expectedCategorySegment)
    {
        var expectedSubstring =
            $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/E/{expectedCategorySegment}";

        using var handler = new StubHandler(request =>
        {
            Assert.True(
                request.RequestUri?.AbsoluteUri.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase) == true,
                $"Unexpected request URI for {normalizedPublicationCode}: {request.RequestUri}");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CategoryNameJson("Localized")),
            };
        });

        var sut = CreateSut(handler);

        var result = await sut.FetchVideoLocalizedNameFromMediatorAsync(
            normalizedPublicationCode,
            normalizedLanguageCode: "E");

        Assert.Equal("Localized", result);
    }
}
