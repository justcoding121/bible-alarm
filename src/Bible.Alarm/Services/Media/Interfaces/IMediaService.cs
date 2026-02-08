#nullable enable annotations
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaService : IDisposable
{
    Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null);
    Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false, IFetchProgress? progress = null);
    Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode, IFetchProgress? progress = null);
    Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode);
    Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode);
    Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode);
    Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode);
    Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases();
    Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode);
    Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode);
    Task<Dictionary<string, Language>> GetVocalMusicLanguages();
    Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false);
    Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode);
    Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url);
    Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url);
    Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url);
    Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url);
    void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null);
    Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode);
    Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode);
    Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName);
    Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode);
}
