#nullable enable

using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;

namespace Bible.Alarm.Tests;

public sealed class MusicSectionSelectedActionTests
{
    [Fact]
    public void Constructor_sets_CurrentMusic()
    {
        var music = new MusicStateItem { Id = 11, SectionCode = "s1" };

        var sut = new MusicSectionSelectedAction(music);

        Assert.Same(music, sut.CurrentMusic);
    }
}
