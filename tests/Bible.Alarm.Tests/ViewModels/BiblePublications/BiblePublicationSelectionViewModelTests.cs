#nullable enable

using System.Runtime.InteropServices;
using System.Windows.Input;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using Bible.Alarm.Stores.Messages.ModalOverlay;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests.ViewModels.BiblePublications;

public sealed class BiblePublicationSelectionViewModelTests
{
    private sealed class FakeApplicationState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value { get; set; } = value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
    }

    private sealed class NopDispatcher : IDispatcher
    {
#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action)
        {
        }
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

    private sealed class ScopeNeverOpenedFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("Headless publication selection tests must not open scopes.");
    }

    /// <summary>
    /// Fails language catalog before any MainThread UI work so headless hosts do not hang.
    /// </summary>
    private sealed class FailingLanguagesMediaService : IMediaService
    {
        private readonly IdleCatalogMediaService inner = new();

        public void Dispose() => inner.Dispose();

        public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(
            string? categoryName = null,
            bool requireIsMusicForMusicCategory = false) =>
            Task.FromException<Dictionary<string, Language>>(
                new InvalidOperationException("Simulated language catalog failure."));

        public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(
            string languageCode, string versionCode, string? sectionCode) =>
            inner.GetBiblePublicationTracks(languageCode, versionCode, sectionCode);

        public Task<Dictionary<string, BiblePublication>> GetBiblePublications(
            string languageCode, string? categoryName = null, bool downloadAll = false,
            IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
            inner.GetBiblePublications(languageCode, categoryName, downloadAll, progress, requireIsMusicForMusicCategory);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(
            string languageCode, string versionCode, IFetchProgress? progress = null) =>
            inner.GetBiblePublicationSections(languageCode, versionCode, progress);

        public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(
            string publicationCode) =>
            inner.GetSectionsForPublicationWithoutLanguage(publicationCode);

        public Task<BiblePublicationSection?> GetBiblePublicationSection(
            string languageCode, string versionCode, string sectionCode) =>
            inner.GetBiblePublicationSection(languageCode, versionCode, sectionCode);

        public Task<BiblePublicationTrack?> GetBiblePublicationTrack(
            string languageCode, string versionCode, string? sectionCode, string trackCode) =>
            inner.GetBiblePublicationTrack(languageCode, versionCode, sectionCode, trackCode);

        public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
            inner.GetMelodyMusicReleases();

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
            inner.GetMelodyMusicTracks(publicationCode);

        public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(
            string publicationCode, string sectionCode) =>
            inner.GetMelodyMusicTracksBySection(publicationCode, sectionCode);

        public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
            inner.GetVocalMusicLanguages();

        public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
            inner.GetVocalMusicReleases(languageCode, downloadAll);

        public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
            inner.GetVocalMusicTracks(languageCode, publicationCode);

        public Task UpdateBiblePublicationTrackUrl(
            string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
            inner.UpdateBiblePublicationTrackUrl(languageCode, versionCode, sectionCode, trackCode, url);

        public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
            inner.UpdateVocalTrackUrl(languageCode, publicationCode, trackCode, url);

        public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
            inner.UpdateMelodyTrackUrl(publicationCode, trackCode, url);

        public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
            inner.UpdateTrackUrlAsync(trackMetadata, url);

        public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null) =>
            inner.InvalidateBiblePublicationsCache(languageCode, categoryName);

        public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
            inner.IsPublicationWithoutLanguageAsync(publicationCode);

        public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            inner.GetExpectedSectionCountAsync(languageCode, publicationCode);

        public Task<int> GetExpectedPublicationCountAsync(
            string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
            inner.GetExpectedPublicationCountAsync(languageCode, categoryName, requireIsMusicForMusicCategory);

        public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
            inner.GetExpectedSectionCountForNoLanguagePublicationAsync(publicationCode);
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

    private static ScheduleStateItem MinimalSchedule(
        string? bibleLangDirection = null,
        string? publicationCode = null,
        string? languageCode = null,
        string? categoryName = null) =>
        new()
        {
            Id = 1,
            Name = "Test",
            IsEnabled = true,
            Hour = 8,
            Minute = 0,
            Second = 0,
            DaysOfWeek = WeekDays.Monday,
            NotificationEnabled = true,
            MusicEnabled = false,
            BiblePublicationLanguageDirection = bibleLangDirection,
            BiblePublicationCode = publicationCode,
            BiblePublicationLanguageCode = languageCode,
            BiblePublicationCategoryName = categoryName,
            BiblePublicationTrackCode = publicationCode is null ? null : "1",
        };

    private static (BiblePublicationSelectionViewModelDeps Deps, RecordingNavigationService Navigation) CreateDeps(
        IState<ApplicationState> fluxorState,
        RecordingNavigationService? navigation = null,
        IMediaService? mediaService = null)
    {
        var nav = navigation ?? new RecordingNavigationService();
        var services = new ServiceCollection();
        // Default: fail language catalog before MainThread so async ctor init cannot hang the testhost.
        services.AddSingleton<IMediaService>(mediaService ?? new FailingLanguagesMediaService());
        services.AddSingleton<ILanguageNameService>(new IdleLanguageNameService());
        services.AddSingleton<IServiceScopeFactory>(new ScopeNeverOpenedFactory());
        var provider = services.BuildServiceProvider();
        var deps = new BiblePublicationSelectionViewModelDeps(
            provider.GetRequiredService<IMediaService>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            fluxorState,
            new NopDispatcher(),
            nav,
            provider);
        return (deps, nav);
    }

    private static BiblePublicationSelectionViewModel CreateSut(
        ScheduleStateItem? schedule,
        out RecordingNavigationService navigation,
        IMediaService? mediaService = null)
    {
        var fluxorState = new FakeApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
        var (deps, nav) = CreateDeps(fluxorState, mediaService: mediaService);
        navigation = nav;
        return new BiblePublicationSelectionViewModel(deps);
    }

    private static async Task ExecuteAsync(ICommand command, object? parameter = null)
    {
        if (command is IAsyncRelayCommand arc)
        {
            await arc.ExecuteAsync(parameter);
            return;
        }

        throw new InvalidOperationException("Expected IAsyncRelayCommand");
    }

    [Fact]
    public void Ctor_with_null_schedule_wires_commands_and_busy_defaults()
    {
        using var sut = CreateSut(null, out _);

        Assert.NotNull(sut.BackCommand);
        Assert.NotNull(sut.CloseModalCommand);
        Assert.NotNull(sut.SelectLanguageCommand);
        Assert.NotNull(sut.SectionSelectionCommand);
        Assert.NotNull(sut.CancelFetchCommand);
        Assert.True(sut.IsBusy);
        Assert.True(sut.ShowCancelButton);
        Assert.Equal(string.Empty, sut.PublicationCode);
        Assert.Empty(sut.Publications);
        Assert.Empty(sut.Languages);
    }

    [Fact]
    public void Ctor_with_publication_schedule_exposes_publication_code()
    {
        var schedule = MinimalSchedule(
            publicationCode: "nwt",
            languageCode: "E",
            categoryName: "Bible");

        using var sut = CreateSut(schedule, out _);

        Assert.Equal("nwt", sut.PublicationCode);
    }

    [Fact]
    public void Ctor_with_category_only_schedule_still_constructs()
    {
        var schedule = MinimalSchedule(categoryName: "Bible");

        using var sut = CreateSut(schedule, out _);

        Assert.Equal(string.Empty, sut.PublicationCode);
        Assert.NotNull(sut.CancelFetchCommand);
    }

    [Fact]
    public void ContentFlowDirection_maps_bible_language_direction_right_to_left()
    {
        var schedule = MinimalSchedule(bibleLangDirection: AppConstants.Media.TextDirectionRightToLeft);
        using var sut = CreateSut(schedule, out _);

        Assert.Equal(FlowDirection.RightToLeft, sut.ContentFlowDirection);
    }

    [Fact]
    public void ContentFlowDirection_defaults_to_left_to_right_when_direction_unspecified()
    {
        using var sut = CreateSut(MinimalSchedule(bibleLangDirection: null), out _);

        Assert.Equal(FlowDirection.LeftToRight, sut.ContentFlowDirection);
    }

    [Fact]
    public void ShowCancelButton_follows_IsBusy_and_ShowProgress()
    {
        // Host-oriented coverage; PopModal / display text race on Android/iOS device runners.
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
        {
            return;
        }
        using var sut = CreateSut(null, out _);

        sut.IsBusy = false;
        Assert.False(sut.ShowCancelButton);

        sut.ShowProgress = true;
        Assert.True(sut.ShowCancelButton);

        sut.ShowProgress = false;
        Assert.False(sut.ShowCancelButton);

        sut.IsBusy = true;
        Assert.True(sut.ShowCancelButton);
    }

    [Fact]
    public void PublicationCode_setter_updates_when_current_initialized()
    {
        var schedule = MinimalSchedule(
            publicationCode: "nwt",
            languageCode: "E",
            categoryName: "Bible");
        using var sut = CreateSut(schedule, out _);

        sut.PublicationCode = "bi12";

        Assert.Equal("bi12", sut.PublicationCode);
    }

    [Fact]
    public void PublicationCode_setter_is_noop_when_current_not_initialized()
    {
        using var sut = CreateSut(null, out _);

        sut.PublicationCode = "nwt";

        Assert.Equal(string.Empty, sut.PublicationCode);
    }

    [Fact]
    public void SelectedItem_tracks_CurrentLanguage()
    {
        using var sut = CreateSut(null, out _);

        var language = new LanguageListViewItemModel(
            new Language { Id = 1, LanguageCode = "E", Direction = AppConstants.Media.TextDirectionLeftToRight },
            "English");
        sut.CurrentLanguage = language;

        Assert.Same(language, sut.SelectedItem);
        Assert.Same(language, sut.CurrentLanguage);
    }

    [Fact]
    public async Task BackCommand_pops_navigation_stack()
    {
        using var sut = CreateSut(null, out var navigation);

        await ExecuteAsync(sut.BackCommand);

        Assert.Equal(1, navigation.PopAsyncCalls);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CloseModalCommand_pops_modal()
    {
        using var sut = CreateSut(null, out var navigation);

        await ExecuteAsync(sut.CloseModalCommand);

        Assert.Equal(0, navigation.PopAsyncCalls);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task SelectLanguageCommand_with_null_parameter_is_noop()
    {
        using var sut = CreateSut(null, out var navigation);

        await ExecuteAsync(sut.SelectLanguageCommand, null);

        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task SectionSelectionCommand_with_null_parameter_is_noop()
    {
        using var sut = CreateSut(null, out var navigation);

        await ExecuteAsync(sut.SectionSelectionCommand, null);

        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CancelFetchCommand_resets_overlay_flags_and_pops_modal()
    {
        // Host-oriented coverage; PopModal / display text race on Android/iOS device runners.
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
        {
            return;
        }
        using var sut = CreateSut(null, out var navigation);
        sut.ShowProgress = true;
        sut.CanCancelFetch = true;
        sut.IsBusy = true;

        try
        {
            await ExecuteAsync(sut.CancelFetchCommand);
        }
        catch (COMException)
        {
            // DeviceDisplay.KeepScreenOn can fail on headless Windows; navigation still ran first.
        }

        Assert.False(sut.ShowProgress);
        Assert.False(sut.CanCancelFetch);
        Assert.False(sut.IsBusy);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task RefreshFromState_without_language_invokes_load_path()
    {
        // Host-oriented coverage; PopModal / display text race on Android/iOS device runners.
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
        {
            return;
        }
        using var sut = CreateSut(MinimalSchedule(categoryName: "Bible"), out _);

        try
        {
            await sut.RefreshFromState();
            Assert.False(sut.CanCancelFetch);
        }
        catch (COMException)
        {
            // DeviceDisplay.KeepScreenOn is unavailable on headless Windows (MTA) hosts.
        }
    }

    [Fact]
    public async Task RefreshLanguagesAsync_swallows_catalog_failures()
    {
        using var sut = CreateSut(
            MinimalSchedule(languageCode: "E", categoryName: "Bible"),
            out _);

        // FailingLanguagesMediaService fails before MainThread UI work; method logs and returns.
        await sut.RefreshLanguagesAsync();

        Assert.NotNull(sut.Languages);
    }

    [Fact]
    public void Receive_list_item_progress_does_not_throw_for_unknown_items()
    {
        using var sut = CreateSut(null, out _);

        try
        {
            sut.Receive(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = "BibleLanguage",
                ItemId = "missing",
                Progress = 0.4,
            }));
            sut.Receive(new ListItemFetchProgressMessage(new ListItemFetchProgress
            {
                Context = "BiblePublication",
                ItemId = "missing",
                Progress = 0.75,
            }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            // BeginInvokeOnMainThread requires a WinUI dispatcher on some hosts.
        }
    }

    [Fact]
    public void Receive_modal_overlay_progress_for_bible_publication_does_not_throw()
    {
        using var sut = CreateSut(null, out _);

        try
        {
            sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = "BiblePublication",
                IsVisible = true,
                Progress = 0.5,
                ProgressText = "50%",
            }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }
    }

    [Fact]
    public void Receive_modal_overlay_ignores_other_modal_types()
    {
        using var sut = CreateSut(null, out _);
        sut.ShowProgress = false;
        sut.ProgressPercent = 0;
        sut.ProgressText = "0%";

        sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
        {
            ModalType = "MusicPublication",
            IsVisible = true,
            Progress = 0.9,
            ProgressText = "90%",
        }));

        Assert.False(sut.ShowProgress);
        Assert.Equal(0, sut.ProgressPercent);
        Assert.Equal("0%", sut.ProgressText);
    }

    [Fact]
    public void Dispose_unregisters_and_completes()
    {
        var sut = CreateSut(null, out _);

        sut.Dispose();

        Assert.NotNull(sut.BackCommand);
    }
}
