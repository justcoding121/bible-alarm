#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.Services.Media.Playback;
using Bible.Alarm.Services.Scheduler;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
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
}
