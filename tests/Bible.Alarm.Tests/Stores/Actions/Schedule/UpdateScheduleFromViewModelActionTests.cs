#nullable enable

using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class UpdateScheduleFromViewModelActionTests
{
    [Fact]
    public void Constructor_defaults_flags_for_domain_updates_and_save()
    {
        var row = new ScheduleStateItem { Id = 1 };

        var sut = new UpdateScheduleFromViewModelAction(row);

        Assert.Same(row, sut.Schedule);
        Assert.True(sut.MusicUpdated);
        Assert.True(sut.BiblePublicationUpdated);
        Assert.True(sut.ShouldSave);
    }

    [Fact]
    public void Constructor_accepts_explicit_flags()
    {
        var row = new ScheduleStateItem { Id = 2 };

        var sut = new UpdateScheduleFromViewModelAction(
            row,
            musicUpdated: false,
            biblePublicationUpdated: false,
            shouldSave: false);

        Assert.False(sut.MusicUpdated);
        Assert.False(sut.BiblePublicationUpdated);
        Assert.False(sut.ShouldSave);
    }
}
