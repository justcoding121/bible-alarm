#nullable enable

using Bible.Alarm.Common;

namespace Bible.Alarm.Tests;

public sealed class ServiceProviderManagerTests
{
    [Fact]
    public void GetService_throws_when_MauiApp_is_not_initialized()
    {
        Assert.Throws<InvalidOperationException>(() => ServiceProviderManager.GetService<object>());
    }
}
