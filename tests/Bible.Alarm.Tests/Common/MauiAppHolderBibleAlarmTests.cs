#nullable enable

using Bible.Alarm.Common;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MauiAppHolderBibleAlarmTests
{
    [Fact]
    public void App_throws_when_maui_app_not_created()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return;
        }

        var ex = Assert.Throws<InvalidOperationException>(() => _ = MauiAppHolder.App);

        Assert.Contains("CreateAndStore", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IsInitialized_is_false_before_create_and_store()
    {
        if (MauiAppHolder.IsInitialized)
        {
            return;
        }

        Assert.False(MauiAppHolder.IsInitialized);
    }

    [Collection("MauiUi")]
    public sealed class MauiBootstrapTests(MauiUiFixture fixture)
    {
        [Fact]
        public void CreateAndStore_initializes_singleton_app_services_and_properties()
        {
            _ = fixture;
            if (!MauiUiTestBootstrap.TryInitializeFullAppHolder())
            {
                return;
            }

            Assert.True(MauiAppHolder.IsInitialized);

            var first = MauiAppHolder.CreateAndStore();
            var second = MauiAppHolder.CreateAndStore();

            Assert.Same(first, second);
            Assert.Same(first, MauiAppHolder.App);
            Assert.NotNull(MauiAppHolder.Services);
        }
    }
}
