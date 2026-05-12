#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class CreateScheduleActionTests
{
    [Fact]
    public void Constructor_defaults_update_flags_to_true()
    {
        var row = new ScheduleStateItem { Id = 1 };

        var sut = new CreateScheduleAction(row);

        Assert.Same(row, sut.Schedule);
        Assert.True(sut.MusicUpdated);
        Assert.True(sut.BiblePublicationUpdated);
    }

    [Fact]
    public void Constructor_accepts_explicit_update_flags()
    {
        var row = new ScheduleStateItem { Id = 2 };

        var sut = new CreateScheduleAction(row, musicUpdated: false, biblePublicationUpdated: false);

        Assert.False(sut.MusicUpdated);
        Assert.False(sut.BiblePublicationUpdated);
    }
}
