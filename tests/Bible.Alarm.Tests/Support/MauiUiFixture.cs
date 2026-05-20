#nullable enable

namespace Bible.Alarm.Tests.Support;

public sealed class MauiUiFixture
{
    public MauiUiFixture()
    {
        MauiUiTestBootstrap.TryInitialize();
    }
}
