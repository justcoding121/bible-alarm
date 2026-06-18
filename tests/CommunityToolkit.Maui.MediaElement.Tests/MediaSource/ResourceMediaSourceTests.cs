#nullable enable

using CommunityToolkit.Maui.MediaElement.Tests.Support;
using ResourceMediaSource = CommunityToolkit.Maui.MediaSource.ResourceMediaSource;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class ResourceMediaSourceTests
{
    public ResourceMediaSourceTests() => MediaElementTestBootstrap.TryInitialize();

    [Fact]
    public void Implicit_string_operator_creates_source_with_resource_path()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        ResourceMediaSource source = "audio/default.mp3";

        Assert.Equal("audio/default.mp3", source.Path);
    }

    [Fact]
    public void Implicit_string_conversion_round_trips_path()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        ResourceMediaSource source = new() { Path = "audio/default.mp3" };

        string? text = source;

        Assert.Equal("audio/default.mp3", text);
    }

    [Fact]
    public void ToString_includes_resource_path()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var source = new ResourceMediaSource { Path = "audio/default.mp3" };

        Assert.Equal("Resource: audio/default.mp3", source.ToString());
    }
}
