#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.BiblePublicationCascadeHandlerHelpers;
using Bible.Alarm.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationCascadeNoLanguageResolverTests
{
    private sealed class ResolverStubMedia(SortedDictionary<string, BiblePublicationSection> sectionsForNoLanguage) : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(sectionsForNoLanguage);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            Task.CompletedTask;

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            Task.CompletedTask;

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
        {
        }

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            Task.FromResult(false);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_flat_publication_loads_ordered_track_and_publication_name()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string publicationCode = "nlp-flat";

        await using (var seed = new MediaDbContext(options))
        {
            var bp = new BiblePublication
            {
                Name = "Flat NL Title",
                PublicationCode = publicationCode,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            bp.Tracks.AddRange(
                new BiblePublicationTrack { TrackCode = "10", Title = "Ten", Publication = bp, BiblePublicationSectionId = null },
                new BiblePublicationTrack { TrackCode = "2", Title = "Two", Publication = bp, BiblePublicationSectionId = null });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var media = new ResolverStubMedia(new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase));
        var factory = new MediaTestScopeFactory(options);

        var result = await BiblePublicationCascadeNoLanguageResolver.GetFirstSectionAndTrackAsync(media, factory, publicationCode);

        Assert.Null(result.sectionCode);
        Assert.Equal("2", result.trackCode);
        Assert.Equal(string.Empty, result.sectionName);
        Assert.Equal("Two", result.trackTitle);
        Assert.Equal("Flat NL Title", result.publicationName);
    }

    [Fact]
    public async Task GetFirstSectionAndTrackAsync_section_publication_matches_first_section_track_from_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var init = new MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        const string publicationCode = "nlp-sec";

        await using (var seed = new MediaDbContext(options))
        {
            var bp = new BiblePublication
            {
                Name = "Section NL Title",
                PublicationCode = publicationCode,
                LanguageId = null,
                IsVideo = false,
                IsMusic = false,
            };
            var sec = new BiblePublicationSection
            {
                SectionCode = "40",
                Name = "Matthew",
                BiblePublication = bp,
            };
            bp.Sections.Add(sec);
            bp.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "1",
                Title = "Chapter One",
                Publication = bp,
                Section = sec,
            });
            seed.BiblePublications.Add(bp);
            await seed.SaveChangesAsync();
        }

        var sectionsOut = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
        {
            ["40"] = new BiblePublicationSection { SectionCode = "40", Name = "Matthew" },
        };
        var media = new ResolverStubMedia(sectionsOut);
        var factory = new MediaTestScopeFactory(options);

        var result = await BiblePublicationCascadeNoLanguageResolver.GetFirstSectionAndTrackAsync(media, factory, publicationCode);

        Assert.Equal("40", result.sectionCode);
        Assert.Equal("1", result.trackCode);
        Assert.Equal("Matthew", result.sectionName);
        Assert.Equal("Chapter One", result.trackTitle);
        Assert.Equal("Section NL Title", result.publicationName);
    }
}
