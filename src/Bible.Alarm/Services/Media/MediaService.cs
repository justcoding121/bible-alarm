using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Services.Media;

public class MediaService(
    MediaIndexService mediaLookUpService,
    IServiceScopeFactory scopeFactory)
    : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private async Task<T> WithDbContextAsync<T>(Func<MediaDbContext, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        return await action(dbContext);
    }

    public async Task<Dictionary<string, Language>> GetBibleLanguages()
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext => 
            await dbContext.BibleTranslations.Select(x => x.Language).Distinct()
                .ToDictionaryAsync(x => x.Code, x => x));
    }

    public async Task<Dictionary<string, BibleTranslation>> GetBibleTranslations(string languageCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext => 
            await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                .ToDictionaryAsync(x => x.Code, x => x));
    }

    public async Task<SortedDictionary<int, BibleBook>> GetBibleBooks(
        string languageCode, string versionCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
        {
            var books = await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .OrderBy(x => x.Number)
                .ToListAsync();
            return new SortedDictionary<int, BibleBook>(books.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task<BibleBook> GetBibleBook(string languageCode, string versionCode, int bookNumber)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
            await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .FirstOrDefaultAsync());
    }

    public async Task<SortedDictionary<int, BibleChapter>>
        GetBibleChapters(string languageCode, string versionCode, int bookNumber)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
        {
            var chapters = await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync();
            return new SortedDictionary<int, BibleChapter>(chapters.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task<BibleChapter> GetBibleChapter(string languageCode,
        string versionCode, int bookNumber, int chapterNumber)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
            await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Where(x => x.Number == chapterNumber)
                .Include(x => x.Source)
                .FirstOrDefaultAsync());
    }

    public async Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases()
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
            await dbContext.MelodyMusic.ToDictionaryAsync(x => x.Code, x => x));
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetMelodyMusicTracks(string publicationCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
        {
            var tracks = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync();
            return new SortedDictionary<int, MusicTrack>(tracks.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
        {
            var languages = await dbContext.VocalMusic.Select(x => x.Language).Distinct().ToListAsync();
            return languages.ToDictionary(x => x.Code, x => x);
        });
    }

    public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
            await dbContext.VocalMusic.Where(x => x.Language.Code == languageCode)
                .ToDictionaryAsync(x => x.Code, x => x));
    }

    public async Task<SortedDictionary<int, MusicTrack>>
        GetVocalMusicTracks(string languageCode, string publicationCode)
    {
        await mediaLookUpService.Verify();
        return await WithDbContextAsync(async dbContext =>
        {
            var tracks = await dbContext.VocalMusic
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync();
            return new SortedDictionary<int, MusicTrack>(tracks.ToDictionary(x => x.Number, x => x));
        });
    }

    public async Task UpdateBibleTrackUrl(string languageCode, string versionCode,
        int bookNumber, int chapterNumber, string url)
    {
        await mediaLookUpService.Verify();
        await WithDbContextAsync(async dbContext =>
        {
            var chapter = await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .Where(x => x.Number == chapterNumber)
                .FirstOrDefaultAsync();
            chapter.Source.Url = url;
            await dbContext.SaveChangesAsync();
            return Task.CompletedTask;
        });
    }

    public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
        int trackNumber, string url)
    {
        await mediaLookUpService.Verify();
        await WithDbContextAsync(async dbContext =>
        {
            var track = await dbContext.VocalMusic
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync();
            track.Source.Url = url;
            await dbContext.SaveChangesAsync();
            return Task.CompletedTask;
        });
    }

    public async Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url)
    {
        await mediaLookUpService.Verify();
        await WithDbContextAsync(async dbContext =>
        {
            var track = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync();
            track.Source.Url = url;
            await dbContext.SaveChangesAsync();
            return Task.CompletedTask;
        });
    }

    public void Dispose()
    {
        // Note: DbContext is now created via IServiceScopeFactory and disposed by the scope
        // mediaLookUpService (MediaIndexService) is a singleton
        // and should not be disposed here as it is managed by the DI container
    }
}