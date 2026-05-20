#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Tests;

public sealed class Sha256Base36CompactHasherBibleAlarmTests
{
    [Fact]
    public void To11CharacterToken_returns_eleven_lowercase_base36_characters()
    {
        var dto = new DateTimeOffset(2030, 7, 15, 10, 30, 45, TimeSpan.Zero);

        var token = Sha256Base36CompactHasher.To11CharacterToken(dto);

        Assert.Equal(11, token.Length);
        Assert.Matches("^[0-9a-z]{11}$", token);
    }

    [Fact]
    public void To11CharacterToken_is_stable_for_same_instant()
    {
        var dto = new DateTimeOffset(2029, 1, 2, 3, 4, 5, TimeSpan.Zero);

        Assert.Equal(
            Sha256Base36CompactHasher.To11CharacterToken(dto),
            Sha256Base36CompactHasher.To11CharacterToken(dto));
    }

    [Fact]
    public void To11CharacterToken_differs_for_different_instants()
    {
        var early = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var later = early.AddMinutes(1);

        Assert.NotEqual(
            Sha256Base36CompactHasher.To11CharacterToken(early),
            Sha256Base36CompactHasher.To11CharacterToken(later));
    }

    [Fact]
    public void To11CharacterToken_covers_many_instants_and_always_pads_to_eleven_characters()
    {
        var anchor = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 500; i++)
        {
            var token = Sha256Base36CompactHasher.To11CharacterToken(anchor.AddMinutes(i));
            Assert.Equal(11, token.Length);
            Assert.Matches("^[0-9a-z]{11}$", token);
        }
    }

    [Fact]
    public void To11CharacterToken_uses_invariant_culture_timestamp_format()
    {
        var dto = new DateTimeOffset(1999, 12, 31, 23, 59, 59, TimeSpan.FromHours(5));

        var token = Sha256Base36CompactHasher.To11CharacterToken(dto);

        Assert.Equal(11, token.Length);
        Assert.NotEqual(new string('0', 11), token);
    }
}
