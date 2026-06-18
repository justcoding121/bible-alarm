#nullable enable

using CommunityToolkit.Maui.Primitives;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class MediaFailedEventArgsTests
{
    [Fact]
    public void Constructor_sets_error_message()
    {
        var args = new MediaFailedEventArgs("network timeout");

        Assert.Equal("network timeout", args.ErrorMessage);
    }

    [Fact]
    public void ErrorMessage_is_read_only_after_construction()
    {
        var args = new MediaFailedEventArgs("codec unsupported");

        Assert.Equal("codec unsupported", args.ErrorMessage);
        Assert.IsAssignableFrom<EventArgs>(args);
    }
}
