#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Fluxor;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionItemSelectorTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Not used when biblePublicationService is null for non-sectioned flow.");
    }

    private class StubMedia : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public virtual Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public virtual Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public virtual Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode)
        {
            if (string.Equals(publicationCode, "iam", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
                {
                    ["iam-1"] = new BiblePublicationSection { SectionCode = "iam-1", Name = "Disc 1" },
                });
            }

            return Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());
        }

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>());

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
    public async Task GetSectionAndTrackForPublicationAsync_NoSectionsAndNoBibleService_ReturnsEmptyTrack()
    {
        var publicationEntity = new BiblePublication
        {
            PublicationCode = "nwtsty",
            Name = "Study Bible",
            LanguageId = 1,
            Language = new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
        };

        var publication = new PublicationListViewItemModel(publicationEntity);
        var language = new LanguageListViewItemModel(new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight }, displayName: "English");

        var sut = new BiblePublicationSelectionItemSelector(
            new StubMedia(),
            new FakeApplicationState(new ApplicationState([], currentSchedule: null)),
            biblePublicationService: null,
            biblePublicationSectionService: null,
            languageContentService: null,
            scopeFactory: new UnusedScopeFactory());

        var result = await sut.GetSectionAndTrackForPublicationAsync(publication, language, progress: null);

        Assert.Null(result.SectionCode);
        Assert.Equal(string.Empty, result.TrackCode);
        Assert.Equal(string.Empty, result.SectionName);
        Assert.Equal(string.Empty, result.TrackTitle);
    }

    [Fact]
    public async Task GetPublicationSectionAndTrackForLanguageAsync_returns_empty_when_no_publications_in_index()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Bible.Alarm.Shared.Database.MediaDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var init = new Bible.Alarm.Shared.Database.MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        var schedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
        };
        var sut = new BiblePublicationSelectionItemSelector(
            new StubMedia(),
            new FakeApplicationState(new ApplicationState([], schedule)),
            scopeFactory: new MediaTestScopeFactory(options));

        var language = new LanguageListViewItemModel(
            new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            displayName: "English");

        var result = await sut.GetPublicationSectionAndTrackForLanguageAsync(language);

        Assert.Null(result.PublicationCode);
        Assert.Equal(string.Empty, result.TrackCode);
    }

    [Fact]
    public async Task GetSectionAndTrackForPublicationAsync_returns_first_track_for_no_language_sectioned_publication()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Bible.Alarm.Shared.Database.MediaDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var init = new Bible.Alarm.Shared.Database.MediaDbContext(options))
        {
            await init.Database.EnsureCreatedAsync();
        }

        await using (var seed = new Bible.Alarm.Shared.Database.MediaDbContext(options))
        {
            var publication = new BiblePublication
            {
                Name = "Kingdom Melodies",
                PublicationCode = "iam",
                LanguageId = null,
                IsVideo = false,
                IsMusic = true,
            };
            var section = new BiblePublicationSection
            {
                Name = "Disc 1",
                SectionCode = "iam-1",
                BiblePublication = publication,
            };
            publication.Sections.Add(section);
            section.Tracks.Add(new BiblePublicationTrack
            {
                TrackCode = "190",
                Title = "Melody 190",
                Section = section,
                Publication = publication,
            });
            seed.BiblePublications.Add(publication);
            await seed.SaveChangesAsync();
        }

        var publicationVm = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "iam",
            Name = "Kingdom Melodies",
            LanguageId = null,
            Id = 5,
        });
        var language = new LanguageListViewItemModel(
            new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            displayName: "English");

        var sut = new BiblePublicationSelectionItemSelector(
            new StubMedia(),
            new FakeApplicationState(new ApplicationState([], currentSchedule: null)),
            scopeFactory: new MediaTestScopeFactory(options));

        var result = await sut.GetSectionAndTrackForPublicationAsync(publicationVm, language, progress: null);

        Assert.Equal("iam-1", result.SectionCode);
        Assert.Equal("190", result.TrackCode);
        Assert.Equal("Disc 1", result.SectionName);
        Assert.Equal("Melody 190", result.TrackTitle);
    }

}
