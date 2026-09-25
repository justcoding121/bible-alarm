#nullable enable

using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.ViewModels.BiblePublications.BibleSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSelectionCommandHandlerTests
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

    private sealed class RecordingNavigationService : INavigationService
    {
        public int PopAsyncCalls { get; private set; }
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

        public Task PopAsync()
        {
            PopAsyncCalls++;
            return Task.CompletedTask;
        }

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

    private static async Task ExecuteAsync(ICommand command)
    {
        if (command is IAsyncRelayCommand arc)
        {
            await arc.ExecuteAsync(null);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand");
    }

    [Fact]
    public async Task CreateBackCommand_Pops_Navigation_Stack()
    {
        var navigation = new RecordingNavigationService();
        var sut = new BiblePublicationSelectionCommandHandler(
            new IdleMediaService(),
            new FakeApplicationState(new ApplicationState([], currentSchedule: null)),
            new RecordingDispatcher(),
            navigation);

        await ExecuteAsync(sut.CreateBackCommand());

        Assert.Equal(1, navigation.PopAsyncCalls);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CreateCloseModalCommand_Pops_Modal()
    {
        var navigation = new RecordingNavigationService();
        var sut = new BiblePublicationSelectionCommandHandler(
            new IdleMediaService(),
            new FakeApplicationState(new ApplicationState([], currentSchedule: null)),
            new RecordingDispatcher(),
            navigation);

        await ExecuteAsync(sut.CreateCloseModalCommand());

        Assert.Equal(0, navigation.PopAsyncCalls);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CreateSectionSelectionCommand_is_noop_when_publication_null()
    {
        var navigation = new RecordingNavigationService();
        var sut = CreateCommandHandler(navigation);
        var selectors = CreateSelectors(null);

        await ExecuteAsync(sut.CreateSectionSelectionCommand(selectors, CreateUiBindings()), null);

        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CreateSectionSelectionCommand_is_noop_when_schedule_missing()
    {
        var navigation = new RecordingNavigationService();
        var sut = CreateCommandHandler(navigation, schedule: null, useDefaultScheduleWhenNull: false);
        var publication = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "nwt",
            Name = "NWT",
            Id = 1,
        });
        var selectors = CreateSelectors(null);

        await ExecuteAsync(sut.CreateSectionSelectionCommand(selectors, CreateUiBindings()), publication);

        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public void ResolveLanguageCodeForSectionPublicationTap_prefers_schedule_language_for_language_bound_publication()
    {
        var method = typeof(BiblePublicationSelectionCommandHandler).GetMethod(
            "ResolveLanguageCodeForSectionPublicationTap",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var publication = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "nwt",
            Name = "NWT",
            LanguageId = 1,
            Id = 2,
        });
        var schedule = new ScheduleStateItem
        {
            Id = 1,
            BiblePublicationLanguageCode = "E",
        };
        var selectors = new SectionSelectionSelectors(
            () => new LanguageListViewItemModel(new Language { LanguageCode = "F" }, "French"),
            () => new ObservableCollection<PublicationListViewItemModel>(),
            () => new Dictionary<string, PublicationListViewItemModel>(),
            () => null);

        var code = (string)method!.Invoke(null, [publication, schedule, selectors])!;
        Assert.Equal("E", code);
    }

    [Fact]
    public void ResolveLanguageCodeForSectionPublicationTap_uses_default_for_no_language_publication()
    {
        var method = typeof(BiblePublicationSelectionCommandHandler).GetMethod(
            "ResolveLanguageCodeForSectionPublicationTap",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var publication = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "iam",
            Name = "Melodies",
            LanguageId = null,
            Id = 3,
        });
        var schedule = new ScheduleStateItem { Id = 1, BiblePublicationLanguageCode = null };
        var selectors = CreateSelectors(null);

        var code = (string)method!.Invoke(null, [publication, schedule, selectors])!;
        Assert.Equal(AppConstants.Media.DefaultLanguageCode, code);
    }

    [Fact]
    public void ResolveLanguageCodeForSectionPublicationTap_falls_back_to_publication_language_code()
    {
        var method = typeof(BiblePublicationSelectionCommandHandler).GetMethod(
            "ResolveLanguageCodeForSectionPublicationTap",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var publication = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "nwt",
            Name = "NWT",
            LanguageId = 1,
            Id = 2,
            Language = new Language { LanguageCode = "F" },
        });
        var schedule = new ScheduleStateItem { Id = 1, BiblePublicationLanguageCode = null };
        var selectors = CreateSelectors(null);

        var code = (string)method!.Invoke(null, [publication, schedule, selectors])!;
        Assert.Equal("F", code);
    }

    [Fact]
    public void CreateBiblePublicationItemFromSelection_preserves_category_and_maps_track_fields()
    {
        var method = typeof(BiblePublicationSelectionCommandHandler).GetMethod(
            "CreateBiblePublicationItemFromSelection",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var publication = new PublicationListViewItemModel(new BiblePublication
        {
            PublicationCode = "nwt",
            Name = "Holy Scriptures",
            Id = 1,
        });
        var language = new LanguageListViewItemModel(
            new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            "English");
        var schedule = new ScheduleStateItem
        {
            Id = 5,
            BiblePublicationCategoryId = 2,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
        };

        var item = Assert.IsType<BiblePublicationStateItem>(
            method!.Invoke(null, [publication, "1", "3", "Genesis", "Chapter 3", language, schedule])!);

        Assert.Equal("nwt", item.PublicationCode);
        Assert.Equal("E", item.LanguageCode);
        Assert.Equal("1", item.SectionCode);
        Assert.Equal("3", item.TrackCode);
        Assert.Equal("Genesis", item.SectionName);
        Assert.Equal("Chapter 3", item.TrackTitle);
        Assert.Equal(AppConstants.Media.BiblePublicationCategoryBible, item.CategoryName);
        Assert.Equal(2, item.CategoryId);
    }

    [Fact]
    public void CreateBiblePublicationItemForLanguageSelection_maps_language_and_publication_fields()
    {
        var method = typeof(BiblePublicationSelectionCommandHandler).GetMethod(
            "CreateBiblePublicationItemForLanguageSelection",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var language = new LanguageListViewItemModel(
            new Language { Id = 3, LanguageCode = "S", Direction = AppConstants.Media.TextDirectionLeftToRight },
            "Spanish");

        var item = Assert.IsType<BiblePublicationStateItem>(
            method!.Invoke(null, [language, "nwt", "40", "1", "Matthew", "NWT", "Matthew 1"])!);

        Assert.Equal("S", item.LanguageCode);
        Assert.Equal("nwt", item.PublicationCode);
        Assert.Equal("40", item.SectionCode);
        Assert.Equal("1", item.TrackCode);
        Assert.Equal("Spanish", item.LanguageName);
        Assert.Equal("Matthew", item.SectionName);
        Assert.Equal("NWT", item.PublicationName);
        Assert.Equal("Matthew 1", item.TrackTitle);
    }

    private static BiblePublicationSelectionCommandHandler CreateCommandHandler(
        RecordingNavigationService navigation,
        ScheduleStateItem? schedule = null,
        bool useDefaultScheduleWhenNull = true)
    {
        if (schedule == null && useDefaultScheduleWhenNull)
        {
            schedule = new ScheduleStateItem
        {
            Id = 1,
            Name = "Morning",
            IsEnabled = true,
            Hour = 7,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            SnoozeMinutes = 5,
            NumberOfTracksToPlay = 1,
            AlwaysPlayFromStart = false,
            CurrentPlayItem = PlayType.Bible,
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationLanguageCode = "E",
            };
        }

        return new BiblePublicationSelectionCommandHandler(
            new IdleMediaService(),
            new FakeApplicationState(new ApplicationState([], schedule)),
            new RecordingDispatcher(),
            navigation);
    }

    private static SectionSelectionSelectors CreateSelectors(LanguageListViewItemModel? currentLanguage) =>
        new(
            () => currentLanguage,
            () => new ObservableCollection<PublicationListViewItemModel>(),
            () => new Dictionary<string, PublicationListViewItemModel>(),
            () => null);

    private static SectionSelectionUiBindings CreateUiBindings() =>
        new(_ => { }, _ => { }, _ => { }, _ => { });

    private static async Task ExecuteAsync(ICommand command, PublicationListViewItemModel? parameter)
    {
        if (command is IAsyncRelayCommand<PublicationListViewItemModel> typed)
        {
            await typed.ExecuteAsync(parameter);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand<PublicationListViewItemModel>");
    }
}
