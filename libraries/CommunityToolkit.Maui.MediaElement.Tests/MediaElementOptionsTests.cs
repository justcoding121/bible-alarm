using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Primitives;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public class MediaElementOptionsTests
{
    [Fact]
    public void SetDefaultAndroidViewType_DoesNotThrow()
    {
        MediaElementOptions.SetDefaultAndroidViewType(AndroidViewType.TextureView);
        MediaElementOptions.SetDefaultAndroidViewType(AndroidViewType.SurfaceView);
    }
}
