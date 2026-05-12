#nullable enable

using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class MusicPublicationSelectionActionTests
{
    [Fact]
    public void Constructor_sets_CurrentMusic()
    {
        var music = new MusicStateItem { Id = 9, PublicationCode = "voc" };

        var sut = new MusicPublicationSelectionAction(music);

        Assert.Same(music, sut.CurrentMusic);
    }
}
