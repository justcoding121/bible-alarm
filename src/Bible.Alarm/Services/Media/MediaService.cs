using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class MediaService(
    IMediaIndexService mediaLookUpService,
    IBibleTranslationService bibleTranslationService,
    IBibleBookService bibleBookService,
    IBibleChapterService bibleChapterService,
    IMelodyMusicService melodyMusicService,
    IVocalMusicService vocalMusicService)
    : IMediaService, IDisposable
{
    private readonly IBibleTranslationService bibleTranslationService = bibleTranslationService ?? throw new ArgumentNullException(nameof(bibleTranslationService));
    private readonly IBibleBookService bibleBookService = bibleBookService ?? throw new ArgumentNullException(nameof(bibleBookService));
    private readonly IBibleChapterService bibleChapterService = bibleChapterService ?? throw new ArgumentNullException(nameof(bibleChapterService));
    private readonly IMelodyMusicService melodyMusicService = melodyMusicService ?? throw new ArgumentNullException(nameof(melodyMusicService));
    private readonly IVocalMusicService vocalMusicService = vocalMusicService ?? throw new ArgumentNullException(nameof(vocalMusicService));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        await mediaLookUpService.Verify();
        return await bibleTranslationService.GetDistinctLanguagesAsync(cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, BibleTranslation>> GetBibleTranslations(string languageCode)
    {
        await mediaLookUpService.Verify();
        return await bibleTranslationService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, BibleBook>> GetBibleBooks(
        string languageCode, string versionCode)
    {
        await mediaLookUpService.Verify();
        return await bibleBookService.GetBooksByTranslationAsync(languageCode, versionCode, cancellationTokenSource.Token);
    }

    public async Task<BibleBook> GetBibleBook(string languageCode, string versionCode, int bookNumber)
    {
        await mediaLookUpService.Verify();
        return await bibleBookService.GetBookAsync(languageCode, versionCode, bookNumber, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, BibleChapter>>
        GetBibleChapters(string languageCode, string versionCode, int bookNumber)
    {
        await mediaLookUpService.Verify();
        return await bibleChapterService.GetChaptersByBookAsync(languageCode, versionCode, bookNumber, cancellationTokenSource.Token);
    }

    public async Task<BibleChapter> GetBibleChapter(string languageCode,
        string versionCode, int bookNumber, int chapterNumber)
    {
        await mediaLookUpService.Verify();
        return await bibleChapterService.GetChapterAsync(languageCode, versionCode, bookNumber, chapterNumber, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases()
    {
        await mediaLookUpService.Verify();
        return await melodyMusicService.GetAllAsync(cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetMelodyMusicTracks(string publicationCode)
    {
        await mediaLookUpService.Verify();
        return await melodyMusicService.GetTracksByCodeAsync(publicationCode, cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        await mediaLookUpService.Verify();
        return await vocalMusicService.GetDistinctLanguagesAsync(cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode)
    {
        await mediaLookUpService.Verify();
        return await vocalMusicService.GetByLanguageCodeAsync(languageCode, cancellationTokenSource.Token);
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        await mediaLookUpService.Verify();
        return await vocalMusicService.GetTracksByLanguageAndCodeAsync(languageCode, publicationCode, cancellationTokenSource.Token);
    }

    public async Task UpdateBibleTrackUrl(string languageCode, string versionCode,
        int bookNumber, int chapterNumber, string url)
    {
        await mediaLookUpService.Verify();
        await bibleChapterService.UpdateChapterUrlAsync(languageCode, versionCode, bookNumber, chapterNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
        int trackNumber, string url)
    {
        await mediaLookUpService.Verify();
        await vocalMusicService.UpdateTrackUrlAsync(languageCode, publicationCode, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url)
    {
        await mediaLookUpService.Verify();
        await melodyMusicService.UpdateTrackUrlAsync(publicationCode, trackNumber, url, cancellationTokenSource.Token);
    }

    public async Task UpdateTrackUrlAsync(TrackMetadata trackMetadata, string url)
    {
        if (trackMetadata.PlayType == PlayType.Bible)
        {
            await UpdateBibleTrackUrl(
                trackMetadata.LanguageCode,
                trackMetadata.PublicationCode,
                trackMetadata.BookNumber,
                trackMetadata.ChapterNumber,
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
        // mediaLookUpService (MediaIndexService) and IServiceScopeFactory are singletons
        // and should not be disposed here as they are managed by the DI container
    }
}
