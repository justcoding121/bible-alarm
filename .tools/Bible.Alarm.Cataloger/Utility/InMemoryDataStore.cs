#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Models.BiblePublications;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Cataloger.Utility;

/// <summary>
/// Thread-safe in-memory data store for cataloged data before seeding to database.
/// Uses ConcurrentDictionary to ensure thread-safety when multiple catalogers write concurrently.
/// </summary>
internal class InMemoryDataStore
{
    // Bible Publications: (languageCode, publicationCode) -> (publicationName, sections, sectionCodeTrackMap)
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), (string PublicationName, Dictionary<int, BiblePublicationSection> Sections, Dictionary<int, Dictionary<int, BiblePublicationTrack>> SectionCodeTrackMap)> BiblePublications { get; } =
        new(PublicationLookupKeyComparers.LanguagePublication.Instance);

    // Mediator publications: (languageCode, publicationCode) -> (publicationName, tracksBySection, sectionNames)
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), (string PublicationName, Dictionary<string, List<MediatorTrack>> TracksBySection, Dictionary<string, string> SectionNames)> MediatorPublications { get; } =
        new(PublicationLookupKeyComparers.LanguagePublication.Instance);

    // Music Publications: (publicationCode, languageCode?) -> (publicationName, tracks)
    public ConcurrentDictionary<(string PublicationCode, string? LanguageCode), (string PublicationName, List<MusicTrack> Tracks)> MusicTracks { get; } =
        new(PublicationLookupKeyComparers.PublicationNullableLanguageCode.Instance);

    // Melody Music: publicationCode -> (discTracksMap, discNamesMap)
    public ConcurrentDictionary<string, (Dictionary<string, List<MusicTrack>> DiscTracksMap, Dictionary<string, string> DiscNamesMap)> MelodyMusic { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Video Publications: (languageCode, publicationCode) -> (publicationName, episodes)
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), (string PublicationName, List<VideoEpisode> Episodes)> VideoPublications { get; } =
        new(PublicationLookupKeyComparers.LanguagePublication.Instance);

    // Language Discovery: (languageCode, publicationCode) -> languageCodeToNameMapping
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), Dictionary<string, string>> LanguageDiscovery { get; } =
        new(PublicationLookupKeyComparers.LanguagePublication.Instance);

    // Publication Languages: publicationCode -> discovered languages (excluding English)
    public ConcurrentDictionary<string, Dictionary<string, LanguageInfo>> PublicationLanguages { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Section Languages: (publicationCode, sectionCode) -> discovered languages (excluding English)
    public ConcurrentDictionary<(string PublicationCode, string SectionCode), Dictionary<string, LanguageInfo>> SectionLanguages { get; } =
        new(PublicationLookupKeyComparers.PublicationSection.Instance);
}
