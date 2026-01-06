#nullable enable

using System.Collections.Generic;
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
            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Error("Failed to create toast notifier for schedule {ScheduleId}. App may not be properly registered for notifications.", scheduleId);
                return Task.CompletedTask;
            }

            // Remove all existing notifications for this schedule before rescheduling
            RemoveAllNotificationsForSchedule(notifier, scheduleId);

            // Schedule notifications for the next 90 days
            const int daysToSchedule = 90;
            var maxDate = DateTimeOffset.Now.AddDays(daysToSchedule);
            var currentDate = DateTimeOffset.Now;
            var scheduledCount = 0;
            const int maxOccurrences = 1000; // Safety limit to prevent infinite loops

            logger.Information("Scheduling notifications for schedule {ScheduleId} for the next {Days} days", scheduleId, daysToSchedule);

            for (int i = 0; i < maxOccurrences; i++)
            {
                var fireDate = schedule.NextFireDate(currentDate);

                // Stop if beyond our 90-day window
                if (fireDate > maxDate)
                {
                    break;
                }

                // Skip if in the past (shouldn't happen, but safety check)
                if (fireDate <= DateTimeOffset.Now)
                {
                    currentDate = fireDate;
                    continue;
                }

                // Create unique ID for this occurrence: "{scheduleId}_{ticks}"
                var uniqueId = $"{scheduleId}_{fireDate.Ticks}";
                var toast = CreateScheduledToast(uniqueId, scheduleId, title, body, fireDate);
                
                try
                {
                    notifier.AddToSchedule(toast);
                    scheduledCount++;
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to schedule notification for schedule {ScheduleId} at {FireDate}", scheduleId, fireDate);
                    // Continue with next occurrence
                }

                currentDate = fireDate;
            }

            if (scheduledCount > 0)
            {
                logger.Information("Successfully scheduled {Count} notifications for schedule {ScheduleId} (next {Days} days)", scheduledCount, scheduleId, daysToSchedule);
            }
            else
            {
                logger.Warning("No notifications were scheduled for schedule {ScheduleId}. Check alarm schedule configuration.", scheduleId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error scheduling notifications for schedule {ScheduleId}", schedule.Id);
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

            // Remove all notifications for this schedule (supports multiple occurrences)
            var removedCount = RemoveAllNotificationsForSchedule(notifier, scheduleId);
            if (removedCount > 0)
            {
                logger.Information("Removed {Count} scheduled notifications for schedule {ScheduleId}", removedCount, scheduleId);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error removing notifications for schedule {ScheduleId}", scheduleId);
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

    /// <summary>
    /// Shows a rich toast notification with media metadata (artwork, title, subtitle, album).
    /// Uses ToastGeneric template which supports images, multiple text elements, and action buttons.
    /// </summary>
    public void ShowMediaToast(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext = false, bool canPlayPrevious = false, bool isPlaying = false)
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Debug("Cannot show media toast - toast notifier unavailable");
                return;
            }

            var toastXml = CreateMediaToastXml(title, subtitle, body, artworkUrl, canPlayNext, canPlayPrevious, isPlaying);
            var toast = new ToastNotification(toastXml);
            
            // Use a unique tag so we can replace previous toasts
            toast.Tag = "MediaPlayback";
            toast.Group = "MediaPlayback";
            
            // Suppress sound for media playback toasts (optional - remove if you want sound)
            toast.SuppressPopup = false;
            
            notifier.Show(toast);
            logger.Debug("Media toast shown: Title={Title}, Subtitle={Subtitle}, CanPlayNext={CanPlayNext}, CanPlayPrevious={CanPlayPrevious}, IsPlaying={IsPlaying}", 
                title, subtitle, canPlayNext, canPlayPrevious, isPlaying);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing media toast notification");
        }
    }

    private static XmlDocument CreateMediaToastXml(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext, bool canPlayPrevious, bool isPlaying)
    {
        // Use ToastGeneric template which supports images and rich content
        var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastGeneric);
        
        var visual = toastXml.SelectSingleNode("/toast/visual");
        if (visual == null)
        {
            return toastXml;
        }

        // Add binding element
        var binding = toastXml.CreateElement("binding");
        binding.SetAttribute("template", "ToastGeneric");
        visual.AppendChild(binding);

        // Add title text
        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleElement = toastXml.CreateElement("text");
            titleElement.SetAttribute("hint-style", "title");
            titleElement.AppendChild(toastXml.CreateTextNode(title));
            binding.AppendChild(titleElement);
        }

        // Add subtitle text
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleElement = toastXml.CreateElement("text");
            subtitleElement.SetAttribute("hint-style", "subtitle");
            subtitleElement.AppendChild(toastXml.CreateTextNode(subtitle));
            binding.AppendChild(subtitleElement);
        }

        // Add body text (album)
        if (!string.IsNullOrWhiteSpace(body))
        {
            var bodyElement = toastXml.CreateElement("text");
            bodyElement.AppendChild(toastXml.CreateTextNode(body));
            binding.AppendChild(bodyElement);
        }

        // Add hero image (artwork) if available
        if (!string.IsNullOrWhiteSpace(artworkUrl))
        {
            try
            {
                // Convert local file path to proper URI for Windows toast
                var imageUri = artworkUrl;
                
                // If it's already a URI (http/https), use it as-is
                if (Uri.TryCreate(artworkUrl, UriKind.Absolute, out var uri) && 
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    imageUri = artworkUrl;
                }
                else
                {
                    // For local files, convert to file:// URI
                    if (System.IO.File.Exists(artworkUrl))
                    {
                        // Use absolute path with file:// scheme
                        var absolutePath = System.IO.Path.GetFullPath(artworkUrl);
                        // Windows toast requires file:/// (three slashes) for local files
                        imageUri = new Uri(absolutePath).ToString();
                    }
                    else
                    {
                        // If file doesn't exist, try to construct URI anyway
                        var absolutePath = System.IO.Path.GetFullPath(artworkUrl);
                        imageUri = new Uri(absolutePath).ToString();
                    }
                }

                var imageElement = toastXml.CreateElement("image");
                imageElement.SetAttribute("placement", "hero");
                imageElement.SetAttribute("src", imageUri);
                binding.AppendChild(imageElement);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to add artwork image to toast: {ArtworkUrl}", artworkUrl);
            }
        }

        // Get toast node for adding actions and duration
        var toastNode = toastXml.SelectSingleNode("/toast");
        
        // Add action buttons for media controls
        if (toastNode != null)
        {
            var actions = toastXml.CreateElement("actions");
            toastNode.AppendChild(actions);

            // Previous button
            if (canPlayPrevious)
            {
                var previousAction = toastXml.CreateElement("action");
                previousAction.SetAttribute("content", "Previous");
                previousAction.SetAttribute("arguments", "action=previous");
                previousAction.SetAttribute("activationType", "foreground");
                actions.AppendChild(previousAction);
            }

            // Play/Pause button (show appropriate button based on current state)
            if (isPlaying)
            {
                var pauseAction = toastXml.CreateElement("action");
                pauseAction.SetAttribute("content", "Pause");
                pauseAction.SetAttribute("arguments", "action=pause");
                pauseAction.SetAttribute("activationType", "foreground");
                actions.AppendChild(pauseAction);
            }
            else
            {
                var playAction = toastXml.CreateElement("action");
                playAction.SetAttribute("content", "Play");
                playAction.SetAttribute("arguments", "action=play");
                playAction.SetAttribute("activationType", "foreground");
                actions.AppendChild(playAction);
            }

            // Next button
            if (canPlayNext)
            {
                var nextAction = toastXml.CreateElement("action");
                nextAction.SetAttribute("content", "Next");
                nextAction.SetAttribute("arguments", "action=next");
                nextAction.SetAttribute("activationType", "foreground");
                actions.AppendChild(nextAction);
            }
        }

        // Set toast duration to long (optional - can be removed if you want short duration)
        if (toastNode?.Attributes != null)
        {
            var durationAttribute = toastXml.CreateAttribute("duration");
            durationAttribute.Value = "long";
            toastNode.Attributes.SetNamedItem(durationAttribute);
        }

        return toastXml;
    }

    private static ScheduledToastNotification CreateScheduledToast(string uniqueId, int scheduleId, string title, string body, DateTimeOffset time)
    {
        var toastXml = CreateToastXml(title, body, scheduleId);
        return new ScheduledToastNotification(toastXml, time)
        {
            Id = uniqueId
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
        // Check if any notification exists for this schedule
        // Notification IDs are in format: "{scheduleId}_{ticks}"
        var scheduledToasts = notifier.GetScheduledToastNotifications();
        var scheduleIdPrefix = $"{scheduleId}_";
        foreach (var toast in scheduledToasts)
        {
            // Check if notification ID starts with scheduleId (supports both old format and new format)
            if (toast.Id == scheduleId.ToString() || toast.Id.StartsWith(scheduleIdPrefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes all scheduled notifications for a given schedule ID.
    /// Supports both old format (just scheduleId) and new format ({scheduleId}_{ticks}).
    /// </summary>
    private static int RemoveAllNotificationsForSchedule(ToastNotifier notifier, int scheduleId)
    {
        var scheduledToasts = notifier.GetScheduledToastNotifications();
        var scheduleIdString = scheduleId.ToString();
        var scheduleIdPrefix = $"{scheduleId}_";
        var toRemove = new List<ScheduledToastNotification>();

        // Find all notifications for this schedule
        foreach (var toast in scheduledToasts)
        {
            // Support both old format (just scheduleId) and new format ({scheduleId}_{ticks})
            if (toast.Id == scheduleIdString || toast.Id.StartsWith(scheduleIdPrefix, StringComparison.Ordinal))
            {
                toRemove.Add(toast);
            }
        }

        // Remove all found notifications
        foreach (var toast in toRemove)
        {
            try
            {
                notifier.RemoveFromSchedule(toast);
            }
            catch (Exception ex)
            {
                // Log but continue removing others
                Log.Warning(ex, "Failed to remove scheduled notification {NotificationId} for schedule {ScheduleId}", toast.Id, scheduleId);
            }
        }

        return toRemove.Count;
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
