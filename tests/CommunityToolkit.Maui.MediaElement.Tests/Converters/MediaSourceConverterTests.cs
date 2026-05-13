#nullable enable

using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Maui.Converters;

namespace CommunityToolkit.Maui.MediaElement.Tests;

/// <summary>
/// Type conversion tests that avoid constructing <see cref="CommunityToolkit.Maui.MediaSource.MediaSource"/>
/// subclasses (those require WinUI initialization in this test host).
/// </summary>
public sealed class MediaSourceConverterTests
{
    private readonly MediaSourceConverter sut = new();

    [Fact]
    public void ConvertFrom_returns_null_for_whitespace_only_string()
    {
        var result = sut.ConvertFrom(context: null, CultureInfo.InvariantCulture, "   ");

        Assert.Null(result);
    }

    [Fact]
    public void ConvertTo_throws_when_value_is_not_a_supported_media_source_instance()
    {
        Assert.Throws<ArgumentException>(() =>
            sut.ConvertTo(context: null, CultureInfo.InvariantCulture, new object(), typeof(string)));
    }
}
