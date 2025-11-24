using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bible.Alarm.AudioLinksHarvestor.Models;
using Bible.Alarm.AudioLinksHarvestor.Models.Bible;
using Bible.Alarm.AudioLinksHarvestor.Models.Music;
using Bible.Alarm.AudioLinksHarvestor.Utility;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Utility
{
    public class DbSeeder
    {
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public DbSeeder(ILogger logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task Seed()
        {
            using (var scope = _scopeFactory.CreateScope())
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
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
            if (displayLanguage == null)
            {
                displayLanguage = new Bible.Alarm.Shared.Models.Media.Language
                {
                    Code = "E",
                    Name = "English"
                };
                db.Languages.Add(displayLanguage);
                await db.SaveChangesAsync();
            }

            var mediaReader = new MediaReader(indexDir);

            Dictionary<string, Bible.Alarm.AudioLinksHarvestor.Models.Language> bibleLanguages;
            try
            {
                bibleLanguages = await mediaReader.GetBibleLanguages();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if (bibleLanguages == null || bibleLanguages.Count == 0)
            {
                return;
            }

            foreach (var language in bibleLanguages)
            {
                var newLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Code == language.Value.Code
                                                                        && x.Name == language.Value.Name);
                if (newLanguage == null)
                {
                    newLanguage = new Bible.Alarm.Shared.Models.Media.Language
                    {
                        Code = language.Value.Code,
                        Name = language.Value.Name
                    };
                }

                Dictionary<string, Bible.Alarm.AudioLinksHarvestor.Models.Publication> translations;
                try
                {
                    translations = await mediaReader.GetBibleTranslations(language.Key);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if (translations == null || translations.Count == 0)
                {
                    continue;
                }

                foreach (var translation in translations)
                {
                    _logger.Information("Seeding translation {TranslationName} ({TranslationCode}) for language {LanguageCode}", translation.Value.Name, translation.Value.Code, language.Key);
                    
                    SortedDictionary<int, Bible.Alarm.AudioLinksHarvestor.Models.Bible.BibleBook> books;
                    try
                    {
                        books = await mediaReader.GetBibleBooks(language.Key, translation.Key);
                    }
                    catch (FileNotFoundException)
                    {
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }

                    if (books == null || books.Count == 0)
                    {
                        continue;
                    }

                    var bibleTranslation = new Bible.Alarm.Shared.Models.Media.Bible.BibleTranslation
                    {
                        Name = translation.Value.Name,
                        Code = translation.Value.Code,
                        Language = newLanguage,
                        DisplayLanguage = displayLanguage
                    };

                    foreach (var book in books)
                    {
                        var newBook = new Bible.Alarm.Shared.Models.Media.Bible.BibleBook
                        {
                            Name = book.Value.Name,
                            Number = book.Value.Number
                        };

                        bibleTranslation.Books.Add(newBook);

                        SortedDictionary<int, Bible.Alarm.AudioLinksHarvestor.Models.Bible.BibleChapter> chapters;
                        try
                        {
                            chapters = await mediaReader.GetBibleChapters(language.Key, translation.Key, book.Key);
                        }
                        catch (FileNotFoundException)
                        {
                            continue;
                        }
                        catch (DirectoryNotFoundException)
                        {
                            continue;
                        }

                        if (chapters == null || chapters.Count == 0)
                        {
                            continue;
                        }

                        foreach (var chapter in chapters)
                        {
                            string lookUpPath;

                            lookUpPath = $"?output=json&pub={bibleTranslation.Code}" +
                                             $"&fileformat=MP3&langwritten={bibleTranslation.Language.Code}" +
                                             $"&txtCMSLang=E&booknum={newBook.Number}&track={chapter.Value.Number}";


                            var newChapter = new Bible.Alarm.Shared.Models.Media.Bible.BibleChapter
                            {
                                Number = chapter.Value.Number,
                                Source = new Bible.Alarm.Shared.Models.Media.AudioSource
                                {
                                    Url = chapter.Value.Url,
                                    LookUpPath = lookUpPath
                                }
                            };

                            newBook.Chapters.Add(newChapter);
                        }
                    }

                    await db.BibleTranslations.AddAsync(bibleTranslation);
                    await db.SaveChangesAsync();
                }
            }
        }

        private async Task SeedMelodies(string indexDir)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
            if (displayLanguage == null)
            {
                displayLanguage = new Bible.Alarm.Shared.Models.Media.Language
                {
                    Code = "E",
                    Name = "English"
                };
                db.Languages.Add(displayLanguage);
                await db.SaveChangesAsync();
            }

            var mediaReader = new MediaReader(indexDir);

            Dictionary<string, Bible.Alarm.AudioLinksHarvestor.Models.Publication> melodyMusicReleases;
            try
            {
                melodyMusicReleases = await mediaReader.GetMelodyMusicReleases();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if (melodyMusicReleases == null || melodyMusicReleases.Count == 0)
            {
                return;
            }

            foreach (var melodyMusicRelease in melodyMusicReleases)
            {
                _logger.Information("Seeding melody code {MelodyCode} music to database.", melodyMusicRelease.Key);

                var newMelodyMusic = new Bible.Alarm.Shared.Models.Media.Music.MelodyMusic
                    {
                        Code = melodyMusicRelease.Value.Code,
                        Name = melodyMusicRelease.Value.Name,
                        DisplayLanguage = displayLanguage
                    };

                SortedDictionary<int, Bible.Alarm.AudioLinksHarvestor.Models.Music.MusicTrack> tracks;
                try
                {
                    tracks = await mediaReader.GetMelodyMusicTracks(melodyMusicRelease.Key);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if (tracks == null || tracks.Count == 0)
                {
                    continue;
                }

                foreach (var track in tracks)
                {
                    var newTrack = new Bible.Alarm.Shared.Models.Media.Music.MusicTrack
                    {
                        Number = track.Value.Number,
                        Title = track.Value.Title,
                        Source = new Bible.Alarm.Shared.Models.Media.AudioSource
                        {
                            Url = track.Value.Url,
                            LookUpPath = track.Value.LookUpPath
                        }
                    };

                    newMelodyMusic.Tracks.Add(newTrack);
                }

                await db.MelodyMusic.AddAsync(newMelodyMusic);
                await db.SaveChangesAsync();
            }

        }

        private async Task SeedVocals(string indexDir)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
            if (displayLanguage == null)
            {
                displayLanguage = new Bible.Alarm.Shared.Models.Media.Language
                {
                    Code = "E",
                    Name = "English"
                };
                db.Languages.Add(displayLanguage);
                await db.SaveChangesAsync();
            }

            var mediaReader = new MediaReader(indexDir);

            Dictionary<string, Bible.Alarm.AudioLinksHarvestor.Models.Language> melodyLanguages;
            try
            {
                melodyLanguages = await mediaReader.GetVocalMusicLanguages();
            }
            catch (FileNotFoundException)
            {
                return;
            }
            catch (DirectoryNotFoundException)
            {
                return;
            }

            if (melodyLanguages == null || melodyLanguages.Count == 0)
            {
                return;
            }

            foreach (var language in melodyLanguages)
            {
                var newLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Code == language.Value.Code
                                                                        && x.Name == language.Value.Name);
                if (newLanguage == null)
                {
                    newLanguage = new Bible.Alarm.Shared.Models.Media.Language
                    {
                        Code = language.Value.Code,
                        Name = language.Value.Name
                    };
                }

                Dictionary<string, Bible.Alarm.AudioLinksHarvestor.Models.Publication> vocalMusicReleases;
                try
                {
                    vocalMusicReleases = await mediaReader.GetVocalMusicReleases(language.Value.Code);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if (vocalMusicReleases == null || vocalMusicReleases.Count == 0)
                {
                    continue;
                }

                foreach (var vocalMusicRelease in vocalMusicReleases)
                {
                    _logger.Information("Seeding song book {SongBookName} ({SongBookCode}) for language {LanguageCode}", vocalMusicRelease.Value.Name, vocalMusicRelease.Value.Code, language.Key);
                    
                    var newVocalMusic = new Bible.Alarm.Shared.Models.Media.Music.VocalMusic
                    {
                        Code = vocalMusicRelease.Value.Code,
                        Name = vocalMusicRelease.Value.Name,
                        DisplayLanguage = displayLanguage,
                        Language = newLanguage
                    };

                    SortedDictionary<int, Bible.Alarm.AudioLinksHarvestor.Models.Music.MusicTrack> tracks;
                    try
                    {
                        tracks = await mediaReader.GetVocalMusicTracks(language.Value.Code, vocalMusicRelease.Key);
                    }
                    catch (FileNotFoundException)
                    {
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }

                    if (tracks == null || tracks.Count == 0)
                    {
                        continue;
                    }

                    foreach (var track in tracks)
                    {
                        var newTrack = new Bible.Alarm.Shared.Models.Media.Music.MusicTrack
                        {
                            Number = track.Value.Number,
                            Title = track.Value.Title,
                            Source = new Bible.Alarm.Shared.Models.Media.AudioSource
                            {
                                Url = track.Value.Url,
                                LookUpPath = track.Value.LookUpPath
                            }
                        };

                        newVocalMusic.Tracks.Add(newTrack);
                    }

                    await db.VocalMusic.AddAsync(newVocalMusic);
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}

