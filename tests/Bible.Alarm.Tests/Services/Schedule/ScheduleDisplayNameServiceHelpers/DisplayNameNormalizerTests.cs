#nullable enable

using Bible.Alarm.Services.Schedule.ScheduleDisplayNameServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class DisplayNameNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeTrackTitle_returns_null_for_missing_or_whitespace(string? raw)
    {
        Assert.Null(DisplayNameNormalizer.NormalizeTrackTitle(raw));
    }

    [Fact]
    public void NormalizeTrackTitle_trims_and_decodes_entities()
    {
        Assert.Equal("A & B", DisplayNameNormalizer.NormalizeTrackTitle("  A &amp; B  "));
    }

    [Fact]
    public void NormalizeTrackTitle_replaces_nbsp_then_returns_null_when_only_spaces_remain()
    {
        Assert.Null(DisplayNameNormalizer.NormalizeTrackTitle("\u00A0\u00A0"));
    }

    [Fact]
    public void NormalizeTrackTitle_returns_null_when_decode_yields_empty_after_trim()
    {
        Assert.Null(DisplayNameNormalizer.NormalizeTrackTitle("\u00A0"));
    }

    [Theory]
    [InlineData("&nbsp;")]
    [InlineData("&#160;")]
    [InlineData("  &nbsp;  ")]
    public void NormalizeTrackTitle_returns_null_when_nbsp_entity_trims_empty(string raw)
    {
        Assert.Null(DisplayNameNormalizer.NormalizeTrackTitle(raw));
    }
}
