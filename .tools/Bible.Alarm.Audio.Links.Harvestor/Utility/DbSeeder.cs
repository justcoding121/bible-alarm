using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bible.Alarm.Audio.Links.Harvestor.Models;
using Bible.Alarm.Audio.Links.Harvestor.Models.Bible;
using Bible.Alarm.Audio.Links.Harvestor.Models.Music;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Audio.Links.Harvestor.Utility
{
    public class DbSeeder
    {
        public async static Task Seed(string indexDir)
        {
            var zipDir = Path.Combine(new DirectoryInfo(indexDir).FullName, "db");

            if (!Directory.Exists(zipDir))
            {
                Directory.CreateDirectory(zipDir);
            }

            var dbPath = Path.Combine(zipDir, "mediaIndex.db");
            // Use default connection string without Cache=Shared to avoid file locking issues
            var connectionString = $"Data Source={dbPath}";
            var dbConfig = new DbContextOptionsBuilder<MediaDbContext>()
               .UseSqlite(connectionString).Options;

            MediaDbContext db = null;
            try
            {
                db = new MediaDbContext(dbConfig);
                await db.Database.MigrateAsync();

                var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
                if (displayLanguage == null)
                {
                    displayLanguage = new Bible.Alarm.Shared.Models.Media.Language
                    {
                        Code = "E",
                        Name = "English"
                    };
                }

                var mediaDir = Path.Combine(indexDir, "media");
                await seedBibleTranslations(mediaDir, db, displayLanguage);
                await seedMelodies(mediaDir, db, displayLanguage);
                await seedVocals(mediaDir, db, displayLanguage);
            }
            finally
            {
                if (db != null)
                {
                    try
                    {
                        // Save any pending changes first
                        await db.SaveChangesAsync();
                    }
                    catch
                    {
                        // Ignore errors during save
                    }

                    try
                    {
                        // Explicitly close the database connection before disposal
                        await db.Database.CloseConnectionAsync();
                    }
                    catch
                    {
                        // Ignore errors during close
                    }

                    try
                    {
                        await db.DisposeAsync();
                    }
                    catch
                    {
                        // Ignore errors during dispose
                    }
                    
                    db = null;
                }
                
                // Force garbage collection to ensure all database resources are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect(); // Second collection to catch finalizers
                
                // Additional delay to allow SQLite to fully release the file
                await Task.Delay(100);
            }
        }

        private async static Task seedBibleTranslations(string indexDir, MediaDbContext db, Bible.Alarm.Shared.Models.Media.Language displayLanguage)
        {
            var mediaReader = new MediaReader(indexDir);

            Dictionary<string, Bible.Alarm.Audio.Links.Harvestor.Models.Language> bibleLanguages;
            try
            {
                bibleLanguages = await mediaReader.GetBibleLanguages();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Warning: languages.json not found. No Bible translations to seed.");
                return;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Warning: Bible directory not found. No Bible translations to seed.");
                return;
            }

            if (bibleLanguages == null || bibleLanguages.Count == 0)
            {
                Console.WriteLine("Warning: No Bible languages found. Skipping Bible translation seeding.");
                return;
            }

            foreach (var language in bibleLanguages)
            {
                Console.WriteLine($"Seeding language code {language.Key} Bible audio links to database.");

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

                Dictionary<string, Bible.Alarm.Audio.Links.Harvestor.Models.Publication> translations;
                try
                {
                    translations = await mediaReader.GetBibleTranslations(language.Key);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"Warning: publications.json not found for language {language.Key}. Skipping language.");
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine($"Warning: Directory not found for language {language.Key}. Skipping language.");
                    continue;
                }

                if (translations == null || translations.Count == 0)
                {
                    Console.WriteLine($"Warning: No translations found for language {language.Key}. Skipping language.");
                    continue;
                }

                foreach (var translation in translations)
                {
                    SortedDictionary<int, Bible.Alarm.Audio.Links.Harvestor.Models.Bible.BibleBook> books;
                    try
                    {
                        books = await mediaReader.GetBibleBooks(language.Key, translation.Key);
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine($"Warning: books.json not found for language {language.Key}, translation {translation.Key}. Skipping.");
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        Console.WriteLine($"Warning: Directory not found for language {language.Key}, translation {translation.Key}. Skipping.");
                        continue;
                    }

                    if (books == null || books.Count == 0)
                    {
                        Console.WriteLine($"Warning: No books found for language {language.Key}, translation {translation.Key}. Skipping.");
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

                        SortedDictionary<int, Bible.Alarm.Audio.Links.Harvestor.Models.Bible.BibleChapter> chapters;
                        try
                        {
                            chapters = await mediaReader.GetBibleChapters(language.Key, translation.Key, book.Key);
                        }
                        catch (FileNotFoundException)
                        {
                            Console.WriteLine($"Warning: chapters.json not found for language {language.Key}, translation {translation.Key}, book {book.Key}. Skipping book.");
                            continue;
                        }
                        catch (DirectoryNotFoundException)
                        {
                            Console.WriteLine($"Warning: Directory not found for language {language.Key}, translation {translation.Key}, book {book.Key}. Skipping book.");
                            continue;
                        }

                        if (chapters == null || chapters.Count == 0)
                        {
                            Console.WriteLine($"Warning: No chapters found for language {language.Key}, translation {translation.Key}, book {book.Key}. Skipping book.");
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

        private async static Task seedMelodies(string indexDir, MediaDbContext db, Bible.Alarm.Shared.Models.Media.Language displayLanguage)
        {
            var mediaReader = new MediaReader(indexDir);

            Dictionary<string, Bible.Alarm.Audio.Links.Harvestor.Models.Publication> melodyMusicReleases;
            try
            {
                melodyMusicReleases = await mediaReader.GetMelodyMusicReleases();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Warning: Melody publications.json not found. No melodies to seed.");
                return;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Warning: Melody directory not found. No melodies to seed.");
                return;
            }

            if (melodyMusicReleases == null || melodyMusicReleases.Count == 0)
            {
                Console.WriteLine("Warning: No melody releases found. Skipping melody seeding.");
                return;
            }

            foreach (var melodyMusicRelease in melodyMusicReleases)
            {
                Console.WriteLine($"Seeding melody code {melodyMusicRelease.Key} music to database.");

                var newMelodyMusic = new Bible.Alarm.Shared.Models.Media.Music.MelodyMusic
                    {
                        Code = melodyMusicRelease.Value.Code,
                        Name = melodyMusicRelease.Value.Name,
                        DisplayLanguage = displayLanguage
                    };

                SortedDictionary<int, Bible.Alarm.Audio.Links.Harvestor.Models.Music.MusicTrack> tracks;
                try
                {
                    tracks = await mediaReader.GetMelodyMusicTracks(melodyMusicRelease.Key);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"Warning: tracks.json not found for melody {melodyMusicRelease.Key}. Skipping.");
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine($"Warning: Directory not found for melody {melodyMusicRelease.Key}. Skipping.");
                    continue;
                }

                if (tracks == null || tracks.Count == 0)
                {
                    Console.WriteLine($"Warning: No tracks found for melody {melodyMusicRelease.Key}. Skipping.");
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
                //db.Music
                await db.SaveChangesAsync();
            }

        }

        private async static Task seedVocals(string indexDir, MediaDbContext db, Bible.Alarm.Shared.Models.Media.Language displayLanguage)
        {
            var mediaReader = new MediaReader(indexDir);

            Dictionary<string, Bible.Alarm.Audio.Links.Harvestor.Models.Language> melodyLanguages;
            try
            {
                melodyLanguages = await mediaReader.GetVocalMusicLanguages();
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Warning: Vocal languages.json not found. No vocals to seed.");
                return;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Warning: Vocal directory not found. No vocals to seed.");
                return;
            }

            if (melodyLanguages == null || melodyLanguages.Count == 0)
            {
                Console.WriteLine("Warning: No vocal languages found. Skipping vocal seeding.");
                return;
            }

            foreach (var language in melodyLanguages)
            {
                Console.WriteLine($"Seeding language code {language.Key} vocals to database.");

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

                Dictionary<string, Bible.Alarm.Audio.Links.Harvestor.Models.Publication> vocalMusicReleases;
                try
                {
                    vocalMusicReleases = await mediaReader.GetVocalMusicReleases(language.Value.Code);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"Warning: publications.json not found for vocal language {language.Value.Code}. Skipping language.");
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine($"Warning: Directory not found for vocal language {language.Value.Code}. Skipping language.");
                    continue;
                }

                if (vocalMusicReleases == null || vocalMusicReleases.Count == 0)
                {
                    Console.WriteLine($"Warning: No vocal releases found for language {language.Value.Code}. Skipping language.");
                    continue;
                }

                foreach (var vocalMusicRelease in vocalMusicReleases)
                {
                    var newVocalMusic = new Bible.Alarm.Shared.Models.Media.Music.VocalMusic
                    {
                        Code = vocalMusicRelease.Value.Code,
                        Name = vocalMusicRelease.Value.Name,
                        DisplayLanguage = displayLanguage,
                        Language = newLanguage
                    };

                    SortedDictionary<int, Bible.Alarm.Audio.Links.Harvestor.Models.Music.MusicTrack> tracks;
                    try
                    {
                        tracks = await mediaReader.GetVocalMusicTracks(language.Value.Code, vocalMusicRelease.Key);
                    }
                    catch (FileNotFoundException)
                    {
                        Console.WriteLine($"Warning: tracks.json not found for language {language.Value.Code}, vocal {vocalMusicRelease.Key}. Skipping.");
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        Console.WriteLine($"Warning: Directory not found for language {language.Value.Code}, vocal {vocalMusicRelease.Key}. Skipping.");
                        continue;
                    }

                    if (tracks == null || tracks.Count == 0)
                    {
                        Console.WriteLine($"Warning: No tracks found for language {language.Value.Code}, vocal {vocalMusicRelease.Key}. Skipping.");
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

