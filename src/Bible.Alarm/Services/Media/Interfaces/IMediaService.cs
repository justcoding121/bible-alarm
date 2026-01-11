using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaService : IDisposable
{
    Task<Dictionary<string, Language>> GetBiblePublicationLanguages();
    Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode);
    Task<SortedDictionary<int, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode);
    Task<BiblePublicationSection> GetBiblePublicationSection(string languageCode, string versionCode, int sectionNumber);
    Task<SortedDictionary<int, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, int sectionNumber);
    Task<BiblePublicationTrack> GetBiblePublicationTrack(string languageCode, string versionCode, int sectionNumber, int trackNumber);
    Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases();
    Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode);
    Task<Dictionary<string, Language>> GetVocalMusicLanguages();
    Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode);
    Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode);
    Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, int sectionNumber, int trackNumber, string url);
    Task UpdateVocalTrackUrl(string languageCode, string publicationCode, int trackNumber, string url);
    Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url);
    Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url);
}
