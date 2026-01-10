using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;

namespace Bible.Alarm.Services.Media.Interfaces;

public interface IMediaService : IDisposable
{
    Task<Dictionary<string, Language>> GetBibleLanguages();
    Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode);
    Task<SortedDictionary<int, BibleSection>> GetBibleSections(string languageCode, string versionCode);
    Task<BibleSection> GetBibleSection(string languageCode, string versionCode, int sectionNumber);
    Task<SortedDictionary<int, BiblePublicationChapter>> GetBibleChapters(string languageCode, string versionCode, int sectionNumber);
    Task<BiblePublicationChapter> GetBibleChapter(string languageCode, string versionCode, int sectionNumber, int chapterNumber);
    Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases();
    Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode);
    Task<Dictionary<string, Language>> GetVocalMusicLanguages();
    Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode);
    Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode);
    Task UpdateBibleTrackUrl(string languageCode, string versionCode, int sectionNumber, int chapterNumber, string url);
    Task UpdateVocalTrackUrl(string languageCode, string publicationCode, int trackNumber, string url);
    Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url);
    Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url);
}
