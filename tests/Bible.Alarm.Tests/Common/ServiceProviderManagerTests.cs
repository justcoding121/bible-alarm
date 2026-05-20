#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Tests.Support;
using Serilog;

namespace Bible.Alarm.Tests;

public sealed class ServiceProviderManagerTests
{
    [Fact]
    public void GetService_throws_when_MauiApp_is_not_initialized()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return;
        }

        Assert.Throws<InvalidOperationException>(() => ServiceProviderManager.GetService<object>());
    }

    [Collection("MauiUi")]
    public sealed class MauiServiceProviderTests(MauiUiFixture _)
    {
        [Fact]
        public void GetService_resolves_registered_services_when_maui_ready()
        {
            if (!MauiUiTestBootstrap.IsReady)
            {
                return;
            }

            var logger = ServiceProviderManager.GetService<ILogger>();

            Assert.NotNull(logger);
        }
    }
}
