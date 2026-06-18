#nullable enable

using System.Net;
using System.Net.Http;
using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class BibleLanguageDiscoveryCatalogerTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    [Fact]
    public async Task DiscoverLanguagesForAllBooks_merges_unique_languages_and_persists_sections()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var bookLanguagesJson = $$"""
            {
              "{{lng}}": {
                "E": { "{{nm}}": "English", "{{AppConstants.Media.LanguageIndexJson.Direction}}": "ltr" },
                "S": { "{{nm}}": "Spanish", "{{AppConstants.Media.LanguageIndexJson.Direction}}": "ltr" }
              }
            }
            """;
        var signLanguagesJson = $$"""
            {
              "{{lng}}": [
                {
                  "{{AppConstants.Media.LanguageIndexJson.LangCode}}": "E",
                  "{{AppConstants.Media.LanguageIndexJson.IsSignLanguage}}": false
                },
                {
                  "{{AppConstants.Media.LanguageIndexJson.LangCode}}": "S",
                  "{{AppConstants.Media.LanguageIndexJson.IsSignLanguage}}": false
                }
              ]
            }
            """;

        var downloadUtility = new StubDownloadUtility(SilentLogger, url =>
        {
            if (url.Contains(AppConstants.ApiEndpoints.JwOrgLanguagesListUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(signLanguagesJson);
            }

            if (url.Contains($"{AppConstants.Media.GetPubQueryParamName.BookNum}=1", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(bookLanguagesJson);
            }

            return Task.FromResult("{}");
        });

        var recordingPersister = new RecordingDataPersister();
        var sut = new BibleLanguageDiscoveryCataloger(SilentLogger, downloadUtility, recordingPersister);

        var discovered = await sut.DiscoverLanguagesForAllBooks("nwt", "New World Translation", isTestRun: false);

        Assert.NotNull(discovered);
        Assert.Equal(2, discovered!.Count);
        Assert.True(discovered.ContainsKey("E"));
        Assert.True(discovered.ContainsKey("S"));
        Assert.NotEmpty(recordingPersister.SectionLanguages);
    }

    [Fact]
    public async Task DiscoverLanguages_persists_language_discovery_and_publication_languages()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var bookLanguagesJson = $$"""
            {
              "{{lng}}": {
                "E": { "{{nm}}": "English" }
              }
            }
            """;
        var signLanguagesJson = $$"""
            {
              "{{lng}}": [
                {
                  "{{AppConstants.Media.LanguageIndexJson.LangCode}}": "E",
                  "{{AppConstants.Media.LanguageIndexJson.IsSignLanguage}}": false
                }
              ]
            }
            """;

        var downloadUtility = new StubDownloadUtility(SilentLogger, url =>
        {
            if (url.Contains(AppConstants.ApiEndpoints.JwOrgLanguagesListUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(signLanguagesJson);
            }

            return Task.FromResult(bookLanguagesJson);
        });

        var recordingPersister = new RecordingDataPersister();
        var sut = new BibleLanguageDiscoveryCataloger(SilentLogger, downloadUtility, recordingPersister);

        await sut.DiscoverLanguages(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nwt"] = "New World Translation" },
            isTestRun: false);

        Assert.Single(recordingPersister.LanguageDiscoveries);
        Assert.Single(recordingPersister.PublicationLanguages);
    }

    [Fact]
    public async Task DiscoverLanguagesForAllBooks_returns_null_when_every_book_is_empty()
    {
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ => Task.FromResult("{}"));
        var sut = new BibleLanguageDiscoveryCataloger(SilentLogger, downloadUtility, dataPersister: null);

        var discovered = await sut.DiscoverLanguagesForAllBooks("nwt", "New World Translation", isTestRun: false);

        Assert.Null(discovered);
    }

    [Fact]
    public async Task DiscoverLanguagesForAllBooks_skips_books_that_return_http_errors()
    {
        var lng = AppConstants.Media.LanguageIndexJson.Languages;
        var nm = AppConstants.Media.PubMediaJson.Name;
        var bookLanguagesJson = $$"""
            {
              "{{lng}}": {
                "E": { "{{nm}}": "English" }
              }
            }
            """;
        var signLanguagesJson = $$"""
            {
              "{{lng}}": [
                {
                  "{{AppConstants.Media.LanguageIndexJson.LangCode}}": "E",
                  "{{AppConstants.Media.LanguageIndexJson.IsSignLanguage}}": false
                }
              ]
            }
            """;

        var downloadUtility = new StubDownloadUtility(SilentLogger, url =>
        {
            if (url.Contains(AppConstants.ApiEndpoints.JwOrgLanguagesListUrl, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(signLanguagesJson);
            }

            if (url.Contains($"{AppConstants.Media.GetPubQueryParamName.BookNum}=2", StringComparison.OrdinalIgnoreCase))
            {
                throw new HttpRequestException("Response status code does not indicate success: 404 (Not Found).");
            }

            if (url.Contains($"{AppConstants.Media.GetPubQueryParamName.BookNum}=1", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(bookLanguagesJson);
            }

            return Task.FromResult("{}");
        });

        var sut = new BibleLanguageDiscoveryCataloger(SilentLogger, downloadUtility, dataPersister: null);

        var discovered = await sut.DiscoverLanguagesForAllBooks("nwt", "New World Translation", isTestRun: false);

        Assert.NotNull(discovered);
        Assert.Single(discovered!);
        Assert.True(discovered!.ContainsKey("E"));
    }

    private sealed class RecordingDataPersister : IDataPersister
    {
        public List<(string LanguageCode, string PublicationCode, Dictionary<string, string> Mapping)> LanguageDiscoveries { get; } = [];

        public List<(string PublicationCode, Dictionary<string, LanguageInfo> Languages)> PublicationLanguages { get; } = [];

        public List<(string PublicationCode, string SectionCode, Dictionary<string, LanguageInfo> Languages)> SectionLanguages { get; } = [];

        public Task SaveBiblePublicationSections(
            string languageCode,
            string publicationCode,
            string publicationName,
            Dictionary<int, Bible.Alarm.Cataloger.Models.BiblePublications.BiblePublicationSection> sections,
            Dictionary<int, Dictionary<int, Bible.Alarm.Cataloger.Models.BiblePublications.BiblePublicationTrack>> sectionCodeTrackMap) =>
            Task.CompletedTask;

        public Task SaveMediatorPublication(
            string languageCode,
            string publicationCode,
            string publicationName,
            Dictionary<string, List<MediatorTrack>> tracksBySection,
            Dictionary<string, string> sectionNames) =>
            Task.CompletedTask;

        public Task SaveMusicTracks(
            string publicationCode,
            string? languageCode,
            string publicationName,
            List<MusicTrack> tracks) =>
            Task.CompletedTask;

        public Task SaveMelodyMusicTracks(
            string publicationCode,
            Dictionary<string, List<MusicTrack>> discTracksMap,
            Dictionary<string, string> discNamesMap) =>
            Task.CompletedTask;

        public Task SaveVideoEpisodes(
            string languageCode,
            string publicationCode,
            string publicationName,
            List<VideoEpisode> episodes) =>
            Task.CompletedTask;

        public Task SaveLanguageDiscovery(
            string languageCode,
            string publicationCode,
            Dictionary<string, string> languageCodeToNameMapping)
        {
            LanguageDiscoveries.Add((languageCode, publicationCode, new Dictionary<string, string>(languageCodeToNameMapping, StringComparer.OrdinalIgnoreCase)));
            return Task.CompletedTask;
        }

        public Task SavePublicationLanguages(
            string publicationCode,
            Dictionary<string, LanguageInfo> discoveredLanguages)
        {
            PublicationLanguages.Add((publicationCode, new Dictionary<string, LanguageInfo>(discoveredLanguages, StringComparer.OrdinalIgnoreCase)));
            return Task.CompletedTask;
        }

        public Task SaveSectionLanguages(
            string publicationCode,
            string sectionCode,
            Dictionary<string, LanguageInfo> discoveredLanguages)
        {
            SectionLanguages.Add((publicationCode, sectionCode, new Dictionary<string, LanguageInfo>(discoveredLanguages, StringComparer.OrdinalIgnoreCase)));
            return Task.CompletedTask;
        }
    }
}
