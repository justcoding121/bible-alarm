#nullable enable

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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
            logger.Information("Scheduling notification for schedule {ScheduleId}. Title: {Title}, Body: {Body}", 
                scheduleId, title, body);
            
            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Error("Failed to create toast notifier for schedule {ScheduleId}. App may not be properly registered for notifications. " +
                    "Scheduled notifications require the app to be installed as an MSIX package.", scheduleId);
                return Task.CompletedTask;
            }
            
            logger.Information("Toast notifier created successfully for schedule {ScheduleId}", scheduleId);

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

                // Create unique ID for this occurrence: Windows has a 16-character limit for notification IDs
                // Use format: "{scheduleId}_{hash}" where hash is a short representation of the date/time
                // ScheduleId can be up to 4 digits (9999), so we have ~12 chars for the hash
                var dateHash = GetShortDateHash(fireDate);
                var uniqueId = $"{scheduleId}_{dateHash}";
                
                // Ensure ID doesn't exceed 16 characters (Windows limit)
                if (uniqueId.Length > 16)
                {
                    // If scheduleId is too long, truncate the hash
                    var maxHashLength = 16 - scheduleId.ToString().Length - 1; // -1 for underscore
                    dateHash = dateHash.Substring(0, Math.Min(maxHashLength, dateHash.Length));
                    uniqueId = $"{scheduleId}_{dateHash}";
                }
                
                var toast = CreateScheduledToast(uniqueId, scheduleId, title, body, fireDate);
                
                try
                {
                    notifier.AddToSchedule(toast);
                    scheduledCount++;
                    logger.Information("Successfully scheduled notification for schedule {ScheduleId} at {FireDate} (ID: {UniqueId})", 
                        scheduleId, fireDate, uniqueId);
                }
                catch (Exception ex)
                {
                    logger.Warning(ex, "Failed to schedule notification for schedule {ScheduleId} at {FireDate}. Error: {ErrorMessage}", 
                        scheduleId, fireDate, ex.Message);
                    // Continue with next occurrence
                }

                currentDate = fireDate;
            }

            if (scheduledCount > 0)
            {
                logger.Information("Successfully scheduled {Count} notifications for schedule {ScheduleId} (next {Days} days)", scheduledCount, scheduleId, daysToSchedule);
                
                // Verify the scheduled notifications are actually in the system
                var scheduledToasts = notifier.GetScheduledToastNotifications();
                var scheduleIdPrefix = $"{scheduleId}_";
                var verifiedCount = scheduledToasts.Count(t => t.Id == scheduleId.ToString() || t.Id.StartsWith(scheduleIdPrefix, StringComparison.Ordinal));
                logger.Information("Verified {VerifiedCount} scheduled notifications in system for schedule {ScheduleId}", verifiedCount, scheduleId);
            }
            else
            {
                logger.Warning("No notifications were scheduled for schedule {ScheduleId}. Check alarm schedule configuration.", scheduleId);
                
                // Log additional diagnostic information
                var scheduledToasts = notifier.GetScheduledToastNotifications();
                logger.Warning("Total scheduled toasts in system: {TotalCount}. Next fire date was: {NextFireDate}", 
                    scheduledToasts.Count, schedule.NextFireDate(DateTimeOffset.Now));
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
    /// Dismisses the media playback toast notification and removes it from the screen.
    /// </summary>
    public void DismissMediaToast()
    {
        try
        {
            var notifier = GetToastNotifier();
            if (notifier == null)
            {
                logger.Debug("Cannot dismiss media toast - toast notifier unavailable");
                return;
            }

            // Remove toast from history using tag and group
            // This removes it from both the action center and dismisses it from the screen if still visible
            try
            {
                ToastNotificationManager.History.Remove("MediaPlayback", "MediaPlayback");
                logger.Debug("Media toast dismissed and removed from screen");
            }
            catch (Exception ex)
            {
                // Toast might not exist in history (e.g., already dismissed or never shown)
                // This is normal and not an error
                logger.Debug(ex, "Toast not found in history (may already be dismissed)");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error dismissing media toast notification");
        }
    }

    /// <summary>
    /// Shows a rich toast notification with media metadata (artwork, title, subtitle, album).
    /// Uses ToastGeneric template which supports images, multiple text elements, and action buttons.
    /// Windows automatically replaces any existing toast with the same Tag and Group, so no need to dismiss first.
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
            
            // Use the same Tag and Group - Windows will automatically replace any existing toast with these values
            // This allows seamless updates when track changes or play/pause state changes
            toast.Tag = "MediaPlayback";
            toast.Group = "MediaPlayback";
            
            // Sound is suppressed via silent audio element in the toast XML
            toast.SuppressPopup = false;
            
            // Show the toast - Windows will automatically replace any existing toast with the same tag/group
            notifier.Show(toast);
            logger.Debug("Media toast shown/updated: Title={Title}, Subtitle={Subtitle}, ArtworkUrl={ArtworkUrl}. Clicking toast will activate existing app instance.", 
                title, subtitle, artworkUrl);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error showing media toast notification");
        }
    }

    private static XmlDocument CreateMediaToastXml(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext, bool canPlayPrevious, bool isPlaying)
    {
        // Create compact ToastGeneric XML with two-column layout
        // Column 1: Artwork, Column 2: Title, Subtitle
        var toastXml = new XmlDocument();
        
        // Create toast element
        var toastElement = toastXml.CreateElement("toast");
        // Set toast to appear on bottom right (UWP style)
        toastElement.SetAttribute("placement", "bottomRight");
        toastElement.SetAttribute("scenario", "reminder"); // Makes it more compact
        toastElement.SetAttribute("useButtonStyle", "false"); // Don't use button style
        toastXml.AppendChild(toastElement);
        
        // Create visual element
        var visual = toastXml.CreateElement("visual");
        toastElement.AppendChild(visual);
        
        // Suppress app name by setting displayName attribute to empty string
        // Note: App icon may still appear, but this hides the app name text
        var displayNameAttribute = toastXml.CreateAttribute("displayName");
        displayNameAttribute.Value = ""; // Empty string to hide app name
        visual.Attributes.SetNamedItem(displayNameAttribute);
        
        // Create binding element with ToastGeneric template
        var binding = toastXml.CreateElement("binding");
        binding.SetAttribute("template", "ToastGeneric");
        // Try to suppress app branding by using hint-overlay (makes toast more compact)
        binding.SetAttribute("hint-overlay", "0"); // 0 = no overlay, makes it more compact
        visual.AppendChild(binding);

        // Create adaptive group for two-column layout (artwork left, text right)
        // IMPORTANT: The first text element in the binding (even if in a group) should be the title
        // to prevent Windows from adding "New Notification"
        var group = toastXml.CreateElement("group");
        binding.AppendChild(group);

        // Column 1: Artwork image (left side)
        var column1 = toastXml.CreateElement("subgroup");
        column1.SetAttribute("hint-weight", "1"); // Smaller column for image
        group.AppendChild(column1);

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
                        var absolutePath = System.IO.Path.GetFullPath(artworkUrl);
                        imageUri = new Uri(absolutePath).ToString();
                    }
                    else
                    {
                        var absolutePath = System.IO.Path.GetFullPath(artworkUrl);
                        imageUri = new Uri(absolutePath).ToString();
                    }
                }

                var imageElement = toastXml.CreateElement("image");
                imageElement.SetAttribute("src", imageUri);
                imageElement.SetAttribute("hint-crop", "none");
                column1.AppendChild(imageElement);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to add artwork image to toast: {ArtworkUrl}", artworkUrl);
            }
        }

        // Column 2: Title and Subtitle (right side)
        var column2 = toastXml.CreateElement("subgroup");
        column2.SetAttribute("hint-weight", "2"); // Larger column for text
        group.AppendChild(column2);

        // Add title in the group (next to artwork) with larger text style
        // Title uses "body" style which is larger than subtitle's "caption" style
        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleElement = toastXml.CreateElement("text");
            titleElement.SetAttribute("hint-style", "body"); // Use body style for larger text (about 2x subtitle size)
            titleElement.SetAttribute("hint-wrap", "true");
            titleElement.AppendChild(toastXml.CreateTextNode(title));
            column2.AppendChild(titleElement);
        }

        // Add subtitle with caption style (smaller than title)
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleElement = toastXml.CreateElement("text");
            subtitleElement.SetAttribute("hint-style", "caption"); // Caption is smaller than body
            subtitleElement.SetAttribute("hint-wrap", "true");
            subtitleElement.AppendChild(toastXml.CreateTextNode(subtitle));
            column2.AppendChild(subtitleElement);
        }

        // No action buttons - toast is display-only
        // Removed next/prev and play/pause buttons as requested

        // Set launch attribute so clicking toast activates the existing app instance
        var launchAttribute = toastXml.CreateAttribute("launch");
        launchAttribute.Value = "MediaPlayback"; // Identifier for media playback activation
        toastElement.Attributes.SetNamedItem(launchAttribute);

        // Add silent audio to suppress sound for media playback toasts
        var audioNode = toastXml.CreateElement("audio");
        audioNode.SetAttribute("silent", "true"); // No sound for media playback toasts
        toastElement.AppendChild(audioNode);

        // Set toast duration to short for compact appearance
        var toastNode = toastXml.SelectSingleNode("/toast");
        if (toastNode?.Attributes != null)
        {
            var durationAttribute = toastXml.CreateAttribute("duration");
            durationAttribute.Value = "short";
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
        // Create ToastGeneric XML manually for more control over content
        var toastXml = new XmlDocument();
        
        // Create toast element
        var toastElement = toastXml.CreateElement("toast");
        toastXml.AppendChild(toastElement);
        
        // Create visual element
        var visual = toastXml.CreateElement("visual");
        toastElement.AppendChild(visual);
        
        // Suppress app name by setting displayName attribute on visual element
        var displayNameAttribute = toastXml.CreateAttribute("displayName");
        displayNameAttribute.Value = ""; // Empty string to hide app name
        visual.Attributes.SetNamedItem(displayNameAttribute);
        
        // Create binding element with ToastGeneric template
        var binding = toastXml.CreateElement("binding");
        binding.SetAttribute("template", "ToastGeneric");
        visual.AppendChild(binding);

        // Only add body text if it's not empty (no title, no app name)
        if (!string.IsNullOrWhiteSpace(body))
        {
            var bodyElement = toastXml.CreateElement("text");
            bodyElement.AppendChild(toastXml.CreateTextNode(body));
            binding.AppendChild(bodyElement);
        }

        // Set launch attribute for activation
        var launchAttribute = toastXml.CreateAttribute("launch");
        launchAttribute.Value = scheduleId.ToString();
        toastElement.Attributes.SetNamedItem(launchAttribute);

        // Add audio
        var audioNode = toastXml.CreateElement("audio");
        audioNode.SetAttribute("src", "ms-winsoundevent:Notification.Default");
        toastElement.AppendChild(audioNode);

        return toastXml;
    }

    private static bool IsNotificationScheduled(ToastNotifier notifier, int scheduleId)
    {
        // Check if any notification exists for this schedule
        // Notification IDs are in format: "{scheduleId}_{hash}" (max 16 characters total)
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
    /// Supports both old format (just scheduleId) and new format ({scheduleId}_{hash}).
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

    /// <summary>
    /// Generates a short hash (11 characters) from a DateTimeOffset for use in notification IDs.
    /// Windows notification IDs have a 16-character limit, so we need a compact representation.
    /// Format: ScheduleId (up to 4 digits) + "_" (1 char) + DateHash (11 chars) = 16 chars max
    /// Uses MD5 hash of the date/time to ensure uniqueness while staying within the character limit.
    /// </summary>
    private static string GetShortDateHash(DateTimeOffset dateTime)
    {
        // Create an MD5 hash of the date/time string to ensure uniqueness
        var dateStr = dateTime.ToString("yyyyMMddHHmmss");
        var bytes = Encoding.UTF8.GetBytes(dateStr);
        var hashBytes = MD5.HashData(bytes);
        
        // Convert hash to base36 (0-9, a-z) for a compact 11-character representation
        const string base36Chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        var hash = new StringBuilder();
        
        // Use first 6 bytes of hash (48 bits) to generate 11 base36 characters
        // 36^11 is much larger than 2^48, so we have good distribution
        ulong value = 0;
        for (int i = 0; i < 6 && i < hashBytes.Length; i++)
        {
            value = (value << 8) | hashBytes[i];
        }
        
        // Convert to base36
        if (value == 0)
        {
            hash.Append('0');
        }
        else
        {
            while (value > 0 && hash.Length < 11)
            {
                hash.Insert(0, base36Chars[(int)(value % 36)]);
                value /= 36;
            }
        }
        
        // Pad to exactly 11 characters for consistency
        return hash.ToString().PadLeft(11, '0').Substring(0, 11);
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
