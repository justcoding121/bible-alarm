#nullable enable

using Bible.Alarm.Common.ViewHelpers;

namespace Bible.Alarm.Tests;

public sealed class PlatformShadowExtensionTests
{
    [Fact]
    public void ProvideValue_returns_null_on_Windows_host()
    {
        var sut = new PlatformShadowExtension
        {
            Brush = "#FF001122",
            Offset = "2,4",
            Radius = "6",
            Opacity = "0.5",
        };

        var shadow = sut.ProvideValue(StubServiceProvider.Instance);

        Assert.Null(shadow);
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        public static readonly StubServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
