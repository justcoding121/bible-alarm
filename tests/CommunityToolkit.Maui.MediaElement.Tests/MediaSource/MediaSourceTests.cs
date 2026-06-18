#nullable enable

using CommunityToolkit.Maui.MediaElement.Tests.Support;
using ToolkitMediaSource = CommunityToolkit.Maui.MediaSource.MediaSource;
using UriMediaSource = CommunityToolkit.Maui.MediaSource.UriMediaSource;
using FileMediaSource = CommunityToolkit.Maui.MediaSource.FileMediaSource;
using ResourceMediaSource = CommunityToolkit.Maui.MediaSource.ResourceMediaSource;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class MediaSourceTests
{
    public MediaSourceTests() => MediaElementTestBootstrap.TryInitialize();

    [Fact]
    public void FromUri_returns_null_for_null_uri()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        Assert.Null(ToolkitMediaSource.FromUri((Uri?)null));
    }

    [Fact]
    public void FromUri_throws_when_uri_is_relative()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        Assert.Throws<ArgumentException>(() => ToolkitMediaSource.FromUri(new Uri("/relative", UriKind.Relative)));
    }

    [Fact]
    public void FromUri_creates_uri_media_source_for_absolute_http_uri()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var uri = new Uri("https://example.com/track.mp3");
        var source = ToolkitMediaSource.FromUri(uri);

        var uriSource = Assert.IsType<UriMediaSource>(source);
        Assert.Equal(uri, uriSource.Uri);
    }

    [Fact]
    public void FromFile_creates_file_media_source_with_path()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var source = ToolkitMediaSource.FromFile(@"C:\media\track.mp3");

        var fileSource = Assert.IsType<FileMediaSource>(source);
        Assert.Equal(@"C:\media\track.mp3", fileSource.Path);
    }

    [Fact]
    public void FromResource_sets_path_for_windows_host()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var source = ToolkitMediaSource.FromResource("audio/sample.mp3");

        var resourceSource = Assert.IsType<ResourceMediaSource>(source);
        Assert.Equal("audio/sample.mp3", resourceSource.Path);
    }

    [Theory]
    [InlineData("https://example.com/a.mp3", typeof(UriMediaSource))]
    [InlineData(@"C:\temp\a.mp3", typeof(FileMediaSource))]
    public void Implicit_string_operator_selects_expected_source_type(string value, Type expectedType)
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        ToolkitMediaSource? source = value;

        Assert.NotNull(source);
        Assert.IsType(expectedType, source);
    }

    [Fact]
    public void Implicit_uri_operator_creates_uri_media_source()
    {
        if (!MediaElementTestBootstrap.IsReady)
        {
            return;
        }

        var uri = new Uri("https://cdn.example.com/audio.mp3");
        ToolkitMediaSource? source = uri;

        var uriSource = Assert.IsType<UriMediaSource>(source);
        Assert.Equal(uri, uriSource.Uri);
    }
}
