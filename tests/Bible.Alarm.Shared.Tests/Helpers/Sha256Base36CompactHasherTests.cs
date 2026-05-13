#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class Sha256Base36CompactHasherTests
{
    [Fact]
    public void To11CharacterToken_returns_eleven_chars_and_stable_for_same_instant()
    {
        var dto = new DateTimeOffset(2035, 6, 7, 8, 9, 10, TimeSpan.Zero);

        var first = Sha256Base36CompactHasher.To11CharacterToken(dto);
        var second = Sha256Base36CompactHasher.To11CharacterToken(dto);

        Assert.Equal(11, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void To11CharacterToken_differs_when_wall_clock_seconds_change()
    {
        var early = new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var later = early.AddSeconds(1);

        Assert.NotEqual(
            Sha256Base36CompactHasher.To11CharacterToken(early),
            Sha256Base36CompactHasher.To11CharacterToken(later));
    }
}
