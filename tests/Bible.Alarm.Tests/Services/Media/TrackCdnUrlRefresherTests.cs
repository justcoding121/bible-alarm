#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class TrackCdnUrlRefresherTests
{
    private abstract class IdleLanguageContentServiceBase : ILanguageContentService
    {
        public Task<string?> GetVideoPublicationDisplayNameAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        Task<bool> ILanguageContentService.FetchPublicationTracksAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken) =>
            FetchPublicationTracksAsync(publicationCode, languageCode, cancellationToken);

        public virtual Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> FetchPublicationSectionsAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        Task<bool> ILanguageContentService.FetchSectionTracksAsync(string publicationCode, string sectionCode,
            string languageCode, bool replaceExistingTracksFromApi, CancellationToken cancellationToken) =>
            FetchSectionTracksAsync(publicationCode, sectionCode, languageCode, replaceExistingTracksFromApi,
                cancellationToken);

        public virtual Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode,
            string languageCode,
            bool replaceExistingTracksFromApi,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> SeedEnglishPublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsurePublicationExistsAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstPublicationForLanguageAsync(string languageCode, string? categoryName = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllPublicationsForLanguageAsync(string languageCode, string? categoryName = null,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> EnsureAllSectionsForPublicationAsync(string publicationCode, string languageCode,
            IFetchProgress? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> FetchFirstSectionOnlyAsync(string publicationCode, string firstSectionCode, string languageCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class RecordingLanguageService : IdleLanguageContentServiceBase
    {
        public bool FetchPublicationResult { get; set; } = true;

        public bool FetchSectionResult { get; set; } = true;

        public List<(string Pub, string Sec, string Lang, bool Replace)> FetchSectionCalls { get; } = [];

        public List<(string Pub, string Lang)> FetchPublicationCalls { get; } = [];

        public override Task<bool> FetchPublicationTracksAsync(string publicationCode, string languageCode,
            CancellationToken cancellationToken)
        {
            FetchPublicationCalls.Add((publicationCode, languageCode));
            return Task.FromResult(FetchPublicationResult);
        }

        public override Task<bool> FetchSectionTracksAsync(string publicationCode, string sectionCode,
            string languageCode,
            bool replaceExistingTracksFromApi,
            CancellationToken cancellationToken)
        {
            FetchSectionCalls.Add((publicationCode, sectionCode, languageCode, replaceExistingTracksFromApi));
            return Task.FromResult(FetchSectionResult);
        }
    }

    private sealed class RecordingUrlConstructionService : IUrlConstructionService
    {
        public int ClearCacheCalls { get; private set; }

        public List<(string Pub, string Lang, string? Section, string Track)> ConstructCalls { get; } = [];

        public Queue<List<string>> Results { get; } = new();

        public void ClearLookUpPathCache()
        {
            ClearCacheCalls++;
        }

        public Task<List<string>> ConstructTrackUrlsAsync(int trackId)
        {
            _ = trackId;
            return Task.FromResult(new List<string>());
        }

        public Task<List<string>> ConstructTrackUrlsAsync(string publicationCode, string languageCode,
            string? sectionCode, string trackCode)
        {
            ConstructCalls.Add((publicationCode, languageCode, sectionCode, trackCode));
            var next = Results.Count > 0
                ? Results.Dequeue()
                : new List<string> { $"https://static/{publicationCode}/{trackCode}" };
            return Task.FromResult(next);
        }

        public Task<string?> ConstructTrackLookUpPathAsync(string publicationCode, string? languageCode,
            string? sectionCode,
            string trackCode)
        {
            _ = publicationCode;
            _ = languageCode;
            _ = sectionCode;
            _ = trackCode;
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class RecordingMelodyDiscTracksApiRefresher : IMelodyDiscTracksApiRefresher
    {
        public List<(string Pub, string Disc)> Calls { get; } = [];

        public bool ReplaceSucceeds { get; set; } = true;

        public Task<bool> ReplaceDiscSectionTracksFromApiAsync(string publicationCode, string discSectionCode,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((publicationCode, discSectionCode));
            return Task.FromResult(ReplaceSucceeds);
        }
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_returns_null_when_track_code_missing()
    {
        var sut = new TrackCdnUrlRefresher(new RecordingLanguageService(), new RecordingUrlConstructionService(),
            new RecordingMelodyDiscTracksApiRefresher(), TestLogging.CreateLogger());

        Assert.Null(await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "iam",
            DownloadCode = "iam-1",
            TrackCode = "",
            LanguageCode = string.Empty,
        }));
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_melody_disc_replaces_sections_then_returns_first_constructed_url()
    {
        var language = new RecordingLanguageService();
        var urls = new RecordingUrlConstructionService();
        urls.Results.Enqueue(new List<string> { "https://melody/track" });

        var melody = new RecordingMelodyDiscTracksApiRefresher { ReplaceSucceeds = true };

        var sut = new TrackCdnUrlRefresher(language, urls, melody, TestLogging.CreateLogger());

        var result = await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "iam",
            DownloadCode = "iam-2",
            TrackCode = "3",
            LanguageCode = string.Empty,
        });

        Assert.Equal("https://melody/track", result);

        Assert.Equal(("iam", "iam-2"), Assert.Single(melody.Calls));
        Assert.Equal(1, urls.ClearCacheCalls);
        Assert.Equal(("iam", string.Empty, "iam-2", "3"), Assert.Single(urls.ConstructCalls));
        Assert.Empty(language.FetchPublicationCalls);
        Assert.Empty(language.FetchSectionCalls);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_melody_when_replace_false_returns_null()
    {
        var urls = new RecordingUrlConstructionService();
        var sut = new TrackCdnUrlRefresher(
            new RecordingLanguageService(),
            urls,
            new RecordingMelodyDiscTracksApiRefresher { ReplaceSucceeds = false },
            TestLogging.CreateLogger());

        Assert.Null(await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "iam",
            DownloadCode = "iam-1",
            TrackCode = "1",
            LanguageCode = " ",
        }));

        Assert.Equal(1, urls.ClearCacheCalls);
        Assert.Empty(urls.ConstructCalls);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_non_melody_without_language_returns_null_after_trim()
    {
        var language = new RecordingLanguageService();

        var sut = new TrackCdnUrlRefresher(
            language,
            new RecordingUrlConstructionService(),
            new RecordingMelodyDiscTracksApiRefresher(),
            TestLogging.CreateLogger());

        Assert.Null(await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "osg",
            TrackCode = "10",
            LanguageCode = "\t ",
        }));

        Assert.Empty(language.FetchPublicationCalls);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_uses_fetch_section_when_section_present()
    {
        var language = new RecordingLanguageService();
        var urls = new RecordingUrlConstructionService();
        urls.Results.Enqueue(new List<string> { "https://sec" });

        var sut = new TrackCdnUrlRefresher(language, urls, new RecordingMelodyDiscTracksApiRefresher(),
            TestLogging.CreateLogger());

        var url = await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "nwt",
            SectionCode = " 40 ",
            TrackCode = "01",
            LanguageCode = "E",
        });

        Assert.Equal("https://sec", url);

        Assert.Equal(("nwt", "40", "E", true), Assert.Single(language.FetchSectionCalls));
        Assert.Empty(language.FetchPublicationCalls);

        Assert.Equal(("nwt", "E", " 40 ", "01"), Assert.Single(urls.ConstructCalls));
        Assert.True(urls.ClearCacheCalls >= 1);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_returns_null_when_api_refresh_succeeds_but_urls_empty()
    {
        var language = new RecordingLanguageService();
        var urls = new RecordingUrlConstructionService();
        urls.Results.Enqueue(new List<string>());

        var sut = new TrackCdnUrlRefresher(language, urls, new RecordingMelodyDiscTracksApiRefresher(),
            TestLogging.CreateLogger());

        Assert.Null(await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "osg",
            SectionCode = null,
            TrackCode = "2",
            LanguageCode = "MY",
        }));

        Assert.Equal(("osg", "MY"), Assert.Single(language.FetchPublicationCalls));
        Assert.Empty(language.FetchSectionCalls);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_returns_null_when_fetch_publication_fails()
    {
        var language = new RecordingLanguageService { FetchPublicationResult = false };
        var urls = new RecordingUrlConstructionService();
        var sut = new TrackCdnUrlRefresher(language, urls, new RecordingMelodyDiscTracksApiRefresher(),
            TestLogging.CreateLogger());

        Assert.Null(await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "osg",
            SectionCode = null,
            TrackCode = "2",
            LanguageCode = "MY",
        }));

        Assert.Equal(("osg", "MY"), Assert.Single(language.FetchPublicationCalls));
        Assert.Equal(1, urls.ClearCacheCalls);
        Assert.Empty(urls.ConstructCalls);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_returns_null_when_fetch_section_fails()
    {
        var language = new RecordingLanguageService { FetchSectionResult = false };
        var urls = new RecordingUrlConstructionService();
        var sut = new TrackCdnUrlRefresher(language, urls, new RecordingMelodyDiscTracksApiRefresher(),
            TestLogging.CreateLogger());

        Assert.Null(await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
        {
            PublicationCode = "nwt",
            SectionCode = "40",
            TrackCode = "01",
            LanguageCode = "E",
        }));

        Assert.Equal(("nwt", "40", "E", true), Assert.Single(language.FetchSectionCalls));
        Assert.Equal(1, urls.ClearCacheCalls);
        Assert.Empty(urls.ConstructCalls);
    }

    [Fact]
    public async Task TryRefreshTrackCdnUrlFromApiAsync_flat_publication_returns_first_url_when_present()
    {
        var language = new RecordingLanguageService();
        var urls = new RecordingUrlConstructionService();
        urls.Results.Enqueue(new List<string> { "https://flat" });

        var sut = new TrackCdnUrlRefresher(language, urls, new RecordingMelodyDiscTracksApiRefresher(),
            TestLogging.CreateLogger());

        Assert.Equal(
            "https://flat",
            await sut.TryRefreshTrackCdnUrlFromApiAsync(new TrackMetadata
            {
                PublicationCode = "osg",
                SectionCode = "",
                TrackCode = "5",
                LanguageCode = "MY",
            }));

        Assert.Equal(("osg", "MY"), Assert.Single(language.FetchPublicationCalls));
        Assert.Empty(language.FetchSectionCalls);
        Assert.Equal(("osg", "MY", "", "5"), Assert.Single(urls.ConstructCalls));
    }
}
