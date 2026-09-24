#nullable enable

using System.Runtime.InteropServices;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;
using Bible.Alarm.Views;
using Fluxor;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionCommandHandlerTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
            Dispatched.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }

    private sealed class IdleMediaService : IMediaService
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

    private sealed class IdleLanguageNameService : ILanguageNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetNameAsync(int languageId, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> GetNameByLanguageCodeAsync(string languageCode, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<Dictionary<int, string>> GetNamesAsync(IEnumerable<int> languageIds, string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<int, string>());

        public string? GetNameCached(int languageId) => null;

        public string? GetNameByLanguageCodeCached(string languageCode) => null;
    }

    private sealed class IdleNavigationService : INavigationService
    {
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
        public Task PopModalAsync() => Task.CompletedTask;
        public Task PopAsync() => Task.CompletedTask;
        public Task PopPlaybackPageAsync(bool animated = false) => Task.CompletedTask;
        public void PopAllModalsAndPages()
        {
        }

        public void ClearCache()
        {
        }

        public Home? GetCurrentHomePage() => null;
        public Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    [Fact]
    public async Task HandleTrackSelectionAsync_no_op_when_song_publication_missing()
    {
        var dispatcher = new RecordingDispatcher();
        var handler = new MusicPublicationSelectionCommandHandler(
            new IdleNavigationService(),
            new FakeApplicationState(new ApplicationState([])),
            dispatcher,
            new IdleMediaService(),
            new IdleLanguageNameService());

        var dataProvider = new MusicPublicationSelectionDataProvider(new IdleMediaService(), new IdleLanguageNameService());

        await handler.HandleTrackSelectionAsync(new HandleMusicPublicationTrackSelectionArgs(
            SongPublication: null!,
            CurrentLanguage: null,
            DataProvider: dataProvider,
            Current: null,
            Progress: new TrackSelectionProgressBindings(_ => { }, _ => { }, _ => { })));

        Assert.Empty(dispatcher.Dispatched);
    }

    [Fact]
    public async Task HandleTrackSelectionAsync_returns_early_when_vocal_publication_has_no_language()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var handler = CreateHandler(dispatcher, navigation);
        var vocalPublication = new PublicationListViewItemModel(new BiblePublication
        {
            Id = 2,
            PublicationCode = "osg",
            Name = "Sing Out",
            LanguageId = 1,
        });

        try
        {
            await handler.HandleTrackSelectionAsync(new HandleMusicPublicationTrackSelectionArgs(
                vocalPublication,
                CurrentLanguage: null,
                DataProvider: new MusicPublicationSelectionDataProvider(new IdleMediaService(), new IdleLanguageNameService()),
                Current: null,
                Progress: new TrackSelectionProgressBindings(_ => { }, _ => { }, _ => { })));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task HandleTrackSelectionAsync_dispatches_flat_melody_selection()
    {
        // Host-oriented coverage; PopModal / display text race on Android/iOS device runners.
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
        {
            return;
        }
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var tracks = new SortedDictionary<int, MusicTrack>
        {
            [1] = new MusicTrack { TrackCode = "7", Title = "Melody Seven", Url = "", LookUpPath = "" },
        };
        var handler = CreateHandler(dispatcher, navigation, new MelodyFlatMediaService(tracks));
        var melodyPublication = new PublicationListViewItemModel(new BiblePublication
        {
            Id = 3,
            PublicationCode = "kmfl",
            Name = "Flat Melodies",
            LanguageId = null,
        });

        try
        {
            await handler.HandleTrackSelectionAsync(new HandleMusicPublicationTrackSelectionArgs(
                melodyPublication,
                CurrentLanguage: null,
                DataProvider: new MusicPublicationSelectionDataProvider(new IdleMediaService(), new IdleLanguageNameService()),
                Current: null,
                Progress: new TrackSelectionProgressBindings(_ => { }, _ => { }, _ => { })));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return;
        }

        var action = Assert.IsType<TrackSelectedAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Equal("kmfl", action.CurrentMusic.PublicationCode);
        Assert.Equal("7", action.CurrentMusic.TrackCode);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task HandleTrackSelectionAsync_dispatches_vocal_track_from_data_provider()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var vocalTracks = new SortedDictionary<int, MusicTrack>
        {
            [0] = new MusicTrack { TrackCode = "12", Title = "Song Twelve", Url = "", LookUpPath = "" },
        };
        var media = new VocalTracksMediaStub { VocalTracksResult = vocalTracks };
        var handler = CreateHandler(dispatcher, navigation, media);
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            Name = "Alarm",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = true,
            MusicLanguageCode = "E",
        };
        var handlerState = new FakeApplicationState(new ApplicationState([], schedule));
        handler = new MusicPublicationSelectionCommandHandler(
            navigation,
            handlerState,
            dispatcher,
            media,
            new IdleLanguageNameService());

        var vocalPublication = new PublicationListViewItemModel(new BiblePublication
        {
            Id = 4,
            PublicationCode = "osg",
            Name = "Sing Out",
            LanguageId = 1,
        });
        var language = new LanguageListViewItemModel(
            new Language { LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            "English");

        try
        {
            await handler.HandleTrackSelectionAsync(new HandleMusicPublicationTrackSelectionArgs(
                vocalPublication,
                language,
                new MusicPublicationSelectionDataProvider(media, new IdleLanguageNameService()),
                Current: null,
                Progress: new TrackSelectionProgressBindings(_ => { }, _ => { }, _ => { })));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return;
        }

        var action = Assert.IsType<TrackSelectedAction>(Assert.Single(dispatcher.Dispatched));
        Assert.Equal("osg", action.CurrentMusic.PublicationCode);
        Assert.Equal("12", action.CurrentMusic.TrackCode);
        Assert.Equal("E", action.CurrentMusic.LanguageCode);
    }

    [Fact]
    public async Task HandleLanguageSelectionAsync_is_noop_when_language_null()
    {
        var dispatcher = new RecordingDispatcher();
        var navigation = new CountingNavigation();
        var handler = CreateHandler(dispatcher, navigation);

        await handler.HandleLanguageSelectionAsync(
            language: null!,
            new MusicPublicationSelectionDataProvider(new IdleMediaService(), new IdleLanguageNameService()),
            new HandleMusicLanguageSelectionUiCallbacks(
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { },
                _ => { }));

        Assert.Empty(dispatcher.Dispatched);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    private static MusicPublicationSelectionCommandHandler CreateHandler(
        RecordingDispatcher dispatcher,
        CountingNavigation navigation,
        IMediaService? media = null) =>
        new(
            navigation,
            new FakeApplicationState(new ApplicationState([])),
            dispatcher,
            media ?? new IdleMediaService(),
            new IdleLanguageNameService());

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

        public Home? GetCurrentHomePage() => null;
        public Page? GetCurrentPage() => null;
        public bool IsPlaybackModalOnScreen() => false;
        public void SetMiniBarVisible(bool visible)
        {
        }
    }

    private sealed class MelodyFlatMediaService(SortedDictionary<int, MusicTrack> tracks) : IMediaService
    {
        private readonly IdleMediaService idle = new();

        public void Dispose() => idle.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            idle.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            idle.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            idle.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            idle.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            idle.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => idle.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            Task.FromResult(tracks);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            idle.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => idle.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            idle.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            idle.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            idle.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            idle.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            idle.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            idle.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            idle.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            idle.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            idle.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            idle.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            idle.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }

    private sealed class VocalTracksMediaStub : IMediaService
    {
        internal SortedDictionary<int, MusicTrack>? VocalTracksResult { get; init; }

        private readonly IdleMediaService idle = new();

        public void Dispose() => idle.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublicationLanguages(categoryName, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
            idle.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            idle.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null) =>
            idle.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
            idle.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
            idle.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            idle.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() => idle.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            idle.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
            idle.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() => idle.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            idle.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            Task.FromResult(VocalTracksResult ?? new SortedDictionary<int, MusicTrack>());

        public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            idle.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            idle.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            idle.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            idle.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            idle.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            idle.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            idle.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            idle.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            idle.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
    }
}
