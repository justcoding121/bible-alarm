using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class MediaService(
    IMediaIndexService mediaLookUpService,
    IServiceScopeFactory scopeFactory)
    : IMediaService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _isDisposed;

    private async Task<T> WithDbContextAsync<T>(Func<MediaDbContext, CancellationToken, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        return await action(dbContext, _cancellationTokenSource.Token);
    }

    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) => 
            await dbContext.BibleTranslations.Select(x => x.Language).Distinct()
                .ToDictionaryAsync(x => x.Code, x => x, ct));
    }

    public async Task<Dictionary<string, BibleTranslation>> GetBibleTranslations(string languageCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) => 
            await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                .ToDictionaryAsync(x => x.Code, x => x, ct));
    }

    public async Task<SortedDictionary<int, BibleBook>> GetBibleBooks(
        string languageCode, string versionCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
        {
            var books = await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .OrderBy(x => x.Number)
                .ToListAsync(ct);
            return new SortedDictionary<int, BibleBook>(books.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task<BibleBook> GetBibleBook(string languageCode, string versionCode, int bookNumber)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
            await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .FirstOrDefaultAsync(ct));
    }

    public async Task<SortedDictionary<int, BibleChapter>>
        GetBibleChapters(string languageCode, string versionCode, int bookNumber)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
        {
            var chapters = await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(ct);
            return new SortedDictionary<int, BibleChapter>(chapters.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task<BibleChapter> GetBibleChapter(string languageCode,
        string versionCode, int bookNumber, int chapterNumber)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
            await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Where(x => x.Number == chapterNumber)
                .Include(x => x.Source)
                .FirstOrDefaultAsync(ct));
    }

    public async Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases()
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
            await dbContext.MelodyMusic.ToDictionaryAsync(x => x.Code, x => x, ct));
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetMelodyMusicTracks(string publicationCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
        {
            var tracks = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(ct);
            return new SortedDictionary<int, MusicTrack>(tracks.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
        {
            var languages = await dbContext.VocalMusic.Select(x => x.Language).Distinct().ToListAsync(ct);
            return languages.ToDictionary(x => x.Code, x => x);
        });
    }

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
            await dbContext.VocalMusic.Where(x => x.Language.Code == languageCode)
                .ToDictionaryAsync(x => x.Code, x => x, ct));
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async (dbContext, ct) =>
        {
            var tracks = await dbContext.VocalMusic
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(ct);
            return new SortedDictionary<int, MusicTrack>(tracks.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task UpdateBibleTrackUrl(string languageCode, string versionCode,
        int bookNumber, int chapterNumber, string url)
    {
        await mediaLookUpService.Verify();
        await WithDbContextAsync(async (dbContext, ct) =>
        {
            var chapter = await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .Where(x => x.Number == chapterNumber)
                .FirstOrDefaultAsync(ct);
            chapter.Source.Url = url;
            await dbContext.SaveChangesAsync(ct);
            return Task.CompletedTask;
        });
    }

    public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
        int trackNumber, string url)
    {
        await mediaLookUpService.Verify();
        await WithDbContextAsync(async (dbContext, ct) =>
        {
            var track = await dbContext.VocalMusic
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync(ct);
            track.Source.Url = url;
            await dbContext.SaveChangesAsync(ct);
            return Task.CompletedTask;
        });
    }

    public async Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url)
    {
        await mediaLookUpService.Verify();
        await WithDbContextAsync(async (dbContext, ct) =>
        {
            var track = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync(ct);
            track.Source.Url = url;
            await dbContext.SaveChangesAsync(ct);
            return Task.CompletedTask;
        });
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
        if (_isDisposed)
        {
            return;
        }
        
        _isDisposed = true;
        
        // Cancel and dispose cancellation token source
        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
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