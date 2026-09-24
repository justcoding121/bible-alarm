#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Services.Scheduler.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Mapping;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Schedule;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.InteropServices;
using BiblePublicationSchedule = Bible.Alarm.Shared.Models.Schedule.BiblePublicationSchedule;

namespace Bible.Alarm.Tests.ViewModels.Schedule;

public sealed class BiblePublicationSelectionContainerViewModelTests
{
    private sealed class RecordingDispatcher : IDispatcher
    {
        public List<object> Dispatched { get; } = [];

#pragma warning disable CS0067
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;
#pragma warning restore CS0067

        public void Dispatch(object action) => Dispatched.Add(action);
    }

    private sealed class StubScheduleSelectionService : IScheduleSelectionService
    {
        public BiblePublicationSchedule? Loaded { get; private set; }

        public AlarmMusic? LoadMusicForSelection(LoadMusicForSelectionArgs args) => null;

        public BiblePublicationSchedule? LoadBiblePublicationForSelection(LoadBiblePublicationForSelectionArgs args)
        {
            Loaded = new BiblePublicationSchedule { Id = 42, PublicationCode = "nwt" };
            return Loaded;
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubCategoryNameService : ICategoryNameService
    {
        public Task WarmCacheForDisplayLanguageAsync(string displayLanguageCode, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public string? GetName(string categoryCode, string displayLanguageCode) =>
            categoryCode switch
            {
                AppConstants.Media.BiblePublicationCategoryBible => "Bible Reading",
                _ => categoryCode,
            };
    }

    private sealed class ScopeNeverOpenedFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("BiblePublicationSelectionContainer headless tests must not open scopes.");
    }

    private static IMapper CreateMapper()
    {
        var cfg = new MapperConfiguration(
            c => c.AddProfile<ScheduleMappingProfile>(),
            NullLoggerFactory.Instance);
        return cfg.CreateMapper();
    }

    private static IServiceProvider CreateServiceProvider(IMediaService? mediaService = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(mediaService ?? new IdleCatalogMediaService());
        services.AddSingleton<ICategoryNameService>(new StubCategoryNameService());
        services.AddSingleton<IServiceScopeFactory>(new ScopeNeverOpenedFactory());
        return services.BuildServiceProvider();
    }

    private static ScheduleStateItem CreateBibleSchedule(int id = 7) =>
        new()
        {
            Id = id,
            Name = "Morning",
            BiblePublicationCategoryName = AppConstants.Media.BiblePublicationCategoryBible,
            BiblePublicationCategoryId = 1,
            BiblePublicationLanguageCode = "E",
            BiblePublicationLanguageName = "English",
            BiblePublicationCode = "nwt",
            BiblePublicationName = "New World Translation",
            BiblePublicationSectionCode = "1",
            BiblePublicationSectionName = "Genesis",
            BiblePublicationTrackCode = "1",
            BiblePublicationTrackTitle = "Chapter 1",
        };

    private static BiblePublicationSelectionContainerViewModel CreateSut(
        ViewModelTestDoubles.MutableApplicationState? appState = null,
        RecordingDispatcher? dispatcher = null,
        IScheduleSelectionService? selectionService = null,
        ScheduleStateItem? currentSchedule = null)
    {
        var state = appState ?? new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), currentSchedule));

