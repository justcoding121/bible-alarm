#nullable enable

using Bible.Alarm.Common.Helpers;

namespace Bible.Alarm.Tests;

public sealed class ArtworkHelperTests
{
    [Fact]
    public void ArtworkLock_is_binary_semaphore_allowing_serial_artwork_operations()
    {
        Assert.NotNull(ArtworkHelper.ArtworkLock);
        Assert.Equal(1, ArtworkHelper.ArtworkLock.CurrentCount);

        Assert.True(ArtworkHelper.ArtworkLock.Wait(0));

        Assert.Equal(0, ArtworkHelper.ArtworkLock.CurrentCount);

        ArtworkHelper.ArtworkLock.Release();

        Assert.Equal(1, ArtworkHelper.ArtworkLock.CurrentCount);
    }
}
