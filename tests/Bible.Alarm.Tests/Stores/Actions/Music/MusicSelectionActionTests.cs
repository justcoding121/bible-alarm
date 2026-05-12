#nullable enable

using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class MusicSelectionActionTests
{
    [Fact]
    public void Constructor_sets_CurrentMusic()
    {
        var music = new MusicStateItem { Id = 4, PublicationCode = "voc" };

        var sut = new MusicSelectionAction(music);

        Assert.Same(music, sut.CurrentMusic);
    }
}
