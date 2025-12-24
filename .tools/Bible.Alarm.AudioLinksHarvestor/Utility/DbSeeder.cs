#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using BibleBook = Bible.Alarm.AudioLinksHarvestor.Models.Bible.BibleBook;
using BibleChapter = Bible.Alarm.AudioLinksHarvestor.Models.Bible.BibleChapter;
using MusicTrack = Bible.Alarm.AudioLinksHarvestor.Models.Music.MusicTrack;
using Publication = Bible.Alarm.AudioLinksHarvestor.Models.Publication;

namespace Bible.Alarm.AudioLinksHarvestor.Utility;

public class DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory)
{
    public async Task Seed()
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = DELETE;");
            await db.Database.MigrateAsync();
        }

        var indexDir = DirectoryHelper.IndexDirectory;
        var mediaDir = Path.Combine(indexDir, "media");
        await SeedBibleTranslations(mediaDir);
        await SeedMelodies(mediaDir);
        await SeedVocals(mediaDir);
    }

    private async Task SeedBibleTranslations(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var bibleLanguages = await GetBibleLanguagesSafely(mediaReader);
        if (bibleLanguages == null || bibleLanguages.Count == 0)
        {
            return;
        }

        foreach (var language in bibleLanguages)
        {
            var newLanguage = await GetOrCreateLanguage(db, language.Value.Code, language.Value.Name);
            await SeedTranslationsForLanguage(db, mediaReader, language.Key, newLanguage, displayLanguage);
        }
    }

    private async Task<Language> GetOrCreateDisplayLanguage(MediaDbContext db)
    {
        var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
        if (displayLanguage == null)
        {
            displayLanguage = new Language
            {
                Code = "E",
                Name = "English"
            };
            db.Languages.Add(displayLanguage);
            await db.SaveChangesAsync();
        }
        return displayLanguage;
    }

    private async Task<Language> GetOrCreateLanguage(MediaDbContext db, string code, string name)
    {
        var language = await db.Languages.FirstOrDefaultAsync(x => x.Code == code && x.Name == name);
        if (language == null)
        {
            language = new Language
            {
                Code = code,
                Name = name
            };
        }
        return language;
    }

    private static async Task<T?> GetSafely<T>(Func<Task<T>> getter) where T : class
    {
        try
        {
            return await getter();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private async Task<Dictionary<string, Models.Language>?> GetBibleLanguagesSafely(MediaReader mediaReader)
    {
        return await GetSafely(() => mediaReader.GetBibleLanguages());
    }

    private async Task SeedTranslationsForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageKey,
        Language newLanguage,
        Language displayLanguage)
    {
        var translations = await GetSafely(() => mediaReader.GetBibleTranslations(languageKey));
        if (translations == null || translations.Count == 0)
        {
            return;
        }

        foreach (var translation in translations)
        {
            logger.Information("Seeding translation {TranslationName} ({TranslationCode}) for language {LanguageCode}", 
                translation.Value.Name, translation.Value.Code, languageKey);

            var books = await GetSafely(() => mediaReader.GetBibleBooks(languageKey, translation.Key));
            if (books == null || books.Count == 0)
            {
                continue;
            }

            var bibleTranslation = CreateBibleTranslation(translation.Value, newLanguage, displayLanguage);
            await SeedBooksForTranslation(db, mediaReader, languageKey, translation.Key, books, bibleTranslation);
            
            await db.BibleTranslations.AddAsync(bibleTranslation);
            await db.SaveChangesAsync();
        }
    }

    private static BibleTranslation CreateBibleTranslation(Publication translation, Language newLanguage, Language displayLanguage)
    {
        return new BibleTranslation
        {
            Name = translation.Name,
            Code = translation.Code,
            Language = newLanguage,
            DisplayLanguage = displayLanguage
        };
    }

    private async Task SeedBooksForTranslation(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageKey,
        string translationKey,
        SortedDictionary<int, BibleBook> books,
        BibleTranslation bibleTranslation)
    {
        foreach (var book in books)
        {
            var newBook = new Shared.Models.Media.Bible.BibleBook
            {
                Name = book.Value.Name,
                Number = book.Value.Number
            };

            bibleTranslation.Books.Add(newBook);

            var chapters = await GetSafely(() => mediaReader.GetBibleChapters(languageKey, translationKey, book.Key));
            if (chapters == null || chapters.Count == 0)
            {
                continue;
            }

            AddChaptersToBook(chapters, newBook, bibleTranslation);
        }
    }

    private static void AddChaptersToBook(
        SortedDictionary<int, BibleChapter> chapters,
        Shared.Models.Media.Bible.BibleBook newBook,
        BibleTranslation bibleTranslation)
    {
        foreach (var chapter in chapters)
        {
            var lookUpPath = BuildChapterLookUpPath(bibleTranslation, newBook, chapter.Value.Number);
            var newChapter = new Shared.Models.Media.Bible.BibleChapter
            {
                Number = chapter.Value.Number,
                Source = new AudioSource
                {
                    Url = chapter.Value.Url,
                    LookUpPath = lookUpPath
                }
            };

            newBook.Chapters.Add(newChapter);
        }
    }

    private static string BuildChapterLookUpPath(BibleTranslation bibleTranslation, Shared.Models.Media.Bible.BibleBook newBook, int chapterNumber)
    {
        return $"?output=json&pub={bibleTranslation.Code}" +
               $"&fileformat=MP3&langwritten={bibleTranslation.Language.Code}" +
               $"&txtCMSLang=E&booknum={newBook.Number}&track={chapterNumber}";
    }

    private async Task SeedMelodies(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var melodyMusicReleases = await GetSafely(() => mediaReader.GetMelodyMusicReleases());
        if (melodyMusicReleases == null || melodyMusicReleases.Count == 0)
        {
            return;
        }

        foreach (var melodyMusicRelease in melodyMusicReleases)
        {
            logger.Information("Seeding melody code {MelodyCode} music to database.", melodyMusicRelease.Key);

            var tracks = await GetSafely(() => mediaReader.GetMelodyMusicTracks(melodyMusicRelease.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            var newMelodyMusic = CreateMelodyMusic(melodyMusicRelease.Value, displayLanguage);
            AddTracksToMelodyMusic(tracks, newMelodyMusic);

            await db.MelodyMusic.AddAsync(newMelodyMusic);
            await db.SaveChangesAsync();
        }
    }

    private static MelodyMusic CreateMelodyMusic(Publication melodyMusicRelease, Language displayLanguage)
    {
        return new MelodyMusic
        {
            Code = melodyMusicRelease.Code,
            Name = melodyMusicRelease.Name,
            DisplayLanguage = displayLanguage
        };
    }

    private static void AddTracksToMelodyMusic(SortedDictionary<int, MusicTrack> tracks, MelodyMusic newMelodyMusic)
    {
        foreach (var track in tracks)
        {
            var newTrack = new Shared.Models.Media.Music.MusicTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Source = new AudioSource
                {
                    Url = track.Value.Url,
                    LookUpPath = track.Value.LookUpPath
                }
            };

            newMelodyMusic.Tracks.Add(newTrack);
        }
    }

    private async Task SeedVocals(string indexDir)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        var displayLanguage = await GetOrCreateDisplayLanguage(db);
        var mediaReader = new MediaReader(indexDir);

        var melodyLanguages = await GetSafely(() => mediaReader.GetVocalMusicLanguages());
        if (melodyLanguages == null || melodyLanguages.Count == 0)
        {
            return;
        }

        foreach (var language in melodyLanguages)
        {
            var newLanguage = await GetOrCreateLanguage(db, language.Value.Code, language.Value.Name);
            await SeedVocalMusicReleasesForLanguage(db, mediaReader, language.Value.Code, newLanguage, displayLanguage);
        }
    }

    private async Task SeedVocalMusicReleasesForLanguage(
        MediaDbContext db,
        MediaReader mediaReader,
        string languageCode,
        Language newLanguage,
        Language displayLanguage)
    {
        var vocalMusicReleases = await GetSafely(() => mediaReader.GetVocalMusicReleases(languageCode));
        if (vocalMusicReleases == null || vocalMusicReleases.Count == 0)
        {
            return;
        }

        foreach (var vocalMusicRelease in vocalMusicReleases)
        {
            logger.Information("Seeding song book {SongBookName} ({SongBookCode}) for language {LanguageCode}", 
                vocalMusicRelease.Value.Name, vocalMusicRelease.Value.Code, languageCode);

            var tracks = await GetSafely(() => mediaReader.GetVocalMusicTracks(languageCode, vocalMusicRelease.Key));
            if (tracks == null || tracks.Count == 0)
            {
                continue;
            }

            var newVocalMusic = CreateVocalMusic(vocalMusicRelease.Value, newLanguage, displayLanguage);
            AddTracksToVocalMusic(tracks, newVocalMusic);

            await db.VocalMusic.AddAsync(newVocalMusic);
            await db.SaveChangesAsync();
        }
    }

    private static VocalMusic CreateVocalMusic(Publication vocalMusicRelease, Language newLanguage, Language displayLanguage)
    {
        return new VocalMusic
        {
            Code = vocalMusicRelease.Code,
            Name = vocalMusicRelease.Name,
            DisplayLanguage = displayLanguage,
            Language = newLanguage
        };
    }

    private static void AddTracksToVocalMusic(SortedDictionary<int, MusicTrack> tracks, VocalMusic newVocalMusic)
    {
        foreach (var track in tracks)
        {
            var newTrack = new Shared.Models.Media.Music.MusicTrack
            {
                Number = track.Value.Number,
                Title = track.Value.Title,
                Source = new AudioSource
                {
                    Url = track.Value.Url,
                    LookUpPath = track.Value.LookUpPath
                }
            };

            newVocalMusic.Tracks.Add(newTrack);
        }
    }
}

