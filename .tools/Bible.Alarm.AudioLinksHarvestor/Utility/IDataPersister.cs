#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.BiblePublications;
using Bible.Alarm.AudioLinksHarvestor.Models.Drama;
using Bible.Alarm.AudioLinksHarvestor.Models.Music;
using Bible.Alarm.AudioLinksHarvestor.Models.Video;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

/// <summary>
/// Interface for persisting harvested data directly to database without intermediate files.
/// </summary>
internal interface IDataPersister
{
    // Bible Publications
    Task SaveBiblePublicationSections(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<int, BiblePublicationSection> sections,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionNumberTrackMap);

    // Drama Publications
    Task SaveDramaPublication(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<string, List<DramaTrack>> tracksBySection,
        Dictionary<string, string> sectionNames);

    // Music Publications
    Task SaveMusicTracks(
        string publicationCode,
        string? languageCode,
        string publicationName,
        List<MusicTrack> tracks);

    Task SaveMelodyMusicTracks(
        string publicationCode,
        Dictionary<string, List<MusicTrack>> discTracksMap,
        Dictionary<string, string> discNamesMap);

    // Video Publications
    Task SaveVideoEpisodes(
        string languageCode,
        string publicationCode,
        string publicationName,
        List<VideoEpisode> episodes);

    // Language discovery
    Task SaveLanguageDiscovery(
        string languageCode,
        string publicationCode,
        Dictionary<string, string> languageCodeToNameMapping);

    // Save discovered languages for publications and sections
    Task SavePublicationLanguages(
        string publicationCode,
        Dictionary<string, LanguageInfo> discoveredLanguages);

    Task SaveSectionLanguages(
        string publicationCode,
        string sectionCode,
        Dictionary<string, LanguageInfo> discoveredLanguages);
}
