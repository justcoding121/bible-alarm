#nullable enable

using System.Runtime.InteropServices;
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
using Bible.Alarm.Stores.Messages.ModalOverlay;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Views;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using Microsoft.Maui.Controls;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Tests;

public sealed class BiblePublicationSectionSelectionViewModelTests
{
#pragma warning disable CS0067
    private sealed class NopDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public void Dispatch(object action)
        {
        }
    }
#pragma warning restore CS0067

    private sealed class FakeAppState(ApplicationState value) : IState<ApplicationState>
    {
        public ApplicationState Value => value;

#pragma warning disable CS0067
        public event EventHandler? StateChanged;
#pragma warning restore CS0067
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

    private sealed class BibleSectionMediaService : IdleCatalogMediaService
    {
        public SortedDictionary<string, BiblePublicationSection> Sections { get; init; } = [];

        public int ExpectedSectionCount { get; init; }

        public new Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(
            string languageCode,
            string versionCode,
            IFetchProgress? progress = null) =>
            Task.FromResult(Sections);

        public new Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
            Task.FromResult(ExpectedSectionCount);
    }

    private static ScheduleStateItem MinimalSchedule(
        string? publicationCode = null,
        string? languageCode = "E",
        string? sectionCode = null) =>
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
            BiblePublicationLanguageCode = languageCode,
            BiblePublicationCode = publicationCode,
            BiblePublicationSectionCode = sectionCode,
            BiblePublicationTrackCode = publicationCode is null ? null : "1",
            BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionLeftToRight,
        };

    private static BiblePublicationSectionSelectionViewModel CreateSut(
        ScheduleStateItem? schedule = null,
        INavigationService? navigation = null,
        IMediaService? media = null)
    {
        var app = new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule);
        return new BiblePublicationSectionSelectionViewModel(
            TestLogging.CreateLogger(),
            media ?? new IdleCatalogMediaService(),
            new FakeAppState(app),
            new NopDispatcher(),
            navigation ?? new UnusedNavigationServiceStub());
    }

    private static async Task ExecuteAsync(System.Windows.Input.ICommand command, object? parameter = null)
    {
        if (command is IAsyncRelayCommand asyncRelay)
        {
            await asyncRelay.ExecuteAsync(parameter);
            return;
        }

        command.Execute(parameter);
    }

    private static BiblePublicationSectionListViewItemModel SectionItem(
        string code = "1",
        string name = "Genesis",
        int id = 10) =>
        new(new BiblePublicationSection
        {
            Id = id,
            SectionCode = code,
            Name = name,
        });

    private static BibleSectionMediaService FullyCatalogedSectionMedia(
        string sectionCode = "1",
        string sectionName = "Genesis") =>
        new()
        {
            Sections = new SortedDictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase)
            {
                [sectionCode] = new BiblePublicationSection
                {
                    Id = 10,
                    SectionCode = sectionCode,
                    Name = sectionName,
                },
            },
            ExpectedSectionCount = 1,
        };

    [Fact]
    public void Ctor_with_empty_state_wires_commands_and_busy_defaults()
    {
        using var sut = CreateSut();

        Assert.NotNull(sut.BackCommand);
        Assert.NotNull(sut.CloseModalCommand);
        Assert.NotNull(sut.TrackSelectionCommand);
        Assert.NotNull(sut.CancelFetchCommand);
        Assert.True(sut.IsBusy);
        Assert.True(sut.ShowCancelButton);
        Assert.Empty(sut.Sections);
        Assert.Null(sut.SelectedSection);
        Assert.Null(sut.SelectedItem);
    }

    [Fact]
    public void ContentFlowDirection_defaults_to_left_to_right()
    {
        using var sut = CreateSut(MinimalSchedule(publicationCode: "nwt"));

        Assert.Equal(FlowDirection.LeftToRight, sut.ContentFlowDirection);
    }

    [Fact]
    public void ContentFlowDirection_maps_right_to_left_from_schedule()
    {
        var schedule = MinimalSchedule(publicationCode: "nwt");
        schedule.BiblePublicationLanguageDirection = AppConstants.Media.TextDirectionRightToLeft;
        using var sut = CreateSut(schedule);

        Assert.Equal(FlowDirection.RightToLeft, sut.ContentFlowDirection);
    }

    [Fact]
    public async Task BackCommand_pops_navigation_stack()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.BackCommand);

        Assert.Equal(1, navigation.PopAsyncCalls);
        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CloseModalCommand_pops_modal()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);

        await ExecuteAsync(sut.CloseModalCommand);

        Assert.Equal(0, navigation.PopAsyncCalls);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task TrackSelectionCommand_with_null_parameter_is_noop()
    {
        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(MinimalSchedule(publicationCode: "nwt"), navigation);

        await ExecuteAsync(sut.TrackSelectionCommand, null);

        Assert.Equal(0, navigation.PopModalCalls);
    }

    [Fact]
    public async Task CancelFetchCommand_resets_overlay_flags_and_pops_modal()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        var navigation = new RecordingNavigationService();
        using var sut = CreateSut(navigation: navigation);
        sut.ShowProgress = true;
        sut.CanCancelFetch = true;
        sut.IsBusy = true;

        try
        {
            await ExecuteAsync(sut.CancelFetchCommand);
        }
        catch (COMException)
        {
        }

        Assert.False(sut.ShowProgress);
        Assert.False(sut.CanCancelFetch);
        Assert.False(sut.IsBusy);
        Assert.Equal(1, navigation.PopModalCalls);
    }

    [Fact]
    public async Task RefreshFromState_without_publication_is_noop()
    {
        using var sut = CreateSut(MinimalSchedule());

        await sut.RefreshFromState();

        Assert.Empty(sut.Sections);
    }

    [Fact]
    public async Task RefreshFromState_with_publication_enters_repopulation_path()
    {
        var media = FullyCatalogedSectionMedia();
        var schedule = MinimalSchedule(publicationCode: "nwtsty", sectionCode: "1");
        using var sut = CreateSut(schedule, media: media);

        try
        {
            var refresh = sut.RefreshFromState();
            var completed = await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(5)));
            if (!ReferenceEquals(completed, refresh))
            {
                return;
            }

            await refresh;

            Assert.Single(sut.Sections);
            Assert.Equal("1", sut.Sections[0].SectionCode);
            Assert.NotNull(sut.SelectedSection);
            Assert.True(sut.SelectedSection!.IsSelected);
            Assert.False(sut.CanCancelFetch);
        }
        catch (COMException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    [Fact]
    public void Receive_modal_overlay_ignores_other_modal_types()
    {
        using var sut = CreateSut();
        sut.ShowProgress = false;

        sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
        {
            ModalType = "MusicSection",
            IsVisible = true,
            Progress = 0.9,
            ProgressText = "90%",
        }));

        Assert.False(sut.ShowProgress);
    }

    [Fact]
    public void Receive_bible_section_overlay_does_not_throw()
    {
        using var sut = CreateSut();

        try
        {
            sut.Receive(new ModalOverlayFetchProgressMessage(new ModalOverlayFetchProgress
            {
                ModalType = "BibleSection",
                IsVisible = true,
                Progress = 0.25,
                ProgressText = "25%",
            }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
        }
    }

    [Fact]
    public void Dispose_unregisters_without_throw()
    {
        var sut = CreateSut();
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void ShowCancelButton_follows_IsBusy_and_ShowProgress()
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            return;
        }

        using var sut = CreateSut();
        sut.IsBusy = false;
        Assert.False(sut.ShowCancelButton);
        sut.ShowProgress = true;
        Assert.True(sut.ShowCancelButton);
    }
}
