#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Maui.Controls;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionCommandHandlerTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class FakeState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class CountingNavigation : INavigationService
    {
        public int PopModalCalls { get; private set; }

        public void Dispose()
        {
        }

        public Task NavigateToHomeAsync(bool animated = true) => Task.CompletedTask;
        public Task NavigateToScheduleAsync() => Task.CompletedTask;
        public Task NavigateToScheduleAsync(int scheduleId, bool isEnabled) => Task.CompletedTask;
        public Task OpenSongPublicationSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBibleSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenMusicSectionSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenBiblePublicationTrackSelectionModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNumberOfTracksModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenLanguageModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenCategoryModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenPlaybackModalAsync(bool animated = false) => Task.CompletedTask;
        public Task OpenBatteryOptimizationModalAsync(object bindingContext) => Task.CompletedTask;
        public Task OpenNotificationPermissionModalAsync(object bindingContext) => Task.CompletedTask;

        public Task PopModalAsync()
        {
            PopModalCalls++;
            return Task.CompletedTask;
        }

        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;

        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Views.Home? GetCurrentHomePage() => null;
        public Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;

        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class StubMediaService(
        bool isMelody,
        SortedDictionary<int, MusicTrack> tracks) : IMediaService
    {
        public void Dispose()
        {
        }

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            Task.FromResult<BiblePublicationSection?>(null);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            Task.FromResult<BiblePublicationTrack?>(null);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(new SortedDictionary<int, MusicTrack>());

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            Task.FromResult(tracks);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(tracks);

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
            Task.FromResult(isMelody);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(0);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            Task.FromResult(0);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            Task.FromResult(0);
    }

    [Fact]
    public async Task HandleSectionSelectedAsync_returns_when_schedule_missing_publication()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new MusicSectionSelectionCommandHandler(
            TestLogging.CreateLogger(),
            new StubMediaService(false, new SortedDictionary<int, MusicTrack>()),
            new FakeState(new ApplicationState()),
            dispatcher,
            new CountingNavigation());

        var section = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1", Name = "A" });

        await sut.HandleSectionSelectedAsync(section, () => false);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleSectionSelectedAsync_returns_when_tracks_empty()
    {
        var dispatcher = new RecordingDispatcher();
        var sut = new MusicSectionSelectionCommandHandler(
            TestLogging.CreateLogger(),
            new StubMediaService(false, new SortedDictionary<int, MusicTrack>()),
            new FakeState(new ApplicationState
            {
                CurrentSchedule = new ScheduleStateItem
                {
                    MusicPublicationCode = "osg",
                    MusicLanguageCode = "E"
                }
            }),
            dispatcher,
            new CountingNavigation());

        var section = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "1", Name = "A" });

        await sut.HandleSectionSelectedAsync(section, () => false);

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleSectionSelectedAsync_dispatches_first_vocal_track_and_pops()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "10", Title = "First" },
            [1] = new MusicTrack { TrackCode = "11", Title = "Second" }
        };

        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var sut = new MusicSectionSelectionCommandHandler(
            TestLogging.CreateLogger(),
            new StubMediaService(false, tracks),
            new FakeState(new ApplicationState
            {
                CurrentSchedule = new ScheduleStateItem
                {
                    MusicPublicationCode = "osg",
                    MusicLanguageCode = "E",
                    MusicPublicationName = "Songs",
                    MusicRepeat = true
                }
            }),
            dispatcher,
            navigation);

        var section = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "sec-x", Name = "Section label" });

        await sut.HandleSectionSelectedAsync(section, () => false);

        var music = Assert.IsType<MusicSectionSelectedAction>(Assert.Single(dispatcher.Dispatched)).CurrentMusic;
        Assert.Equal("10", music.TrackCode);
        Assert.Equal("sec-x", music.SectionCode);
        Assert.Equal("Section label", music.SectionName);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task HandleSectionSelectedAsync_uses_melody_by_section_path()
    {
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "m1", Title = "Melody" }
        };

        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var sut = new MusicSectionSelectionCommandHandler(
            TestLogging.CreateLogger(),
            new StubMediaService(true, tracks),
            new FakeState(new ApplicationState
            {
                CurrentSchedule = new ScheduleStateItem
                {
                    MusicPublicationCode = "iam",
                    MusicLanguageCode = null,
                    MusicPublicationName = "IAM"
                }
            }),
            dispatcher,
            navigation);

        var section = new BiblePublicationSectionListViewItemModel(new BiblePublicationSection { SectionCode = "iam-2", Name = "Disc 2" });

        await sut.HandleSectionSelectedAsync(section, () => false);

        var music = Assert.IsType<MusicSectionSelectedAction>(Assert.Single(dispatcher.Dispatched)).CurrentMusic;
        Assert.Equal("m1", music.TrackCode);
        Assert.Equal("iam-2", music.SectionCode);
        Assert.Equal(1, navigation.PopModalCalls);
    }
}
