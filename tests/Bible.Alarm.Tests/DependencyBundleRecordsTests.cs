#nullable enable

using AutoMapper;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Services.Media.AudioPlayerHelpers;
using Bible.Alarm.ViewModels;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.Tests.Support;
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
}
