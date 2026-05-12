#nullable enable

using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests.Stores.Actions.Music;

public sealed class TrackSelectedActionTests
{
    [Fact]
    public void Constructor_sets_CurrentMusic()
    {
        var music = new MusicStateItem { Id = 3, TrackCode = "1" };

        var sut = new TrackSelectedAction(music);

        Assert.Same(music, sut.CurrentMusic);
    }
}
