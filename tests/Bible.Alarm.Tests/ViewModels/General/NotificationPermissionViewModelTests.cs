#nullable enable

using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.General;

namespace Bible.Alarm.Tests;

public sealed class NotificationPermissionViewModelTests
{
    [Fact]
    public void Ctor_initializes_on_desktop_without_platform_permission_service()
    {
        NotificationPermissionViewModel? sut = null;
        try
        {
            sut = new NotificationPermissionViewModel(
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
