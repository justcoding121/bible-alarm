#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

/// <summary>
/// Thread-safe in-memory data store for harvested data before seeding to database.
/// Uses ConcurrentDictionary to ensure thread-safety when multiple harvesters write concurrently.
/// </summary>
internal class InMemoryDataStore
{
    // Bible Publications: (languageCode, publicationCode) -> (publicationName, sections, sectionCodeTrackMap)
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), (string PublicationName, Dictionary<int, BiblePublicationSection> Sections, Dictionary<int, Dictionary<int, BiblePublicationTrack>> SectionCodeTrackMap)> BiblePublications { get; } = new();

    // Mediator publications: (languageCode, publicationCode) -> (publicationName, tracksBySection, sectionNames)
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), (string PublicationName, Dictionary<string, List<MediatorTrack>> TracksBySection, Dictionary<string, string> SectionNames)> MediatorPublications { get; } = new();

    // Music Publications: (publicationCode, languageCode?) -> (publicationName, tracks)
    public ConcurrentDictionary<(string PublicationCode, string? LanguageCode), (string PublicationName, List<MusicTrack> Tracks)> MusicTracks { get; } = new();

    // Melody Music: publicationCode -> (discTracksMap, discNamesMap)
    public ConcurrentDictionary<string, (Dictionary<string, List<MusicTrack>> DiscTracksMap, Dictionary<string, string> DiscNamesMap)> MelodyMusic { get; } = new();

    // Video Publications: (languageCode, publicationCode) -> (publicationName, episodes)
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), (string PublicationName, List<VideoEpisode> Episodes)> VideoPublications { get; } = new();

    // Language Discovery: (languageCode, publicationCode) -> languageCodeToNameMapping
    public ConcurrentDictionary<(string LanguageCode, string PublicationCode), Dictionary<string, string>> LanguageDiscovery { get; } = new();

    // Publication Languages: publicationCode -> discovered languages (excluding English)
    public ConcurrentDictionary<string, Dictionary<string, LanguageInfo>> PublicationLanguages { get; } = new();

    // Section Languages: (publicationCode, sectionCode) -> discovered languages (excluding English)
    public ConcurrentDictionary<(string PublicationCode, string SectionCode), Dictionary<string, LanguageInfo>> SectionLanguages { get; } = new();
}
