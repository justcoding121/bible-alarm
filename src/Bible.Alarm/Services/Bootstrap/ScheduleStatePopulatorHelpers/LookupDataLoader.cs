#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Loads all lookup data in parallel for batch processing.
/// </summary>
internal sealed class LookupDataLoader
{
    private readonly IBibleTranslationService? bibleTranslationService;
    private readonly IBibleBookService? bibleBookService;
    private readonly IMediaService? mediaService;

    public LookupDataLoader(
        IBibleTranslationService? bibleTranslationService,
        IBibleBookService? bibleBookService,
        IMediaService? mediaService)
    {
        this.bibleTranslationService = bibleTranslationService;
        this.bibleBookService = bibleBookService;
        this.mediaService = mediaService;
    }

    public async Task<LookupData> LoadAllAsync(LookupDataCollector.LookupKeys keys)
    {
        // Load all data in parallel
        var translationTasks = keys.TranslationKeys.Select(async key =>
        {
            try
            {
                var translation = bibleTranslationService != null 
                    ? await bibleTranslationService.GetByLanguageAndCodeWithBooksAsync(
                        key.LanguageCode, key.PublicationCode)
                    : null;
                return (Key: key, Translation: translation);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading translation {LanguageCode}/{PublicationCode}",
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Translation: (BibleTranslation?)null);
            }
        }).ToList();

        var bookTasks = keys.BookKeys.Select(async key =>
        {
            try
            {
                var bookName = bibleBookService != null
                    ? await bibleBookService.GetBookNameAsync(
                        key.LanguageCode, key.PublicationCode, key.BookNumber)
                    : null;
                return (Key: key, BookName: bookName);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading book {LanguageCode}/{PublicationCode}/{BookNumber}",
                    key.LanguageCode, key.PublicationCode, key.BookNumber);
                return (Key: key, BookName: (string?)null);
            }
        }).ToList();

        var vocalLanguagesTask = mediaService != null && keys.VocalMusicLanguageCodes.Any()
            ? mediaService.GetVocalMusicLanguages()
            : Task.FromResult<Dictionary<string, Language>>(new Dictionary<string, Language>());

        var vocalReleasesTasks = keys.VocalMusicKeys.GroupBy(k => k.LanguageCode).Select(async group =>
        {
            try
            {
                var releases = mediaService != null
                    ? await mediaService.GetVocalMusicReleases(group.Key)
                    : null;
                return (LanguageCode: group.Key, Releases: releases ?? new Dictionary<string, VocalMusic>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal music releases for {LanguageCode}", group.Key);
                return (LanguageCode: group.Key, Releases: new Dictionary<string, VocalMusic>());
            }
        }).ToList();

        var vocalTracksTasks = keys.VocalTrackKeys.Select(async key =>
        {
            try
            {
                var tracks = mediaService != null
                    ? await mediaService.GetVocalMusicTracks(key.LanguageCode, key.PublicationCode)
                    : null;
                return (Key: key, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal tracks {LanguageCode}/{PublicationCode}",
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        var melodyTracksTasks = keys.MelodyPublicationCodes.Select(async pubCode =>
        {
            try
            {
                var tracks = mediaService != null
                    ? await mediaService.GetMelodyMusicTracks(pubCode)
                    : null;
                return (PublicationCode: pubCode, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading melody tracks {PublicationCode}", pubCode);
                return (PublicationCode: pubCode, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        // Wait for all batch loads to complete in parallel
        await Task.WhenAll(
            Task.WhenAll(translationTasks),
            Task.WhenAll(bookTasks),
            vocalLanguagesTask,
            Task.WhenAll(vocalReleasesTasks),
            Task.WhenAll(vocalTracksTasks),
            Task.WhenAll(melodyTracksTasks));

        // Build lookup dictionaries
        var translationsDict = translationTasks
            .Where(t => t.Result.Translation != null)
            .ToDictionary(t => t.Result.Key, t => t.Result.Translation!);

        var booksDict = bookTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Result.BookName))
            .ToDictionary(t => t.Result.Key, t => t.Result.BookName!);

        var vocalLanguagesDict = await vocalLanguagesTask;

        var vocalReleasesDict = (await Task.WhenAll(vocalReleasesTasks))
            .SelectMany(r => r.Releases.Select(kvp => new { Key = (r.LanguageCode, PublicationCode: kvp.Key), Release = kvp.Value }))
            .ToDictionary(x => x.Key, x => x.Release);

        var vocalTracksDict = (await Task.WhenAll(vocalTracksTasks))
            .ToDictionary(t => t.Key, t => t.Tracks);

        var melodyTracksDict = (await Task.WhenAll(melodyTracksTasks))
            .ToDictionary(t => t.PublicationCode, t => t.Tracks);

        return new LookupData(
            Translations: translationsDict,
            Books: booksDict,
            VocalLanguages: vocalLanguagesDict,
            VocalReleases: vocalReleasesDict,
            VocalTracks: vocalTracksDict,
            MelodyTracks: melodyTracksDict);
    }

    public sealed record LookupData(
        Dictionary<(string LanguageCode, string PublicationCode), BibleTranslation> Translations,
        Dictionary<(string LanguageCode, string PublicationCode, int BookNumber), string> Books,
        Dictionary<string, Language> VocalLanguages,
        Dictionary<(string LanguageCode, string PublicationCode), VocalMusic> VocalReleases,
        Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>> VocalTracks,
        Dictionary<string, SortedDictionary<int, MusicTrack>> MelodyTracks);
}

