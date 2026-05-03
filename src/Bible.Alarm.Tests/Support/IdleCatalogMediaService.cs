#nullable enable

using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;

namespace Bible.Alarm.Tests.Support;

internal sealed class IdleCatalogMediaService : IMediaService
{
    public void Dispose()
    {
    }

    public Task<Dictionary<string, Language>> GetBiblePublicationLanguages(string? categoryName = null, bool requireIsMusicForMusicCategory = false) =>
        Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

    public Task<SortedDictionary<string, BiblePublicationTrack>> GetBiblePublicationTracks(string languageCode, string versionCode, string? sectionCode) =>
        Task.FromResult(new SortedDictionary<string, BiblePublicationTrack>());

    public Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode, string? categoryName = null, bool downloadAll = false,
        IFetchProgress? progress = null, bool requireIsMusicForMusicCategory = false) =>
        Task.FromResult(new Dictionary<string, BiblePublication>(StringComparer.OrdinalIgnoreCase));

    public Task<SortedDictionary<string, BiblePublicationSection>> GetBiblePublicationSections(string languageCode, string versionCode,
        IFetchProgress? progress = null) =>
        Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

    public Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsForPublicationWithoutLanguage(string publicationCode) =>
        Task.FromResult(new SortedDictionary<string, BiblePublicationSection>());

    public Task<BiblePublicationSection?> GetBiblePublicationSection(string languageCode, string versionCode, string sectionCode) =>
        Task.FromResult<BiblePublicationSection?>(null);

    public Task<BiblePublicationTrack?> GetBiblePublicationTrack(string languageCode, string versionCode, string? sectionCode, string trackCode) =>
        Task.FromResult<BiblePublicationTrack?>(null);

    public Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases() =>
        Task.FromResult(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

    public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode) =>
        Task.FromResult(new SortedDictionary<int, MusicTrack>());

    public Task<SortedDictionary<int, MusicTrack>> GetMelodyMusicTracksBySection(string publicationCode, string sectionCode) =>
        Task.FromResult(new SortedDictionary<int, MusicTrack>());

    public Task<Dictionary<string, Language>> GetVocalMusicLanguages() =>
        Task.FromResult(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

    public Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode, bool downloadAll = false) =>
        Task.FromResult(new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));

    public Task<SortedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode) =>
        Task.FromResult(new SortedDictionary<int, MusicTrack>());

    public Task UpdateBiblePublicationTrackUrl(string languageCode, string versionCode, string? sectionCode, string trackCode, string url) =>
        Task.CompletedTask;

    public Task UpdateVocalTrackUrl(string languageCode, string publicationCode, string trackCode, string url) =>
        Task.CompletedTask;

    public Task UpdateMelodyTrackUrl(string publicationCode, string trackCode, string url) =>
        Task.CompletedTask;

    public Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url) =>
        Task.CompletedTask;

    public void InvalidateBiblePublicationsCache(string languageCode, string? categoryName = null)
    {
    }

    public Task<bool> IsPublicationWithoutLanguageAsync(string publicationCode) =>
        Task.FromResult(false);

    public Task<int> GetExpectedSectionCountAsync(string languageCode, string publicationCode) =>
        Task.FromResult(0);

    public Task<int> GetExpectedPublicationCountAsync(string languageCode, string categoryName, bool requireIsMusicForMusicCategory = false) =>
        Task.FromResult(0);

    public Task<int> GetExpectedSectionCountForNoLanguagePublicationAsync(string publicationCode) =>
        Task.FromResult(0);
}
