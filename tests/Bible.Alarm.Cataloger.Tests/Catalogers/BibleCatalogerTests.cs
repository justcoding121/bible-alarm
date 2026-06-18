#nullable enable

using System.Collections.Concurrent;
using Bible.Alarm.Cataloger.Catalogers;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Utility;
using Bible.Alarm.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Cataloger.Tests;

public sealed class BibleCatalogerTests
{
    private static readonly ILogger SilentLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    private sealed class NullScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new NotSupportedException("CatalogBibleLinks tests do not open database scopes.");
    }

    [Fact]
    public async Task CatalogBibleLinks_maps_english_from_db_seeder_publication_languages()
    {
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ =>
            Task.FromResult("{}"));
        var dbSeeder = new DbSeeder(SilentLogger, new NullScopeFactory(), downloadUtility);
        await dbSeeder.SavePublicationLanguages("nwt", new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [AppConstants.Media.DefaultLanguageCode] = new LanguageInfo("English", AppConstants.Media.TextDirectionLeftToRight),
            ["S"] = new LanguageInfo("Spanish", AppConstants.Media.TextDirectionLeftToRight),
        });

        var sut = new BibleCataloger(SilentLogger, downloadUtility, dbSeeder);
        var languageCodeToInfoMappings = new ConcurrentDictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        await sut.CatalogBibleLinks(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nwt"] = "New World Translation" },
            languageCodeToInfoMappings,
            languageCodeToEditionsMapping);

        Assert.True(languageCodeToInfoMappings.TryGetValue(AppConstants.Media.DefaultLanguageCode, out var english));
        Assert.Equal("English", english!.Name);
        Assert.True(languageCodeToEditionsMapping.TryGetValue(AppConstants.Media.DefaultLanguageCode, out var editions));
        Assert.Contains("nwt", editions!);
    }

    [Fact]
    public async Task CatalogBibleLinks_skips_publication_when_english_missing_from_discovered_languages()
    {
        var downloadUtility = new StubDownloadUtility(SilentLogger, _ =>
            Task.FromResult("{}"));
        var dbSeeder = new DbSeeder(SilentLogger, new NullScopeFactory(), downloadUtility);
        await dbSeeder.SavePublicationLanguages("nwt", new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["S"] = new LanguageInfo("Spanish", AppConstants.Media.TextDirectionLeftToRight),
        });

        var sut = new BibleCataloger(SilentLogger, downloadUtility, dbSeeder);
        var languageCodeToInfoMappings = new ConcurrentDictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        await sut.CatalogBibleLinks(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nwt"] = "New World Translation" },
            languageCodeToInfoMappings,
            languageCodeToEditionsMapping);

        Assert.Empty(languageCodeToInfoMappings);
        Assert.Empty(languageCodeToEditionsMapping);
    }

    [Fact]
    public async Task CatalogBibleLinks_runs_discovery_when_db_seeder_has_no_publication_languages()
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
                { "{{AppConstants.Media.LanguageIndexJson.LangCode}}": "E", "{{AppConstants.Media.LanguageIndexJson.IsSignLanguage}}": false }
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
        var sut = new BibleCataloger(SilentLogger, downloadUtility, recordingPersister);
        var languageCodeToInfoMappings = new ConcurrentDictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
        var languageCodeToEditionsMapping = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        await sut.CatalogBibleLinks(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["nwt"] = "New World Translation" },
            languageCodeToInfoMappings,
            languageCodeToEditionsMapping);

        Assert.True(languageCodeToInfoMappings.ContainsKey(AppConstants.Media.DefaultLanguageCode));
        Assert.NotEmpty(recordingPersister.PublicationLanguages);
    }

    private sealed class RecordingDataPersister : IDataPersister
    {
        public List<(string PublicationCode, Dictionary<string, LanguageInfo> Languages)> PublicationLanguages { get; } = [];

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
            Dictionary<string, string> languageCodeToNameMapping) =>
            Task.CompletedTask;

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
            Dictionary<string, LanguageInfo> discoveredLanguages) =>
            Task.CompletedTask;
    }
}
