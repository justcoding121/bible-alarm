#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers;

/// <summary>
/// Loads all lookup data in parallel for batch processing.
/// </summary>
internal sealed class LookupDataLoader
{
    private readonly IBiblePublicationService? BiblePublicationService;
    private readonly IBiblePublicationSectionService? biblePublicationSectionService;
    private readonly IMediaService? mediaService;

    public LookupDataLoader(
        IBiblePublicationService? BiblePublicationService,
        IBiblePublicationSectionService? biblePublicationSectionService,
        IMediaService? mediaService)
    {
        this.BiblePublicationService = BiblePublicationService;
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.mediaService = mediaService;
    }

    public async Task<LookupData> LoadAllAsync(LookupDataCollector.LookupKeys keys)
    {
        // Load all data in parallel
        // For drama/video publications (non-sectioned), load tracks instead of sections
        var publicationTasks = keys.PublicationKeys.Select(async key =>
        {
            try
            {
                BiblePublication? publication = null;
                if (BiblePublicationService != null)
                {
                    // Use GetByLanguageAndCodeWithTracksAsync for drama/video publications
                    // Use GetByLanguageAndCodeWithSectionsAsync for traditional Bible (sectioned)
                    var hasSectionStructure = PublicationTypeHelper.HasSectionStructure(key.PublicationCode);
                    publication = hasSectionStructure
                        ? await BiblePublicationService.GetByLanguageAndCodeWithSectionsAsync(key.LanguageCode, key.PublicationCode)
                        : await BiblePublicationService.GetByLanguageAndCodeWithTracksAsync(key.LanguageCode, key.PublicationCode);
                }
                return (Key: key, Publication: publication);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading publication {LanguageCode}/{PublicationCode}",
                    key.LanguageCode, key.PublicationCode);
                return (Key: key, Publication: (BiblePublication?)null);
            }
        }).ToList();

        var sectionTasks = keys.SectionKeys.Select(async key =>
        {
            try
            {
                var sectionName = biblePublicationSectionService != null
                    ? await biblePublicationSectionService.GetSectionNameAsync(
                        key.LanguageCode, key.PublicationCode, key.SectionNumber)
                    : null;
                return (Key: key, SectionName: sectionName);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading section {LanguageCode}/{PublicationCode}/{SectionNumber}",
                    key.LanguageCode, key.PublicationCode, key.SectionNumber);
                return (Key: key, SectionName: (string?)null);
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

        // Melody tracks:
        // - Flat melody publications: keyed only by PublicationCode
        // - Sectioned melody publications (e.g. "iam"): keyed by (PublicationCode, SectionCode) to avoid ambiguity
        var melodyTracksFlatTasks = keys.MelodyPublicationCodes.Select(async pubCode =>
        {
            try
            {
                var tracks = mediaService != null && !PublicationTypeHelper.HasSectionStructure(pubCode)
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

        var melodyTracksBySectionTasks = keys.MelodySectionKeys.Select(async key =>
        {
            try
            {
                var tracks = mediaService != null
                    ? await mediaService.GetMelodyMusicTracksBySection(key.PublicationCode, key.SectionCode)
                    : null;
                return (Key: key, Tracks: tracks ?? new SortedDictionary<int, MusicTrack>());
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading melody tracks {PublicationCode}/{SectionCode}", key.PublicationCode, key.SectionCode);
                return (Key: key, Tracks: new SortedDictionary<int, MusicTrack>());
            }
        }).ToList();

        // Load melody releases for publication names
        var melodyReleasesTask = mediaService != null && keys.MelodyPublicationCodes.Any()
            ? mediaService.GetMelodyMusicReleases()
            : Task.FromResult<Dictionary<string, MelodyMusic>>(new Dictionary<string, MelodyMusic>());

        // Wait for all batch loads to complete in parallel
        await Task.WhenAll(
            Task.WhenAll(publicationTasks),
            Task.WhenAll(sectionTasks),
            vocalLanguagesTask,
            Task.WhenAll(vocalReleasesTasks),
            Task.WhenAll(vocalTracksTasks),
            Task.WhenAll(melodyTracksFlatTasks),
            Task.WhenAll(melodyTracksBySectionTasks),
            melodyReleasesTask);

        // Build lookup dictionaries
        var publicationsDict = publicationTasks
            .Where(t => t.Result.Publication != null)
            .ToDictionary(t => t.Result.Key, t => t.Result.Publication!);

        var sectionsDict = sectionTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Result.SectionName))
            .ToDictionary(t => t.Result.Key, t => t.Result.SectionName!);

        var vocalLanguagesDict = await vocalLanguagesTask;

        var vocalReleasesDict = (await Task.WhenAll(vocalReleasesTasks))
            .SelectMany(r => r.Releases.Select(kvp => new { Key = (r.LanguageCode, PublicationCode: kvp.Key), Release = kvp.Value }))
            .ToDictionary(x => x.Key, x => x.Release);

        var vocalTracksDict = (await Task.WhenAll(vocalTracksTasks))
            .ToDictionary(t => t.Key, t => t.Tracks);

        var melodyTracksFlatDict = (await Task.WhenAll(melodyTracksFlatTasks))
            .ToDictionary(t => t.PublicationCode, t => t.Tracks);

        var melodyTracksBySectionDict = (await Task.WhenAll(melodyTracksBySectionTasks))
            .ToDictionary(t => t.Key, t => t.Tracks);

        var melodyReleasesDict = await melodyReleasesTask;

        return new LookupData(
            Publications: publicationsDict,
            Sections: sectionsDict,
            VocalLanguages: vocalLanguagesDict,
            VocalReleases: vocalReleasesDict,
            VocalTracks: vocalTracksDict,
            MelodyTracksFlat: melodyTracksFlatDict,
            MelodyTracksBySection: melodyTracksBySectionDict,
            MelodyReleases: melodyReleasesDict);
    }

    public sealed record LookupData(
        Dictionary<(string LanguageCode, string PublicationCode), BiblePublication> Publications,
        Dictionary<(string LanguageCode, string PublicationCode, int SectionNumber), string> Sections,
        Dictionary<string, Language> VocalLanguages,
        Dictionary<(string LanguageCode, string PublicationCode), VocalMusic> VocalReleases,
        Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>> VocalTracks,
        Dictionary<string, SortedDictionary<int, MusicTrack>> MelodyTracksFlat,
        Dictionary<(string PublicationCode, string SectionCode), SortedDictionary<int, MusicTrack>> MelodyTracksBySection,
        Dictionary<string, MelodyMusic> MelodyReleases);
}

