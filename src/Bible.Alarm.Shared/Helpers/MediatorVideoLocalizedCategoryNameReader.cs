#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Reads localized category display names from mediator JSON payloads for video publications.
/// </summary>
public static class MediatorVideoLocalizedCategoryNameReader
{
    public static bool TryReadLocalizedDisplayName(JsonElement root, [NotNullWhen(true)] out string? localizedName)
    {
        localizedName = null;

        if (!root.TryGetProperty(AppConstants.Media.PubMediaJson.Category, out var category))
        {
            return false;
        }

        if (!category.TryGetProperty(AppConstants.Media.PubMediaJson.Name, out var nameElement))
        {
            return false;
        }

        var decoded = MediaTrackTitleHelper.DecodeHtmlTitleNullable(nameElement.GetString());
        if (string.IsNullOrEmpty(decoded))
        {
            return false;
        }

        localizedName = decoded;
        return true;
    }
}
