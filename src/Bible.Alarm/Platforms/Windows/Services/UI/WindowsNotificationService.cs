#nullable enable

using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Platforms.Windows.Helpers;
using Bible.Alarm.Platforms.Windows.Services.Handlers.Interfaces;
using Serilog;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

public sealed partial class WindowsNotificationService(IServiceProvider serviceProvider, ILogger logger) : INotificationService
{
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task ShowNotificationAsync(int scheduleId)
    {
        // Resolve WindowsAlarmHandler lazily to break circular dependency
        // WindowsNotificationService -> WindowsAlarmHandler -> IPlaybackService -> INotificationService
        var windowsAlarmHandler = serviceProvider.GetRequiredService<IWindowsAlarmHandler>();
        await windowsAlarmHandler.HandleAsync(scheduleId, true);
    }

    public Task ScheduleNotificationAsync(AlarmSchedule schedule,
        string title, string body)
    {
        try
        {
            var scheduleId = schedule.Id;
            var time = schedule.NextFireDate();

            if (time <= DateTimeOffset.Now)
            {
                logger.Warning("Cannot schedule notification for schedule {ScheduleId}: time {Time} is in the past", scheduleId, time);
                return Task.CompletedTask;
            }

            logger.Information("Scheduling notification for schedule {ScheduleId} at {Time}", scheduleId, time);

            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Error("Failed to create toast notifier for schedule {ScheduleId}. App may not be properly registered for notifications.", scheduleId);
                return Task.CompletedTask;
            }

            var toast = CreateScheduledToast(scheduleId, title, body, time);
            notifier.AddToSchedule(toast);

            if (IsNotificationScheduled(notifier, scheduleId))
            {
                logger.Information("Successfully scheduled notification for schedule {ScheduleId} at {Time}", scheduleId, time);
            }
            else
            {
                logger.Warning("Notification may not have been scheduled for schedule {ScheduleId}. Check Windows notification settings.", scheduleId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error scheduling notification for schedule {ScheduleId}", schedule.Id);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(int scheduleId)
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier is null)
            {
                return Task.CompletedTask;
            }

            var toRemove = FindScheduledToast(notifier, scheduleId);
            if (toRemove is not null)
            {
                notifier.RemoveFromSchedule(toRemove);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error removing notification for schedule {ScheduleId}", scheduleId);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsScheduledAsync(int scheduleId)
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier is null)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(IsNotificationScheduled(notifier, scheduleId));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error checking if notification is scheduled for schedule {ScheduleId}", scheduleId);
            return Task.FromResult(false);
        }
    }

    public Task<bool> CanScheduleAsync() => Task.FromResult(WindowsBootstrapHelper.IsBackgroundTaskEnabled);

    private static ScheduledToastNotification CreateScheduledToast(int scheduleId, string title, string body, DateTimeOffset time)
    {
        var toastXml = CreateToastXml(title, body, scheduleId);
        return new ScheduledToastNotification(toastXml, time)
        {
            Id = scheduleId.ToString()
        };
    }

    private static XmlDocument CreateToastXml(string title, string body, int scheduleId)
    {
        var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

        var textElements = toastXml.GetElementsByTagName("text");
        if (textElements.Length > 0)
        {
            textElements[0].AppendChild(toastXml.CreateTextNode(title));
        }

        if (textElements.Length > 1)
        {
            textElements[1].AppendChild(toastXml.CreateTextNode(body));
        }

        var toastNode = toastXml.SelectSingleNode("/toast");
        if (toastNode?.Attributes != null)
        {
            var launchAttribute = toastXml.CreateAttribute("launch");
            launchAttribute.Value = scheduleId.ToString();
            toastNode.Attributes.SetNamedItem(launchAttribute);
        }

        var audioNode = toastXml.CreateElement("audio");
        audioNode.SetAttribute("src", "ms-winsoundevent:Notification.Default");
        toastNode?.AppendChild(audioNode);

        return toastXml;
    }

    private static bool IsNotificationScheduled(ToastNotifier notifier, int scheduleId)
    {
        var scheduledToasts = notifier.GetScheduledToastNotifications();
        var scheduleIdString = scheduleId.ToString();
        foreach (var toast in scheduledToasts)
        {
            if (toast.Id == scheduleIdString)
            {
                return true;
            }
        }
        return false;
    }

