#nullable enable

using CommunityToolkit.Maui.MediaElement.Tests.Support;
using UriMediaSource = CommunityToolkit.Maui.MediaSource.UriMediaSource;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class UriMediaSourceTests
{
    public UriMediaSourceTests() => MediaElementTestBootstrap.TryInitialize();

    [Fact]
    public void Implicit_string_operator_creates_source_with_absolute_uri()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        UriMediaSource? source = "https://example.com/media.mp3";

        Assert.NotNull(source);
        Assert.Equal("https://example.com/media.mp3", source.Uri?.ToString());
    }

    [Fact]
    public void Implicit_string_conversion_round_trips_uri()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        UriMediaSource source = new() { Uri = new Uri("https://example.com/a.mp3") };

        string? text = source;

        Assert.Equal("https://example.com/a.mp3", text);
    }

    [Fact]
    public void ToString_includes_uri_value()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var source = new UriMediaSource { Uri = new Uri("https://example.com/x.mp3") };

        Assert.Equal("Uri: https://example.com/x.mp3", source.ToString());
    }

    [Fact]
    public void Setting_relative_uri_throws_from_bindable_property_validator()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var source = new UriMediaSource();

        Assert.Throws<ArgumentException>(() =>
            source.Uri = new Uri("/relative", UriKind.Relative));
    }
}
