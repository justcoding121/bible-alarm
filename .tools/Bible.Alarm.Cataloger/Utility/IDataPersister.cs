#nullable enable

using System.Collections.Generic;
using System.Threading.Tasks;
using Bible.Alarm.Cataloger.Models;
using Bible.Alarm.Cataloger.Models.BiblePublications;

namespace Bible.Alarm.Cataloger.Utility;

/// <summary>
/// Interface for persisting cataloged data directly to database without intermediate files.
/// </summary>
internal interface IDataPersister
{
    Task SaveBiblePublicationSections(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<int, BiblePublicationSection> sections,
        Dictionary<int, Dictionary<int, BiblePublicationTrack>> sectionCodeTrackMap);

    Task SaveMediatorPublication(
        string languageCode,
        string publicationCode,
        string publicationName,
        Dictionary<string, List<MediatorTrack>> tracksBySection,
        Dictionary<string, string> sectionNames);

    Task SaveMusicTracks(
        string publicationCode,
        string? languageCode,
        string publicationName,
        List<MusicTrack> tracks);

    Task SaveMelodyMusicTracks(
        string publicationCode,
        Dictionary<string, List<MusicTrack>> discTracksMap,
        Dictionary<string, string> discNamesMap);

    Task SaveVideoEpisodes(
        string languageCode,
        string publicationCode,
        string publicationName,
        List<VideoEpisode> episodes);

    Task SaveLanguageDiscovery(
        string languageCode,
        string publicationCode,
        Dictionary<string, string> languageCodeToNameMapping);

    Task SavePublicationLanguages(
        string publicationCode,
        Dictionary<string, LanguageInfo> discoveredLanguages);

    Task SaveSectionLanguages(
        string publicationCode,
        string sectionCode,
        Dictionary<string, LanguageInfo> discoveredLanguages);
}
