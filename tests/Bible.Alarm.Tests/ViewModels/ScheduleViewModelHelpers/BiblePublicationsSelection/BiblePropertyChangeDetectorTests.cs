#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.BiblePublicationsSelection;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.Tests;

public sealed class BiblePropertyChangeDetectorTests
{
    private sealed class MutableAppState : IState<ApplicationState>
    {
#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067

        public MutableAppState(ApplicationState initial) => Value = initial;

        public ApplicationState Value { get; set; }
    }

    private sealed class StubMediaService : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>());

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

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

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) => categoryCode;
    }

    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Detector tests must not open EF-backed scopes.");
    }

    private static ScheduleStateItem BaseSchedule() =>
        new()
        {
            Id = 1,
            Name = "S",
            BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight,
            BiblePublicationCategoryId = 5,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationLanguageCode = "E",
            BiblePublicationLanguageName = "English",
            BiblePublicationCode = "nwt",
            BiblePublicationSectionCode = "1",
            BiblePublicationTrackCode = "1",
            BiblePublicationName = "Holy Scriptures",
        };

    private static BiblePublicationDisplayTextProvider CreateProvider(MutableAppState flux) =>
        new(
            flux,
            TestLogging.CreateLogger(),
            new StubMediaService(),
            new StubCategoryNameService(),
            new UnusedScopeFactory());

    private static BiblePublicationPropertyChangeDetector CreateSut(MutableAppState flux) =>
        new(CreateProvider(flux));

    [Fact]
    public void DetectPropertyChanges_stable_across_repeat_when_snapshot_unchanged()
    {
        var schedule = BaseSchedule();
        var flux = new MutableAppState(new ApplicationState([], schedule));
        var sut = CreateSut(flux);

        sut.Initialize(
            schedule.BiblePublicationCategoryId,
            schedule.BiblePublicationCategoryName,
            schedule.BiblePublicationLanguageCode,
            schedule.BiblePublicationCode,
            schedule.BiblePublicationSectionCode,
            schedule.BiblePublicationTrackCode);

        Assert.False(sut.DetectPropertyChanges(schedule).HasChanges);
        Assert.False(sut.DetectPropertyChanges(schedule).HasChanges);
    }

    [Fact]
    public void DetectPropertyChanges_notifies_publication_when_publication_code_changes()
    {
        var initial = BaseSchedule();
        var flux = new MutableAppState(new ApplicationState([], initial));
        var sut = CreateSut(flux);

        sut.Initialize(
            initial.BiblePublicationCategoryId,
            initial.BiblePublicationCategoryName,
            initial.BiblePublicationLanguageCode,
            initial.BiblePublicationCode,
            initial.BiblePublicationSectionCode,
            initial.BiblePublicationTrackCode);

        _ = sut.DetectPropertyChanges(initial);

        var updated = BaseSchedule();
        updated.BiblePublicationCode = AppConstants.Media.BiblePublicationCategoryDramas;
        updated.BiblePublicationName = "Drama series";
        flux.Value = new ApplicationState([], updated);

        var delta = sut.DetectPropertyChanges(updated);

        Assert.True(delta.NotifyPublication);
        Assert.True(delta.CascadeChangeOccurred);
        Assert.Equal(AppConstants.Media.BiblePublicationCategoryDramas, delta.CurrentPublicationCode);
    }

    [Fact]
    public void DetectPropertyChanges_flags_display_only_change_when_language_name_updates_without_codes()
    {
        var schedule = BaseSchedule();
        var flux = new MutableAppState(new ApplicationState([], schedule));
        var sut = CreateSut(flux);

        sut.Initialize(
            schedule.BiblePublicationCategoryId,
            schedule.BiblePublicationCategoryName,
            schedule.BiblePublicationLanguageCode,
            schedule.BiblePublicationCode,
            schedule.BiblePublicationSectionCode,
            schedule.BiblePublicationTrackCode);

        _ = sut.DetectPropertyChanges(schedule);

        schedule = BaseSchedule();
        schedule.BiblePublicationLanguageName = "английский";
        flux.Value = new ApplicationState([], schedule);

        var delta = sut.DetectPropertyChanges(schedule);

        Assert.True(delta.DisplayTextOnlyChanged);
        Assert.False(delta.CascadeChangeOccurred);
        Assert.True(delta.LanguageDisplayChanged);
    }
}
