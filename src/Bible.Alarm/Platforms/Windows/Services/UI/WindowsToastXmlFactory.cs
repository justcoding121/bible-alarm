#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;
using Serilog;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

internal static class WindowsToastXmlFactory
{
    internal static XmlDocument CreateMediaToastXml(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext, bool canPlayPrevious, bool isPlaying)
    {
        // Two-column ToastGeneric: artwork left, title/subtitle right
        var toastXml = new XmlDocument();

        var toastElement = toastXml.CreateElement("toast");
        // UWP-style bottom-right placement
        toastElement.SetAttribute("placement", "bottomRight");
        toastElement.SetAttribute("scenario", "reminder"); // Makes it more compact
        toastElement.SetAttribute("useButtonStyle", "false"); // Don't use button style
        toastXml.AppendChild(toastElement);

        var visual = toastXml.CreateElement("visual");
        toastElement.AppendChild(visual);

        // Suppress app name by setting displayName attribute to empty string
        // Note: App icon may still appear, but this hides the app name text
        var displayNameAttribute = toastXml.CreateAttribute("displayName");
        displayNameAttribute.Value = ""; // Empty string to hide app name
        visual.Attributes.SetNamedItem(displayNameAttribute);

        var binding = toastXml.CreateElement("binding");
        binding.SetAttribute("template", "ToastGeneric");
        // hint-overlay 0 suppresses branding overlay and keeps the toast compact
        binding.SetAttribute("hint-overlay", "0"); // 0 = no overlay, makes it more compact
        visual.AppendChild(binding);

        // First text in the binding (even inside a group) must be the title or Windows adds "New Notification"
        var group = toastXml.CreateElement("group");
        binding.AppendChild(group);

        var column1 = toastXml.CreateElement("subgroup");
        column1.SetAttribute("hint-weight", "1"); // Smaller column for image
        group.AppendChild(column1);

        if (!string.IsNullOrWhiteSpace(artworkUrl))
        {
            try
            {
                var imageSrc = ToastArtworkImageSrcResolver.TryResolve(artworkUrl);
                if (imageSrc != null)
                {
                    var imageElement = toastXml.CreateElement("image");
                    imageElement.SetAttribute("src", imageSrc);
                    imageElement.SetAttribute("hint-crop", "none");
                    column1.AppendChild(imageElement);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, AppConstants.Logging.MauiPlatformUiDiagnosticsLog.WindowsToastXmlFailedToAddArtwork, artworkUrl);
            }
        }

        var column2 = toastXml.CreateElement("subgroup");
        column2.SetAttribute("hint-weight", "2"); // Larger column for text
        group.AppendChild(column2);

        // "body" is larger than subtitle's "caption"
        if (!string.IsNullOrWhiteSpace(title))
        {
            var titleElement = toastXml.CreateElement("text");
            titleElement.SetAttribute("hint-style", "body"); // Use body style for larger text (about 2x subtitle size)
            titleElement.SetAttribute("hint-wrap", "true");
            titleElement.AppendChild(toastXml.CreateTextNode(title));
            column2.AppendChild(titleElement);
        }

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

        // Clicking the toast activates the existing app instance
        var launchAttribute = toastXml.CreateAttribute("launch");
        launchAttribute.Value = "MediaPlayback"; // Identifier for media playback activation
        toastElement.Attributes.SetNamedItem(launchAttribute);

        // Add silent audio to suppress sound for media playback toasts
        var audioNode = toastXml.CreateElement("audio");
        audioNode.SetAttribute("silent", "true"); // No sound for media playback toasts
        toastElement.AppendChild(audioNode);

        var toastNode = toastXml.SelectSingleNode("/toast");
        if (toastNode?.Attributes != null)
        {
            var durationAttribute = toastXml.CreateAttribute("duration");
            durationAttribute.Value = "short";
            toastNode.Attributes.SetNamedItem(durationAttribute);
        }

        return toastXml;
    }

    internal static ScheduledToastNotification CreateScheduledToast(string uniqueId, int scheduleId, string title, string body, DateTimeOffset time)
    {
        var toastXml = CreateToastXml(title, body, scheduleId);
        return new ScheduledToastNotification(toastXml, time)
        {
            Id = uniqueId,
            Tag = scheduleId.ToString(),
            Group = WindowsNotificationService.AlarmToastGroup
        };
    }

    internal static XmlDocument CreateToastXml(string title, string body, int scheduleId)
    {
        var toastXml = new XmlDocument();

        // Alarm scenario breaks through Focus Assist
        var toastElement = toastXml.CreateElement("toast");
        toastElement.SetAttribute("scenario", "alarm");
        toastXml.AppendChild(toastElement);

        var visual = toastXml.CreateElement("visual");
        toastElement.AppendChild(visual);

        // Suppress app name by setting displayName attribute on visual element
        var displayNameAttribute = toastXml.CreateAttribute("displayName");
        displayNameAttribute.Value = ""; // Empty string to hide app name
        visual.Attributes.SetNamedItem(displayNameAttribute);

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

        var launchAttribute = toastXml.CreateAttribute("launch");
        launchAttribute.Value = scheduleId.ToString();
        toastElement.Attributes.SetNamedItem(launchAttribute);

        var audioNode = toastXml.CreateElement("audio");
        audioNode.SetAttribute("src", "ms-winsoundevent:Notification.Default");
        toastElement.AppendChild(audioNode);

        return toastXml;
    }
}

