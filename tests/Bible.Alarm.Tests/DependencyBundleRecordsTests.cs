#nullable enable

using System.Collections.Generic;
using System.Threading;
using AutoMapper;
using Bible.Alarm.Services.Bootstrap;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Shared.DataStructures;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Stores.Actions;
using Bible.Alarm.Stores.Actions.BiblePublications;
using Bible.Alarm.Stores.Actions.Playback;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Effects;
using Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;
using Bible.Alarm.ViewModels.Schedule;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainer;
using Bible.Alarm.ViewModels.ScheduleViewModelHelpers.MusicSelection;
using Bible.Alarm.ViewModels.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bible.Alarm.Tests;

public sealed class DependencyBundleRecordsTests
{
    [Fact]
    public void MediaServiceDependencies_primary_ctor_round_trips()
    {
        var sut = new MediaServiceDependencies(
            null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.MediaIndexService);
        Assert.Null(sut.ScopeFactory);
    }

    [Fact]
    public void MediaCacheServiceDeps_primary_ctor_round_trips()
    {
        var sut = new MediaCacheServiceDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.TrackCdnUrlRefresher);
    }

    [Fact]
    public void PlaybackViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new PlaybackViewModelDeps(null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.PlaybackService);
        Assert.Null(sut.MainThreadScheduler);
    }

    [Fact]
    public void HomeViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new HomeViewModelDeps(null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Dispatcher);
        Assert.Null(sut.Mapper);
    }

    [Fact]
    public void HomeStateChangeHandlerDeps_primary_ctor_round_trips()
    {
        var logger = TestLogging.CreateLogger();
        var mapper = new MapperConfiguration(_ => { }, NullLoggerFactory.Instance).CreateMapper();
        var preparer = new ScheduleDataPreparer(mapper);
        var viewModelManager = new ScheduleViewModelManager(
            logger,
            new ServiceCollection().BuildServiceProvider(),
            _ => { });

        var sut = new HomeStateChangeHandlerDeps(logger, preparer, viewModelManager);

        Assert.Same(logger, sut.Logger);
        Assert.Same(preparer, sut.DataPreparer);
        Assert.Same(viewModelManager, sut.ViewModelManager);
    }

    [Fact]
    public void HomeStateChangeHandlerCallbacks_invokes_SetIsBusy()
    {
        var busyCalls = 0;
        var sut = new HomeStateChangeHandlerCallbacks(
            _ => busyCalls++,
            () => false,
            () => null,
            _ => { },
            () => { },
            () => { },
            () => Task.CompletedTask,
            () => false);

        sut.SetIsBusy(true);

        Assert.Equal(1, busyCalls);
    }

    [Fact]
    public void EventHandlerManagerDeps_primary_ctor_round_trips()
    {
        var sut = new EventHandlerManagerDeps(null!, null!, null!, null!);

        Assert.Null(sut.MetadataHandler);
        Assert.Null(sut.PositionTracker);
    }

    [Fact]
    public void EventHandlerManagerCallbacks_delegates_invoke()
    {
        var sut = new EventHandlerManagerCallbacks(
            () => TimeSpan.FromSeconds(1),
            () => TimeSpan.FromSeconds(2),
            _ => { },
            _ => { },
            _ => { },
            () => null,
            () => null);

        Assert.Equal(TimeSpan.FromSeconds(1), sut.GetCurrentPosition());
        Assert.Equal(TimeSpan.FromSeconds(2), sut.GetDuration());
        Assert.Null(sut.GetMediaOpenedCompletionSource());
        Assert.Null(sut.GetCurrentTrack());
    }

    [Fact]
    public void PlaybackServiceInjectionContext_primary_ctor_round_trips()
    {
        var sut = new PlaybackServiceInjectionContext(
            null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.PlaylistService);
        Assert.Null(sut.DefaultDeviceRingtoneService);
    }

    [Fact]
    public void PlaylistServiceDeps_primary_ctor_round_trips()
    {
        var sut = new PlaylistServiceDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.ScheduleDisplayNameService);
    }

    [Fact]
    public void ScheduleListItemViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new ScheduleListItemViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.CategoryNameService);
        Assert.Null(sut.ScheduleStateService);
    }

    [Fact]
    public void MusicStateChangeHandlerServices_primary_ctor_round_trips()
    {
        var sut = new MusicStateChangeHandlerServices(null!, null!, null!, null!, null!);

        Assert.Null(sut.Mapper);
        Assert.Null(sut.ServiceProvider);
    }

    [Fact]
    public void MusicStateChangeHandlerCollaborators_primary_ctor_round_trips()
    {
        var sut = new MusicStateChangeHandlerCollaborators(null!, null!, null!);

        Assert.Null(sut.StateTracker);
        Assert.Null(sut.DisplayTextProvider);
    }

    [Fact]
    public void MusicStateHolder_exposes_mutable_music_flags()
    {
        var sut = new MusicStateHolder
        {
            MusicUpdated = true,
            IsUpdatingFromState = true,
            PendingMusicEnabled = false,
        };

        Assert.True(sut.MusicUpdated);
        Assert.True(sut.IsUpdatingFromState);
        Assert.False(sut.PendingMusicEnabled!.Value);
    }

    [Fact]
    public void LoadMusicForSelectionArgs_round_trips()
    {
        var sut = new LoadMusicForSelectionArgs(3, true, null, "p", "lc", "tc", false);

        Assert.Equal(3, sut.ScheduleId);
        Assert.True(sut.IsNewSchedule);
        Assert.Equal("p", sut.PublicationCode);
        Assert.Equal("lc", sut.LanguageCode);
        Assert.Equal("tc", sut.TrackCode);
        Assert.False(sut.Repeat!.Value);
    }

    [Fact]
    public void LoadBiblePublicationScheduleCodes_round_trips()
    {
        var sut = new LoadBiblePublicationScheduleCodes("en", "pub", "sec", "trk");

        Assert.Equal("en", sut.LanguageCode);
        Assert.Equal("sec", sut.SectionCode);
    }

    [Fact]
    public void LoadBiblePublicationForSelectionArgs_round_trips()
    {
        var codes = new LoadBiblePublicationScheduleCodes(null, "pc", null, "tc");
        var sut = new LoadBiblePublicationForSelectionArgs(9, false, null, codes, TimeSpan.FromMinutes(2));

        Assert.Equal(9, sut.ScheduleId);
        Assert.Same(codes, sut.Codes);
        Assert.Equal(TimeSpan.FromMinutes(2), sut.FinishedDuration);
    }

    [Fact]
    public void ScheduleViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new ScheduleViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.Logger);
        Assert.Null(sut.ScheduleStateChangeHandler);
    }

    [Fact]
    public void MusicPublicationSelectionViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new MusicPublicationSelectionViewModelDeps(
            null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.MediaService);
        Assert.Null(sut.ServiceProvider);
    }

    [Fact]
    public void MusicSelectionContainerViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new MusicSelectionContainerViewModelDeps(
            null!, null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.ToastService);
        Assert.Null(sut.ScheduleSelectionService);
    }

    [Fact]
    public void BiblePublicationSelectionViewModelDeps_primary_ctor_round_trips()
    {
        var sut = new BiblePublicationSelectionViewModelDeps(
            null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.BiblePublicationService);
        Assert.Null(sut.NavigationService);
    }

    [Fact]
    public void ScheduleStatePopulatorDeps_primary_ctor_round_trips()
    {
        var sut = new ScheduleStatePopulatorDeps(null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.ScopeFactory);
        Assert.Null(sut.Mapper);
    }

    [Fact]
    public void ScheduleStateServiceDeps_primary_ctor_round_trips()
    {
        var sut = new ScheduleStateServiceDeps(
            null!, null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.AlarmService);
        Assert.Null(sut.ServiceProvider);
    }

    [Fact]
    public void BootstrapOrchestratorDeps_primary_ctor_round_trips()
    {
        var sut = new BootstrapOrchestratorDeps(null!, null!, null!, null!, null!, null!, null!);

        Assert.Null(sut.PlatformBootstrapService);
        Assert.Null(sut.LanguageNameService);
        Assert.Null(sut.CategoryNameService);
    }

    [Fact]
    public void ScheduleEffectsOptionalDeps_default_ctor_sets_nulls()
    {
        var sut = new ScheduleEffectsOptionalDeps();

        Assert.Null(sut.BiblePublicationService);
        Assert.Null(sut.State);
    }

    [Fact]
    public void MusicSectionSelectionStateChangeHandlerCallbacks_invoke_Initialize()
    {
        var initLen = 0;
        var sut = new MusicSectionSelectionStateChangeHandlerCallbacks(
            _ => { },
            _ => { },
            () => true,
            _ => { },
            s => initLen = s.Length,
            () => { });

        sut.Initialize("xy");

        Assert.Equal(2, initLen);
    }

    [Fact]
    public void PlaybackMediaEventAdapterCallbacks_invoke_Getters()
    {
        var sut = new PlaybackMediaEventAdapterCallbacks(
            () => null,
            () => 3,
            _ => { },
            () => 9,
            () => true,
            () => Task.FromResult(true),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => false,
            () => true,
            (_, _) => Task.CompletedTask,
            _ => true);

        Assert.Null(sut.GetPlaylist());
        Assert.Equal(3, sut.GetCurrentTrackIndex());
        Assert.Equal(9, sut.GetCurrentScheduleId());
        Assert.True(sut.GetIsIndefinitePlayback());
        Assert.True(sut.GetIsAlarm());
        Assert.True(sut.IsPlaybackEstablishedForTrack(0));
    }

    [Fact]
    public void PlaybackMediaFailedRequest_round_trips_GetCurrentTrackIndex()
    {
        var sut = new PlaybackMediaFailedRequest(
            null,
            () => 7,
            "u",
            "full",
            _ => Task.CompletedTask,
            () => true,
            () => false,
            (_, _) => Task.CompletedTask,
            _ => false);

        Assert.Equal(7, sut.GetCurrentTrackIndex());
        Assert.Equal("u", sut.TrackUri);
    }

    [Fact]
    public void PlaybackNavigationNextRequest_holds_manually_visited_set()
    {
        var visited = new HashSet<int> { 1 };
        var sut = new PlaybackNavigationNextRequest(
            null,
            () => 0,
            _ => { },
            5,
            true,
            () => Task.FromResult(false),
            visited,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => Task.CompletedTask,
            () => Task.CompletedTask);

        Assert.Same(visited, sut.ManuallyVisitedTrackIndices);
        Assert.Equal(5, sut.CurrentScheduleId);
        Assert.True(sut.IsIndefinitePlayback);
    }

    [Fact]
    public void PlaybackNavigationPreviousRequest_holds_manually_visited_set()
    {
        var visited = new HashSet<int>();
        var sut = new PlaybackNavigationPreviousRequest(
            null,
            () => 2,
            _ => { },
            null,
            false,
            () => Task.FromResult(true),
            visited,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => Task.CompletedTask);

        Assert.Same(visited, sut.ManuallyVisitedTrackIndices);
        Assert.Equal(2, sut.GetCurrentTrackIndex());
    }

    [Fact]
    public void PlaybackStopRequest_ResetState_invokes_action()
    {
        var timerStopped = false;
        var resetCount = 0;
        var meta = new TrackMetadata { ScheduleId = 1 };
        var cts = new CancellationTokenSource();
        var sut = new PlaybackStopRequest(
            10,
            meta,
            true,
            false,
            cts,
            () => resetCount++,
            () => timerStopped = true);

        sut.ResetState();
        sut.StopProgressTimer();

        Assert.Equal(1, resetCount);
        Assert.True(timerStopped);
        Assert.True(sut.SkipMarkAsPlayed);
        Assert.Same(meta, sut.TrackMetadataToMark);
    }

    [Fact]
    public void PlaybackPrepareFallbackRequest_SetPlaylist_invokes_delegate()
    {
        var setPlaylistCalls = 0;
        var sut = new PlaybackPrepareFallbackRequest(
            42,
            false,
            _ => setPlaylistCalls++,
            _ => { },
            () => { },
            (_, _) => { },
            _ => Task.CompletedTask);

        sut.SetPlaylist([]);

        Assert.Equal(1, setPlaylistCalls);
        Assert.Equal(42, sut.ScheduleId);
    }

    [Fact]
    public async Task PlaybackHandleFailureRequest_ResetAsync_invokes()
    {
        var resetCalls = 0;
        var sut = new PlaybackHandleFailureRequest(
            true,
            8,
            () =>
            {
                resetCalls++;
                return Task.CompletedTask;
            },
            _ => { },
            _ => { },
            _ => Task.CompletedTask);

        await sut.ResetAsync();

        Assert.Equal(1, resetCalls);
        Assert.True(sut.IsAlarm);
        Assert.Equal(8, sut.CurrentScheduleId);
    }

    [Fact]
    public void PlaybackMediaEndedRequest_GetCurrentTrackIndex_round_trips()
    {
        var sut = new PlaybackMediaEndedRequest(
            null,
            () => 11,
            _ => { },
            3,
            true,
            () => Task.FromResult(false),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            () => false,
            () => true,
            (_, _) => Task.CompletedTask);

        Assert.Equal(11, sut.GetCurrentTrackIndex());
        Assert.Equal(3, sut.CurrentScheduleId);
        Assert.True(sut.IsIndefinitePlayback);
    }

    [Fact]
    public void PlaybackTrackTransitionEndedAction_instantiates()
    {
        var sut = new PlaybackTrackTransitionEndedAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void PlaybackTrackTransitionStartedAction_instantiates()
    {
        var sut = new PlaybackTrackTransitionStartedAction();
        Assert.NotNull(sut);
    }

    [Fact]
    public void TrackSelectionAction_exposes_state_item()
    {
        var item = new BiblePublicationStateItem { Id = 77, PublicationCode = "p" };
        var sut = new TrackSelectionAction(item);

        Assert.Same(item, sut.CurrentBiblePublicationSchedule);
    }

    [Fact]
    public void UpdateDraftScheduleAction_exposes_schedule()
    {
        var sched = new ScheduleStateItem { Id = 5, Name = "n" };
        var sut = new UpdateDraftScheduleAction(sched);

        Assert.Same(sched, sut.Schedule);
    }

    [Fact]
    public void UpdateScheduleLastPlayedAction_exposes_fields()
    {
        var t = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var sut = new UpdateScheduleLastPlayedAction(99, t);

        Assert.Equal(99, sut.ScheduleId);
        Assert.Equal(t, sut.LastPlayedAtUtc);
    }

    [Fact]
    public void ViewExistingScheduleAction_exposes_fields()
    {
        var sut = new ViewExistingScheduleAction(12, isEnabled: false);

        Assert.Equal(12, sut.ScheduleId);
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public void InitializeAction_null_scheduleList_uses_empty_hash_set()
    {
        var sut = new InitializeAction(null!);

        Assert.NotNull(sut.ScheduleList);
        Assert.Empty(sut.ScheduleList);
    }

    [Fact]
    public void InitializeAction_preserves_passed_schedule_list()
    {
        var list = new ObservableHashSet<ScheduleStateItem>();
        var sut = new InitializeAction(list);

        Assert.Same(list, sut.ScheduleList);
    }

    [Fact]
    public void CategorySelectionAutoPopulateHandlerDeps_primary_ctor_round_trips()
    {
        var logger = TestLogging.CreateLogger();
        var sut = new CategorySelectionAutoPopulateHandlerDeps(
            null!, null!, null!, null!, null!, null!, null!, logger);

        Assert.Null(sut.BiblePublicationService);
        Assert.Same(logger, sut.Logger);
    }
}
