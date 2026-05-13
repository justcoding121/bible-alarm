#nullable enable

using System;
using Bible.Alarm.Shared.Constants;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Builds relative mediator categories paths ({prefix}/{language}/{categoryKey}) used by video localized-name fetchers.
/// </summary>
public static class JwMediatorVideoCategoryApiRelativePath
{
    public static string Compose(string normalizedLanguageCode, string categoryKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedLanguageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        return $"{AppConstants.ApiEndpoints.MediatorApiCategoriesPathPrefix}/{normalizedLanguageCode}/{categoryKey}";
    }
}
