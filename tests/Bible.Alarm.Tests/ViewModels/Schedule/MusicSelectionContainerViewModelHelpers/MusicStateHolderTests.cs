#nullable enable

using Bible.Alarm.Shared.Models.Schedule;
using Bible.Alarm.ViewModels.Schedule.MusicSelectionContainerViewModelHelpers;

namespace Bible.Alarm.Tests;

public sealed class MusicStateHolderTests
{
    [Fact]
    public void Properties_round_trip()
    {
        var m1 = new AlarmMusic { Id = 1 };
        var m2 = new AlarmMusic { Id = 2 };
        var sut = new MusicStateHolder
        {
            Music = m1,
            LastMusic = m2,
            MusicUpdated = true,
            IsUpdatingFromState = true,
            IsMusicEnabledNotificationQueued = true,
            PendingMusicEnabled = false,
        };

        Assert.Same(m1, sut.Music);
        Assert.Same(m2, sut.LastMusic);
        Assert.True(sut.MusicUpdated);
        Assert.True(sut.IsUpdatingFromState);
        Assert.True(sut.IsMusicEnabledNotificationQueued);
        Assert.False(sut.PendingMusicEnabled);
    }
}
