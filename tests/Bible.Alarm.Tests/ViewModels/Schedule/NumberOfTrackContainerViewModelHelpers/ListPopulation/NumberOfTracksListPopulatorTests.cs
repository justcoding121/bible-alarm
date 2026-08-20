#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule.NumberOfTrackContainerViewModelHelpers.ListPopulation;

namespace Bible.Alarm.Tests;

public sealed class NumberOfTracksListPopulatorTests
{
    private static BiblePublication PublicationWithTrackCount(int count)
    {
        var tracks = new List<BiblePublicationTrack>();
        for (var i = 0; i < count; i++)
        {
            tracks.Add(new BiblePublicationTrack { TrackCode = $"t{i}" });
        }

        return new BiblePublication { Tracks = tracks };
    }

    private sealed class BiblePublicationServiceStub : IBiblePublicationService
    {
        public BiblePublication? PublicationToReturn { get; init; }

        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PublicationToReturn);

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<string>> GetAvailablePublicationCodesAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetFirstPublicationCodeByOrderAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(
            string languageCode,
            string categoryCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }

    [Fact]
    public async Task PopulateAsync_coerces_non_positive_schedule_track_count_to_default_before_selection()
    {
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), biblePublicationService: null);
        var schedule = new ScheduleStateItem { NumberOfTracksToPlay = 0 };

        var result = await sut.PopulateAsync(schedule, preservedSelection: null, currentNumberOfTracks: null);

        Assert.NotNull(result.SelectedItem);
        Assert.Equal(1, result.SelectedItem.Value);
    }

    [Fact]
    public async Task PopulateAsync_uses_preserved_selection_over_computed_value()
    {
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), biblePublicationService: null);
        var schedule = new ScheduleStateItem { NumberOfTracksToPlay = 2 };

        var result = await sut.PopulateAsync(schedule, preservedSelection: 5, currentNumberOfTracks: 2);

        Assert.Equal(5, result.SelectedItem?.Value);
        Assert.Single(result.List, i => i.IsSelected);
    }

    [Fact]
    public async Task PopulateAsync_dramas_caps_max_tracks_by_publication_episode_count()
    {
        var stub = new BiblePublicationServiceStub { PublicationToReturn = PublicationWithTrackCount(5) };
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), stub);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryDramas,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "dram-code"
        };

        var result = await sut.PopulateAsync(schedule, null, null);

        Assert.Equal(5, result.List.Count);
        Assert.Equal(5, result.List.Max(i => i.Value));
    }

    [Fact]
    public async Task PopulateAsync_dramas_large_episode_count_caps_at_twenty_one()
    {
        var stub = new BiblePublicationServiceStub { PublicationToReturn = PublicationWithTrackCount(40) };
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), stub);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryDramas,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "dram-code"
        };

        var result = await sut.PopulateAsync(schedule, null, null);

        Assert.Equal(21, result.List.Count);
    }

    [Fact]
    public async Task PopulateAsync_dramas_without_bible_service_uses_default_cap()
    {
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), biblePublicationService: null);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryDramas,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "dram-code",
        };

        var result = await sut.PopulateAsync(schedule, null, null);

        Assert.Equal(21, result.List.Count);
    }

    [Fact]
    public async Task PopulateAsync_dramas_without_language_or_code_uses_default_cap()
    {
        var stub = new BiblePublicationServiceStub { PublicationToReturn = PublicationWithTrackCount(5) };
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), stub);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryDramas,
            BiblePublicationLanguageCode = "",
            BiblePublicationCode = "",
        };

        var result = await sut.PopulateAsync(schedule, null, null);

        Assert.Equal(21, result.List.Count);
    }

    [Fact]
    public async Task PopulateAsync_dramas_when_publication_has_no_tracks_uses_default_cap()
    {
        var stub = new BiblePublicationServiceStub
        {
            PublicationToReturn = new BiblePublication { Tracks = [] },
        };
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), stub);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryDramas,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "dram-code",
        };

        var result = await sut.PopulateAsync(schedule, null, null);

        Assert.Equal(21, result.List.Count);
    }

    [Fact]
    public async Task PopulateAsync_dramas_when_lookup_throws_uses_default_cap()
    {
        var stub = new ThrowingBiblePublicationService();
        var sut = new NumberOfTracksListPopulator(TestLogging.CreateLogger(), stub);
        var schedule = new ScheduleStateItem
        {
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryDramas,
            BiblePublicationLanguageCode = "E",
            BiblePublicationCode = "dram-code",
        };

        var result = await sut.PopulateAsync(schedule, null, null);

        Assert.Equal(21, result.List.Count);
    }

    private sealed class ThrowingBiblePublicationService : IBiblePublicationService
    {
        public void Dispose()
        {
        }

        public Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("db");

        public Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<string>> GetAvailablePublicationCodesAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetFirstPublicationCodeByOrderAsync(
            string languageCode,
            string? categoryName = null,
            bool filterIsMusicWhenMusicCategory = false,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> IsNoLanguagePublicationAsync(string publicationCode, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<(string? CategoryCode, bool IsMusic)?> GetPublicationCategoryInfoAsync(
            string languageCode,
            string publicationCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<List<string>> GetPublicationCodesInCategoryOrderAsync(
            string languageCode,
            string categoryCode,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public void InvalidatePublicationCaches(string languageCode, string publicationCode)
        {
        }
    }
}
