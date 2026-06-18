#nullable enable

using CommunityToolkit.Maui.MediaElement.Tests.Support;
using FileMediaSource = CommunityToolkit.Maui.MediaSource.FileMediaSource;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class FileMediaSourceTests
{
    public FileMediaSourceTests() => MediaElementTestBootstrap.TryInitialize();

    [Fact]
    public void Implicit_string_operator_creates_source_with_path()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        FileMediaSource source = @"D:\cache\alarm.mp3";

        Assert.Equal(@"D:\cache\alarm.mp3", source.Path);
    }

    [Fact]
    public void Implicit_string_conversion_round_trips_path()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        FileMediaSource source = new() { Path = @"D:\cache\alarm.mp3" };

        string? text = source;

        Assert.Equal(@"D:\cache\alarm.mp3", text);
    }

    [Fact]
    public void ToString_includes_path_value()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var source = new FileMediaSource { Path = @"C:\media\track.mp3" };

        Assert.Equal(@"File: C:\media\track.mp3", source.ToString());
    }
}
