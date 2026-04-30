#nullable enable

using Bible.Alarm.Shared.Constants;
using Serilog;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Bible.Alarm.Platforms.Windows.Services.UI;

internal static class WindowsToastXmlFactory
{
    internal static XmlDocument CreateMediaToastXml(string? title, string? subtitle, string? body, string? artworkUrl, bool canPlayNext, bool canPlayPrevious, bool isPlaying)
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
                string imageUri;

                // If it's already a URI (http/https), use it as-is
                if (Uri.TryCreate(artworkUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    imageUri = artworkUrl;
                }
                else
                {
                    // For local files, convert to file:// URI
                    var absolutePath = System.IO.Path.GetFullPath(artworkUrl);
                    imageUri = new Uri(absolutePath).ToString();
                }

                var imageElement = toastXml.CreateElement("image");
                imageElement.SetAttribute("src", imageUri);
                imageElement.SetAttribute("hint-crop", "none");
                column1.AppendChild(imageElement);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, AppConstants.Logging.MauiPlatformUiDiagnosticsLog.WindowsToastXmlFailedToAddArtwork, artworkUrl);
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
        // Create ToastGeneric XML manually for more control over content
        var toastXml = new XmlDocument();

        // Create toast element with alarm scenario so it breaks through Focus Assist
        var toastElement = toastXml.CreateElement("toast");
        toastElement.SetAttribute("scenario", "alarm");
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
}

