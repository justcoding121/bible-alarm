#nullable enable
using Bible.Alarm.Services.Battery.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.ViewModels.HomeViewModelHelpers;

/// <summary>
/// Handles battery optimization floating button visibility for HomeViewModel (Android only).
/// </summary>
public sealed class HomeViewModelFloatingButtonHandler
{
    private readonly ILogger logger;
    private readonly IServiceProvider serviceProvider;

    public HomeViewModelFloatingButtonHandler(ILogger logger, IServiceProvider serviceProvider)
    {
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    public bool ComputeFloatingButtonVisible()
    {
        logger.Information("[FLOATING-BUTTON] UpdateFloatingButtonVisibility called - Platform={Platform}", DeviceInfo.Platform);

        if (DeviceInfo.Platform != DevicePlatform.Android)
        {
            return false;
        }

        try
        {
            var batteryService = serviceProvider.GetService<IBatteryOptimizationService>();
            if (batteryService == null)
            {
                return true;
            }

            var isBatteryExcluded = batteryService.IsIgnoringBatteryOptimizations();
            var isDndGranted = batteryService.IsNotificationPolicyAccessGranted();
            return !(isBatteryExcluded && isDndGranted);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating floating button visibility");
            return true;
        }
    }
}
