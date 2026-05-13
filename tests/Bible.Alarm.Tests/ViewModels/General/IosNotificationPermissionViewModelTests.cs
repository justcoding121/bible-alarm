#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.General;

namespace Bible.Alarm.Tests;

public sealed class IosNotificationPermissionViewModelTests
{
    [Fact]
    public void Ctor_initializes_on_non_ios_without_permission_probe()
    {
        IosNotificationPermissionViewModel? sut = null;
        try
        {
            sut = new IosNotificationPermissionViewModel(
                TestLogging.CreateLogger(),
                null!);
            Assert.NotNull(sut);
        }
        finally
        {
            sut?.Dispose();
        }
    }
}