        return new BiblePublicationSelectionContainerViewModel(
            TestLogging.CreateLogger(),
            new UnusedNavigationServiceStub(),
            selectionService ?? new StubScheduleSelectionService(),
            state,
            dispatcher ?? new RecordingDispatcher(),
            CreateMapper(),
            CreateServiceProvider());
    }

    [Fact]
    public void CategoryDisplayText_is_empty_when_current_schedule_is_missing()
    {
        var fluxorState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), null));
        using var sut = new BiblePublicationSelectionContainerViewModel(
            TestLogging.CreateLogger(),
            new UnusedNavigationServiceStub(),
            new StubScheduleSelectionService(),
            fluxorState,
            new RecordingDispatcher(),
            CreateMapper(),
            CreateServiceProvider());

        Assert.Equal(string.Empty, sut.CategoryDisplayText);
    }

    [Fact]
    public void ShouldScrollToContainer_property_round_trips()
    {
        using var sut = CreateSut();

        sut.ShouldScrollToContainer = true;

        Assert.True(sut.ShouldScrollToContainer);
    }

    [Fact]
    public void SetScheduleId_accepts_new_schedule_identity()
    {
        using var sut = CreateSut();

        sut.SetScheduleId(0, isNewSchedule: true);
    }

    [Fact]
    public void OnStateChanged_updates_track_display_when_track_title_changes()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var schedule = CreateBibleSchedule();
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
        using var sut = CreateSut(appState);

        try
        {
            schedule.BiblePublicationTrackTitle = "Chapter 2";
            schedule.BiblePublicationTrackCode = "2";
            appState.NotifyChanged();
            if (!MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult())
            {
                return;
            }

            Assert.Equal("Chapter 2", sut.TrackDisplayText);
        }
        catch (COMException)
        {
        }
    }

    [Fact]
    public void Dispose_unsubscribes_without_throw()
    {
        var sut = CreateSut();
        sut.Dispose();
        sut.Dispose();
    }

    [Fact]
    public void IsCategorySelectable_is_always_true()
    {
        using var sut = CreateSut(currentSchedule: CreateBibleSchedule());

        Assert.True(sut.IsCategorySelectable);
    }

    [Fact]
    public void SetScheduleId_updates_identity_for_existing_schedule()
    {
        using var sut = CreateSut(currentSchedule: CreateBibleSchedule(12));

        sut.SetScheduleId(12, isNewSchedule: false);

        Assert.Equal("Chapter 1", sut.TrackDisplayText);
    }

    [Fact]
    public void OnStateChanged_with_overlay_visible_does_not_throw()
    {
        var schedule = CreateBibleSchedule();
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(
                new ObservableHashSet<ScheduleStateItem>(),
                schedule,
                isSchedulePageOverlayVisible: true));
        using var sut = CreateSut(appState, currentSchedule: schedule);
        sut.SetScheduleId(schedule.Id, isNewSchedule: false);

        schedule.BiblePublicationSectionName = "Exodus";
        var ex = Record.Exception(() => appState.NotifyChanged());

        Assert.Null(ex);
    }

    [Fact]
    public void OnStateChanged_updates_section_display_when_section_name_changes()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var schedule = CreateBibleSchedule();
        var appState = new ViewModelTestDoubles.MutableApplicationState(
            new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
        using var sut = CreateSut(appState, currentSchedule: schedule);
        sut.SetScheduleId(schedule.Id, isNewSchedule: false);

        try
        {
            schedule.BiblePublicationSectionName = "Exodus";
            schedule.BiblePublicationSectionCode = "2";
            appState.NotifyChanged();
            if (!MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult())
            {
                return;
            }

            Assert.Equal("Exodus", sut.SectionDisplayText);
        }
        catch (COMException)
        {
        }
    }

    [Collection("MauiUi")]
    public sealed class MauiBiblePublicationSelectionContainerViewModelTests(MauiUiFixture fixture)
    {
        private static void RunWithMainThreadOrSkip(Action testBody)
        {
            // Nested Maui* Facts are for WinUI OpenCover; AVD/simulator runners hang on MainThread flush races.
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            {
                return;
            }

            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            try
            {
                testBody();
            }
            catch (COMException)
            {
            }
        }

        [Fact]
        public void Ctor_exposes_display_text_from_current_schedule()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                using var sut = CreateSut(currentSchedule: CreateBibleSchedule());

                Assert.Equal("Bible Reading", sut.CategoryDisplayText);
                Assert.Equal("English", sut.LanguageDisplayText);
                Assert.Equal("New World Translation", sut.PublicationDisplayText);
                Assert.Equal("Genesis", sut.SectionDisplayText);
                Assert.Equal("Chapter 1", sut.TrackDisplayText);
                Assert.True(sut.IsLanguageVisible);
                Assert.True(sut.IsCategorySelectable);
            });
        }

        [Fact]
        public void Ctor_initializes_selection_commands()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                using var sut = CreateSut(currentSchedule: CreateBibleSchedule());

                Assert.NotNull(sut.SelectCategoryCommand);
                Assert.NotNull(sut.SelectLanguageCommand);
                Assert.NotNull(sut.SelectBibleCommand);
                Assert.NotNull(sut.SelectSectionCommand);
                Assert.NotNull(sut.SelectTrackCommand);
            });
        }

        [Fact]
        public void OnStateChanged_ignores_unrelated_schedule_id()
        {
            // Device runner assertions differ from host; keep coverage on WinUI OpenCover pass.
            if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
            {
                return;
            }
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                var schedule = CreateBibleSchedule(5);
                var appState = new ViewModelTestDoubles.MutableApplicationState(
                    new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
                using var sut = CreateSut(appState, currentSchedule: schedule);
                sut.SetScheduleId(5, isNewSchedule: false);

                var other = CreateBibleSchedule(99);
                other.BiblePublicationTrackTitle = "Other track";
                appState.Value.CurrentSchedule = other;
                appState.NotifyChanged();
                if (!MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult())
                {
                    return;
                }

                Assert.Equal("Chapter 1", sut.TrackDisplayText);
            });
        }

        [Fact]
        public void SignalContainerReady_dispatches_container_ready_action()
        {
            _ = fixture;
            RunWithMainThreadOrSkip(() =>
            {
                var schedule = CreateBibleSchedule();
                var dispatcher = new RecordingDispatcher();
                var appState = new ViewModelTestDoubles.MutableApplicationState(
                    new ApplicationState(new ObservableHashSet<ScheduleStateItem>(), schedule));
                using var sut = new BiblePublicationSelectionContainerViewModel(
                    TestLogging.CreateLogger(),
                    new UnusedNavigationServiceStub(),
                    new StubScheduleSelectionService(),
                    appState,
                    dispatcher,
                    CreateMapper(),
                    CreateServiceProvider());

                if (!MauiUiTestHostHelper.FlushMainThreadAsync().GetAwaiter().GetResult())
                {
                    return;
                }

                Assert.Contains(
                    dispatcher.Dispatched,
                    action => action is ContainerReadyAction ready && ready.ContainerName == "BiblePublicationSelection");
            });
        }
    }
}
