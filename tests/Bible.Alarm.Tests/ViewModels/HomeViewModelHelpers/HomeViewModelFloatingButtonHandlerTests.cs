#nullable enable

using Bible.Alarm.Services.Battery.Interfaces;
using Bible.Alarm.Tests.Support;
using Bible.Alarm.ViewModels.HomeViewModelHelpers;
using Microsoft.Maui.Devices;

namespace Bible.Alarm.Tests;

public sealed class HomeViewModelFloatingButtonHandlerTests
{
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class FixedServiceProvider(object? service) : IServiceProvider
    {
        public object? GetService(Type serviceType) => service;
    }

    private sealed class FullyGrantedBatteryService : IBatteryOptimizationService
    {
        public void Dispose() { }

        public Task MarkModalAsShownAsync() => Task.CompletedTask;

        public Task<bool> ShouldShowModalAsync() => Task.FromResult(false);

        public void ShowOptimizationSettingsPage() { }

        public bool CanShowOptimizeActivity() => false;

        public void ShowDoNotDisturbSettingsPage() { }

        public bool IsIgnoringBatteryOptimizations() => true;

        public bool IsNotificationPolicyAccessGranted() => true;
    }

    private sealed class ThrowingBatteryService : IBatteryOptimizationService
    {
        public void Dispose() { }

        public Task MarkModalAsShownAsync() => Task.CompletedTask;

        public Task<bool> ShouldShowModalAsync() => Task.FromResult(false);

        public void ShowOptimizationSettingsPage() { }

        public bool CanShowOptimizeActivity() => false;

        public void ShowDoNotDisturbSettingsPage() { }

        public bool IsIgnoringBatteryOptimizations() => throw new InvalidOperationException("battery");

        public bool IsNotificationPolicyAccessGranted() => throw new InvalidOperationException("dnd");
    }

    [Fact]
    public void ComputeFloatingButtonVisible_follows_platform_rules()
    {
        var logger = TestLogging.CreateLogger();
        var sut = new HomeViewModelFloatingButtonHandler(logger, new EmptyServiceProvider());

        var visible = sut.ComputeFloatingButtonVisible();

        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            // Android: handler may show the battery-permission nudge when optimization service isn't wired.
            Assert.True(visible);
        }
        else
        {
            Assert.False(visible);
        }
    }

    [Fact]
    public void ComputeFloatingButtonVisible_non_android_always_false()
    {
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return;
        }

        var sut = new HomeViewModelFloatingButtonHandler(
            TestLogging.CreateLogger(),
            new FixedServiceProvider(new FullyGrantedBatteryService()));

        Assert.False(sut.ComputeFloatingButtonVisible());
    }

    [Fact]
    public void ComputeFloatingButtonVisible_android_hides_when_battery_and_dnd_granted()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        var sut = new HomeViewModelFloatingButtonHandler(
            TestLogging.CreateLogger(),
            new FixedServiceProvider(new FullyGrantedBatteryService()));

        Assert.False(sut.ComputeFloatingButtonVisible());
    }

    [Fact]
    public void ComputeFloatingButtonVisible_android_returns_true_when_service_throws()
    {
        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return;
        }

        var sut = new HomeViewModelFloatingButtonHandler(
            TestLogging.CreateLogger(),
            new FixedServiceProvider(new ThrowingBatteryService()));

        Assert.True(sut.ComputeFloatingButtonVisible());
    }
}
