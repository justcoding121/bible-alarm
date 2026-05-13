#nullable enable

using System;

namespace Bible.Alarm.Shared.Helpers;

/// <summary>
/// Normalizes publication/language identifiers before PublicationLanguages / publication queries.
/// </summary>
public static class PublicationLanguageFetchLookupNormalizer
{
    public static string NormalizeLanguageCodeForPublicationLanguageJoin(string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        return languageCode.Trim().ToUpperInvariant();
    }

    public static string ResolvePublicationCodeForDatabaseLookup(string publicationCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicationCode);

        var lowerCode = publicationCode.ToLowerInvariant();

        return JwSourceHelper.GetCanonicalMediatorPublicationCode(lowerCode) ?? publicationCode;
    }
}
