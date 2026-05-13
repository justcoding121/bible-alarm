#nullable enable

using Bible.Alarm.Services.Media;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DisplayMetadataServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new DisplayMetadataService(
            TestLogging.CreateLogger(),
            new IdleCatalogMediaService(),
            new HttpClientHandler());

        Assert.NotNull(sut);
    }
}