    private static ScheduledToastNotification? FindScheduledToast(ToastNotifier notifier, int scheduleId)
    {
        var scheduledToasts = notifier.GetScheduledToastNotifications();
        var scheduleIdString = scheduleId.ToString();
        foreach (var toast in scheduledToasts)
        {
            if (toast.Id == scheduleIdString)
            {
                return toast;
            }
        }
        return null;
    }

    private static ToastNotifier? GetToastNotifier()
    {
        try
        {
            var notifier = TryCreateNotifierWithoutParameters();
            if (notifier is not null)
            {
                return notifier;
            }

            notifier = TryCreateNotifierWithAumid();
            if (notifier is not null)
            {
                return notifier;
            }

            Log.Warning(
                "Unable to create toast notifier. Scheduled notifications will not work. " +
                "This is common in debug mode or when the app is not properly registered for notifications. " +
                "Try running the app from an installed package instead of Visual Studio.");
        }
        catch (Exception ex)
        {
            // Catch any unexpected exceptions during notifier creation
            Log.Warning(ex, "Unexpected error creating toast notifier. Scheduled notifications will not work.");
        }

        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithoutParameters()
    {
        try
        {
            Log.Debug("Attempting to create toast notifier without parameters...");
            var notifier = ToastNotificationManager.CreateToastNotifier();
            if (notifier is not null)
            {
                Log.Debug("Successfully created toast notifier without parameters");
                return notifier;
            }
            Log.Warning("ToastNotificationManager.CreateToastNotifier() returned null");
        }
        catch (COMException ex)
        {
            // Check HResult - 0x80070490 = Element not found
            // This is common in debug mode or when app is not registered for notifications
            if (ex.HResult == unchecked((int)0x80070490))
            {
                // This is expected and handled gracefully - no need to log as error
                Log.Debug("Failed to create toast notifier without parameters (0x80070490 - Element not found). This is expected in debug mode. Trying with AUMID...");
            }
            else
            {
                // Catch any other COM exceptions
                Log.Debug(ex, "COMException creating toast notifier without parameters. HResult: 0x{HR:X8}. Trying with AUMID...", ex.HResult);
            }
        }
        catch (Exception ex)
        {
            // Catch any other exceptions - safely get HResult if available
            var hResult = 0;
            if (ex is COMException comEx)
            {
                hResult = comEx.HResult;
            }
            else
            {
                try
                {
                    hResult = ex.HResult;
                }
                catch
                {
                    // HResult not available on this exception type
                }
            }
            Log.Debug(ex, "Exception creating toast notifier without parameters. HResult: 0x{HR:X8}. Trying with AUMID...", hResult);
        }
        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithAumid()
    {
        try
        {
            var package = Package.Current;
            var packageId = package.Id;

            string[] aumidFormats =
            [
                $"{packageId.FamilyName}!App",
                packageId.FamilyName,
                packageId.Name,
            ];

            foreach (var aumid in aumidFormats)
            {
                var notifier = TryCreateNotifierWithAumid(aumid);
                if (notifier is not null)
                {
                    return notifier;
                }
            }

            Log.Warning(
                "Failed to create toast notifier with any AUMID format. " +
                "Package: {PackageName}, FamilyName: {FamilyName}, Publisher: {Publisher}",
                packageId.Name, packageId.FamilyName, packageId.Publisher);
        }
        catch (InvalidOperationException)
        {
            Log.Warning(
                "Package.Current is not available. This is expected in debug mode or unpackaged WinUI 3 apps. " +
                "Scheduled notifications require the app to be properly packaged and installed.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception while trying to create toast notifier with AUMID");
        }
        return null;
    }

    private static ToastNotifier? TryCreateNotifierWithAumid(string aumid)
    {
        try
        {
            Log.Debug("Trying to create toast notifier with AUMID: {AUMID}", aumid);
            var notifier = ToastNotificationManager.CreateToastNotifier(aumid);
            if (notifier is not null)
            {
                Log.Information("Successfully created toast notifier with AUMID: {AUMID}", aumid);
                return notifier;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to create toast notifier with AUMID '{AUMID}'. HResult: 0x{HR:X8}", aumid, ex.HResult);
        }
        return null;
    }
}
