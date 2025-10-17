using Advanced.Algorithms.DataStructures.Foundation;
using Bible.Alarm.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bible.Alarm.Services
{
    public class MediaService : IDisposable
    {
        private MediaIndexService mediaLookUpService;
        private MediaDbContext dbContext;

        public MediaService(MediaIndexService mediaLookUpService,
            MediaDbContext dbContext)
        {
            this.mediaLookUpService = mediaLookUpService;
            this.dbContext = dbContext;
        }

        public async Task<Dictionary<string, Language>> GetBibleLanguages()
        {
            await mediaLookUpService.Verify();
            return await dbContext.BibleTranslations.Select(x => x.Language).Distinct().ToDictionaryAsync(x => x.Code, x => x);
        }

        public async Task<Dictionary<string, BibleTranslation>> GetBibleTranslations(string languageCode)
        {
            await mediaLookUpService.Verify();
            return await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode).ToDictionaryAsync(x => x.Code, x => x);
        }

        public async Task<OrderedDictionary<int, BibleBook>> GetBibleBooks(string languageCode, string versionCode)
        {
            await mediaLookUpService.Verify();

            var books = await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                                                         .Where(x => x.Code == versionCode)
                                                         .SelectMany(x => x.Books)
                                                         .OrderBy(x => x.Number)
                                                         .ToListAsync();

            return new OrderedDictionary<int, BibleBook>(books.Select(x => new KeyValuePair<int, BibleBook>(x.Number, x)));
        }

        public async Task<BibleBook> GetBibleBook(string languageCode, string versionCode, int bookNumber)
        {
            await mediaLookUpService.Verify();

            var book = await dbContext.BibleTranslations.Where(x => x.Language.Code == languageCode)
                                                         .Where(x => x.Code == versionCode)
                                                         .SelectMany(x => x.Books)
                                                         .Where(x => x.Number == bookNumber)
                                                         .FirstOrDefaultAsync();

            return book;
        }

        public async Task<OrderedDictionary<int, BibleChapter>> GetBibleChapters(string languageCode, string versionCode, int bookNumber)
        {
            await mediaLookUpService.Verify();

            var chapters = await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync();

            return new OrderedDictionary<int, BibleChapter>(chapters.Select(x => new KeyValuePair<int, BibleChapter>(x.Number, x)));
        }

        public async Task<BibleChapter> GetBibleChapter(string languageCode, 
            string versionCode, int bookNumber, int chapterNumber)
        {
            await mediaLookUpService.Verify();

            return await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == versionCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Where(x => x.Number == chapterNumber)
                .Include(x => x.Source)
                .FirstOrDefaultAsync();
        }

        public async Task<Dictionary<string, MelodyMusic>> GetMelodyMusicReleases()
        {
            await mediaLookUpService.Verify();

            return await dbContext.MelodyMusic.ToDictionaryAsync(x => x.Code, x => x);
        }

        public async Task<OrderedDictionary<int, MusicTrack>> GetMelodyMusicTracks(string publicationCode)
        {
            await mediaLookUpService.Verify();

            var tracks = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync();

            return new OrderedDictionary<int, MusicTrack>(tracks.Select(x => new KeyValuePair<int, MusicTrack>(x.Number, x)));
        }

        public async Task<Dictionary<string, Language>> GetVocalMusicLanguages()
        {
            await mediaLookUpService.Verify();

            var languages = await dbContext.VocalMusic.Select(x => x.Language).Distinct().ToListAsync();
            return languages.ToDictionary(x => x.Code, x => x);
        }

        public async Task<Dictionary<string, VocalMusic>> GetVocalMusicReleases(string languageCode)
        {
            await mediaLookUpService.Verify();

            return await dbContext.VocalMusic.Where(x => x.Language.Code == languageCode).ToDictionaryAsync(x => x.Code, x => x);
        }

        public async Task<OrderedDictionary<int, MusicTrack>> GetVocalMusicTracks(string languageCode, string publicationCode)
        {
            await mediaLookUpService.Verify();

            var tracks = await dbContext.VocalMusic
                .Where(x => x.Language.Code == languageCode)
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync();

            return new OrderedDictionary<int, MusicTrack>(tracks.Select(x => new KeyValuePair<int, MusicTrack>(x.Number, x)));
        }

        public async Task UpdateBibleTrackUrl(string languageCode, string versionCode,
                                        int bookNumber, int chapterNumber, string url)
        {
            await mediaLookUpService.Verify();

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
        }

        public async Task UpdateVocalTrackUrl(string languageCode, string publicationCode,
                                       int trackNumber, string url)
        {
            await mediaLookUpService.Verify();

            var track = await dbContext.VocalMusic
                   .Where(x => x.Language.Code == languageCode)
                   .Where(x => x.Code == publicationCode)
                   .SelectMany(x => x.Tracks)
                   .Include(x => x.Source)
                   .Where(x => x.Number == trackNumber)
                   .FirstOrDefaultAsync();

            track.Source.Url = url;
            await dbContext.SaveChangesAsync();
        }

        public async Task UpdateMelodyTrackUrl(string publicationCode, int trackNumber, string url)
        {
            await mediaLookUpService.Verify();

            var track = await dbContext.MelodyMusic
                .Where(x => x.Code == publicationCode)
                .SelectMany(x => x.Tracks)
                .Include(x => x.Source)
                .Where(x => x.Number == trackNumber)
                .FirstOrDefaultAsync();

            track.Source.Url = url;
            await dbContext.SaveChangesAsync();
        }

        public void Dispose()
        {
            dbContext.Dispose();
            mediaLookUpService.Dispose();
        }
    }
}
