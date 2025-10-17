using Bible.Alarm.Audio.Links.Harvestor.Utility;
using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Models;
using Bible.Alarm.Models.Enums;
using Bible.Alarm.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Bible.Alarm.Audio.Links.Harvestor
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

            var dbConfig = new DbContextOptionsBuilder<MediaDbContext>()
               .UseSqlite($"Data Source={Path.Combine(zipDir, "mediaIndex.db")}").Options;

            using (var db = new MediaDbContext(dbConfig))
            {
                db.Database.Migrate();

                var displayLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Name == "English" && x.Code == "E");
                if (displayLanguage == null)
                {
                    displayLanguage = new Language()
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
        }

        private async static Task seedBibleTranslations(string indexDir, MediaDbContext db, Language displayLanguage)
        {
            var mediaReader = new MediaReader(indexDir);

            var bibleLanguages = await mediaReader.GetBibleLanguages();

            foreach (var language in bibleLanguages)
            {
                Console.WriteLine($"Seeding language code {language.Key} Bible audio links to database.");

                var newLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Code == language.Value.Code
                                                                        && x.Name == language.Value.Name);
                if (newLanguage == null)
                {
                    newLanguage = new Language()
                    {
                        Code = language.Value.Code,
                        Name = language.Value.Name
                    };
                }

                var translations = await mediaReader.GetBibleTranslations(language.Key);

                foreach (var translation in translations)
                {
                    var bibleTranslation = new BibleTranslation()
                    {
                        Name = translation.Value.Name,
                        Code = translation.Value.Code,
                        Language = newLanguage,
                        DisplayLanguage = displayLanguage
                    };

                    var books = await mediaReader.GetBibleBooks(language.Key, translation.Key);

                    foreach (var book in books)
                    {
                        var newBook = new BibleBook()
                        {
                            Name = book.Value.Name,
                            Number = book.Value.Number
                        };

                        bibleTranslation.Books.Add(newBook);

                        var chapters = await mediaReader.GetBibleChapters(language.Key, translation.Key, book.Key);

                        foreach (var chapter in chapters)
                        {
                            string lookUpPath;

                            lookUpPath = $"?output=json&pub={bibleTranslation.Code}" +
                                             $"&fileformat=MP3&langwritten={bibleTranslation.Language.Code}" +
                                             $"&txtCMSLang=E&booknum={newBook.Number}&track={chapter.Value.Number}";


                            var newChapter = new BibleChapter()
                            {
                                Number = chapter.Value.Number,
                                Source = new AudioSource()
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

        private async static Task seedMelodies(string indexDir, MediaDbContext db, Language displayLanguage)
        {
            var mediaReader = new MediaReader(indexDir);

            var melodyMusicReleases = await mediaReader.GetMelodyMusicReleases();

            foreach (var melodyMusicRelease in melodyMusicReleases)
            {
                Console.WriteLine($"Seeding melody code {melodyMusicRelease.Key} music to database.");

                var newMelodyMusic = new MelodyMusic()
                {
                    Code = melodyMusicRelease.Value.Code,
                    Name = melodyMusicRelease.Value.Name,
                    DisplayLanguage = displayLanguage
                };

                var tracks = await mediaReader.GetMelodyMusicTracks(melodyMusicRelease.Key);

                foreach (var track in tracks)
                {
                    var newTrack = new MusicTrack()
                    {
                        Number = track.Value.Number,
                        Title = track.Value.Title,
                        Source = new AudioSource()
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

        private async static Task seedVocals(string indexDir, MediaDbContext db, Language displayLanguage)
        {
            var mediaReader = new MediaReader(indexDir);

            var melodyLanguages = await mediaReader.GetVocalMusicLanguages();

            foreach (var language in melodyLanguages)
            {
                Console.WriteLine($"Seeding language code {language.Key} vocals to database.");

                var newLanguage = await db.Languages.FirstOrDefaultAsync(x => x.Code == language.Value.Code
                                                                        && x.Name == language.Value.Name);
                if (newLanguage == null)
                {
                    newLanguage = new Language()
                    {
                        Code = language.Value.Code,
                        Name = language.Value.Name
                    };
                }

                var vocalMusicReleases = await mediaReader.GetVocalMusicReleases(language.Value.Code);

                foreach (var vocalMusicRelease in vocalMusicReleases)
                {
                    var newVocalMusic = new VocalMusic()
                    {
                        Code = vocalMusicRelease.Value.Code,
                        Name = vocalMusicRelease.Value.Name,
                        DisplayLanguage = displayLanguage,
                        Language = newLanguage
                    };

                    var tracks = await mediaReader.GetVocalMusicTracks(language.Value.Code, vocalMusicRelease.Key);

                    foreach (var track in tracks)
                    {
                        var newTrack = new MusicTrack()
                        {
                            Number = track.Value.Number,
                            Title = track.Value.Title,
                            Source = new AudioSource()
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

