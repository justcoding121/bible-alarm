#nullable enable

using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class ToastMessageNormalizerTests
{
    [Fact]
    public void Normalize_returns_empty_for_null_or_whitespace_messages()
    {
        Assert.Equal(string.Empty, ToastMessageNormalizer.Normalize("   "));
        Assert.Equal(string.Empty, ToastMessageNormalizer.Normalize("\t\r\n"));
    }

    [Fact]
    public void Normalize_trims_outer_whitespace_when_content_present()
    {
        Assert.Equal("hello", ToastMessageNormalizer.Normalize("  hello  "));
    }
}
