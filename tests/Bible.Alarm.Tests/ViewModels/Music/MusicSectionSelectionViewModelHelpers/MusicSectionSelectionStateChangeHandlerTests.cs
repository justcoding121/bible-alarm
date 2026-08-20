#nullable enable

using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Models;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.Music.MusicSectionSelectionViewModelHelpers;
using Xunit;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectionStateChangeHandlerTests
{
    [Fact]
    public void HandleStateChanged_returns_when_current_schedule_null()
    {
        var initCalls = 0;
        var sut = new MusicSectionSelectionStateChangeHandler(
            TestLogging.CreateLogger(),
            new MusicSectionSelectionStateChangeHandler.Callbacks(
                _ => { },
                _ => { },
                () => false,
                _ => { },
                _ => initCalls++,
                () => { }));

        sut.HandleStateChanged(new ApplicationState());

        Assert.Equal(0, initCalls);
    }

    [Fact]
    public void HandleStateChanged_returns_when_music_publication_missing()
    {
        var initCalls = 0;
        var sut = new MusicSectionSelectionStateChangeHandler(
            TestLogging.CreateLogger(),
            new MusicSectionSelectionStateChangeHandler.Callbacks(
                _ => { },
                _ => { },
                () => false,
                _ => { },
                _ => initCalls++,
                () => { }));

        sut.HandleStateChanged(new ApplicationState
        {
            CurrentSchedule = new ScheduleStateItem { MusicPublicationCode = null }
        });

        Assert.Equal(0, initCalls);
    }
}
