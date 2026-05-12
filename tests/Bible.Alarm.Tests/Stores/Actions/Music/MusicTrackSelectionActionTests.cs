#nullable enable

using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class MusicTrackSelectionActionTests
{
    [Fact]
    public void Constructor_sets_CurrentMusic()
    {
        var music = new MusicStateItem { Id = 12, TrackCode = "5" };

        var sut = new MusicTrackSelectionAction(music);

        Assert.Same(music, sut.CurrentMusic);
    }
}
