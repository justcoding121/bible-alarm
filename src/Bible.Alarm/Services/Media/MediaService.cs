using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaService(
    IMediaIndexService mediaIndexService,
    IBiblePublicationService BiblePublicationService,
    IBibleSectionService bibleSectionService,
    IBibleTrackService bibleTrackService,
    IMelodyMusicService melodyMusicService,
    IVocalMusicService vocalMusicService)
    : IMediaService, IDisposable
{
    private readonly IMediaIndexService mediaIndexService = mediaIndexService ?? throw new ArgumentNullException(nameof(mediaIndexService));
    private readonly IBiblePublicationService BiblePublicationService = BiblePublicationService ?? throw new ArgumentNullException(nameof(BiblePublicationService));
    private readonly IBibleSectionService bibleSectionService = bibleSectionService ?? throw new ArgumentNullException(nameof(bibleSectionService));
    private readonly IBibleTrackService bibleTrackService = bibleTrackService ?? throw new ArgumentNullException(nameof(bibleTrackService));
    private readonly IMelodyMusicService melodyMusicService = melodyMusicService ?? throw new ArgumentNullException(nameof(melodyMusicService));
    private readonly IVocalMusicService vocalMusicService = vocalMusicService ?? throw new ArgumentNullException(nameof(vocalMusicService));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        await mediaIndexService.Verify();
        return await BiblePublicationService.GetDistinctLanguagesAsync(cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, BiblePublication>> GetBiblePublications(string languageCode)
    {
        await mediaIndexService.Verify();
        return await BiblePublicationService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, BibleSection>> GetBibleSections(
        string languageCode, string versionCode)
    {
        await mediaIndexService.Verify();
        return await bibleSectionService.GetSectionsByPublicationAsync(languageCode, versionCode, cancellationTokenSource.Token);
    }

    public async Task<BibleSection> GetBibleSection(string languageCode, string versionCode, int sectionNumber)
    {
        await mediaIndexService.Verify();
        return await bibleSectionService.GetSectionAsync(languageCode, versionCode, sectionNumber, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, BiblePublicationTrack>>
        GetBibleTracks(string languageCode, string versionCode, int sectionNumber)
    {
        await mediaIndexService.Verify();
        return await bibleTrackService.GetTracksBySectionAsync(languageCode, versionCode, sectionNumber, cancellationTokenSource.Token);
    }

    public async Task<BiblePublicationTrack> GetBibleTrack(string languageCode,
        string versionCode, int sectionNumber, int trackNumber)
    {
        await mediaIndexService.Verify();
        return await bibleTrackService.GetTrackAsync(languageCode, versionCode, sectionNumber, trackNumber, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases()
    {
        await mediaIndexService.Verify();
        return await melodyMusicService.GetAllAsync(cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetMelodyMusicTracks(string publicationCode)
    {
        await mediaIndexService.Verify();
        return await melodyMusicService.GetTracksByCodeAsync(publicationCode, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        await mediaIndexService.Verify();
        var result = await vocalMusicService.GetDistinctLanguagesAsync(cancellationTokenSource.Token);
        Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: returned {Count} languages: {LanguageCodes}",
            result.Count, string.Join(", ", result.Keys));

        // If no vocal languages found, fall back to basic languages (English)
        // This can happen if the vocal music database doesn't have language metadata
        if (result.Count == 0)
        {
            Serilog.Log.Debug("MediaService.GetVocalMusicLanguages: No vocal languages found, falling back to English");
            result = new Dictionary<string, Language>
            {
                ["E"] = new Language { Code = "E", Name = "English" }
            };
        }

        return result;
    }

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode)
    {
        await mediaIndexService.Verify();
        return await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        await mediaIndexService.Verify();
        return await vocalMusicService.GetTracksByLanguageAndCodeAsync(languageCode, publicationCode, cancellationTokenSource.Token);
    }

    public async Task UpdateBibleTrackUrl(string languageCode, string versionCode,
        int sectionNumber, int trackNumber, string url)
    {
        await mediaIndexService.Verify();
        await bibleTrackService.UpdateTrackUrlAsync(languageCode, versionCode, sectionNumber, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
        int trackNumber, string url)
    {
        await mediaIndexService.Verify();
        await vocalMusicService.UpdateTrackUrlAsync(languageCode, publicationCode, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url)
    {
        await mediaIndexService.Verify();
        await melodyMusicService.UpdateTrackUrlAsync(publicationCode, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url)
    {
        if (trackMetadata.PlayType == PlayType.Bible)
        {
            await UpdateBibleTrackUrl(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.SectionNumber,
                trackMetadata.TrackNumber,
                url);
        }
        else
        {
            if (trackMetadata.LanguageCode == null)
            {
                await UpdateMelodyTrackUrl(
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackNumber,
                    url);
            }
            else
            {
                await UpdateVocalTrackUrl(
                    trackMetadata.LanguageCode,
                    trackMetadata.PublicationCode,
                    trackMetadata.TrackNumber,
                    url);
            }
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;

        // Cancel and dispose cancellation token source
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            // Ignore errors during cancellation/disposal
            Log.Logger.Warning(ex, "Error during cancellation token source disposal");
        }

        // Note: DbContext is now created via IServiceScopeFactory and disposed by the scope
        // mediaIndexService (MediaIndexService) and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
