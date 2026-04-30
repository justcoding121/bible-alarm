#nullable enable
using System.Linq;
using Bible.Alarm.Services.Bootstrap.ScheduleStatePopulatorHelpers.LookupLoading;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Models.Media.Music;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IVocalMusicService? vocalMusicService;
    private readonly IServiceScopeFactory? scopeFactory;

    public LookupDataLoader(
        IBiblePublicationService? BiblePublicationService,
        IBiblePublicationSectionService? biblePublicationSectionService,
        IMediaService? mediaService,
        IVocalMusicService? vocalMusicService,
        IServiceScopeFactory? scopeFactory)
    {
        this.BiblePublicationService = BiblePublicationService;
        this.biblePublicationSectionService = biblePublicationSectionService;
        this.mediaService = mediaService;
        this.vocalMusicService = vocalMusicService;
        this.scopeFactory = scopeFactory;
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
                        key.LanguageCode, key.PublicationCode, key.SectionCode)
                    : null;
                return (Key: key, SectionName: sectionName);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading section {LanguageCode}/{PublicationCode}/{SectionCode}",
                    key.LanguageCode, key.PublicationCode, key.SectionCode);
                return (Key: key, SectionName: (string?)null);
            }
        }).ToList();

        var vocalLanguagesTask = mediaService != null && keys.VocalMusicLanguageCodes.Any()
            ? mediaService.GetVocalMusicLanguages()
            : Task.FromResult<Dictionary<string, Language>>(new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase));

        // For bootstrap schedule list display, we only need already-downloaded vocal publications
        // referenced by schedules. Avoid GetVocalMusicReleases (discovery + placeholders) to reduce DB work.
        var vocalReleasesTasks = keys.VocalMusicKeys.GroupBy(k => k.LanguageCode, StringComparer.OrdinalIgnoreCase).Select(async group =>
        {
            try
            {
                var releases = vocalMusicService != null
                    ? await vocalMusicService.GetByLanguageCodeAsync(group.Key)
                    : null;
                return (LanguageCode: group.Key, Releases: releases ?? new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "Error loading vocal music releases for {LanguageCode}", group.Key);
                return (LanguageCode: group.Key, Releases: new Dictionary<string, VocalMusic>(StringComparer.OrdinalIgnoreCase));
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
            : Task.FromResult<Dictionary<string, MelodyMusic>>(new Dictionary<string, MelodyMusic>(StringComparer.OrdinalIgnoreCase));

        // Batch load no-language publication/section/track display data for publications that
        // can't be resolved via language-bound services (e.g. melody discs like "iam" stored with LanguageId == null).
        var noLanguageDataTask = LoadNoLanguageLookupDataAsync(keys, publicationTasks);

        // Wait for all batch loads to complete in parallel
        await Task.WhenAll(
            Task.WhenAll(publicationTasks),
            Task.WhenAll(sectionTasks),
            vocalLanguagesTask,
            Task.WhenAll(vocalReleasesTasks),
            Task.WhenAll(vocalTracksTasks),
            Task.WhenAll(melodyTracksFlatTasks),
            Task.WhenAll(melodyTracksBySectionTasks),
            melodyReleasesTask,
            noLanguageDataTask);

        // Build lookup dictionaries
        var publicationsDict = publicationTasks
            .Where(t => t.Result.Publication != null)
            .ToDictionary(t => t.Result.Key, t => t.Result.Publication!, PublicationLookupKeyComparers.LanguagePublication.Instance);

        var sectionsDict = sectionTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.Result.SectionName))
            .ToDictionary(t => t.Result.Key, t => t.Result.SectionName!, PublicationLookupKeyComparers.LanguagePublicationSection.Instance);

        var vocalLanguagesDict = await vocalLanguagesTask;

        var vocalReleasesDict = (await Task.WhenAll(vocalReleasesTasks))
            .SelectMany(r => r.Releases.Select(kvp => new { Key = (r.LanguageCode, PublicationCode: kvp.Key), Release = kvp.Value }))
            .ToDictionary(x => x.Key, x => x.Release, PublicationLookupKeyComparers.LanguagePublication.Instance);

        var vocalTracksDict = (await Task.WhenAll(vocalTracksTasks))
            .ToDictionary(t => t.Key, t => t.Tracks, PublicationLookupKeyComparers.LanguagePublication.Instance);

        var melodyTracksFlatDict = (await Task.WhenAll(melodyTracksFlatTasks))
            .ToDictionary(t => t.PublicationCode, t => t.Tracks, StringComparer.OrdinalIgnoreCase);

        var melodyTracksBySectionDict = (await Task.WhenAll(melodyTracksBySectionTasks))
            .ToDictionary(t => t.Key, t => t.Tracks, PublicationLookupKeyComparers.PublicationSection.Instance);

        var melodyReleasesDict = await melodyReleasesTask;

        var noLanguageData = await noLanguageDataTask;

        return new LookupData(
            Publications: publicationsDict,
            Sections: sectionsDict,
            NoLanguagePublications: noLanguageData.Publications,
            NoLanguageSections: noLanguageData.Sections,
            NoLanguageTrackTitles: noLanguageData.TrackTitles,
            VocalLanguages: vocalLanguagesDict,
            VocalReleases: vocalReleasesDict,
            VocalTracks: vocalTracksDict,
            MelodyTracksFlat: melodyTracksFlatDict,
            MelodyTracksBySection: melodyTracksBySectionDict,
            MelodyReleases: melodyReleasesDict);
    }

    private async Task<NoLanguageLookupData> LoadNoLanguageLookupDataAsync(
        LookupDataCollector.LookupKeys keys,
        List<Task<((string LanguageCode, string PublicationCode) Key, BiblePublication? Publication)>> publicationTasks)
    {
        // If we don't have a scope factory, we can't query MediaDbContext here.
        if (scopeFactory == null)
        {
            return NoLanguageLookupData.Empty;
        }

        // Only attempt no-language lookups for publications that were requested but not found via language-bound queries.
        // This keeps the no-language queries tightly bounded and avoids scanning large parts of the media index.
        var missingPublicationCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in publicationTasks.Select(t => t.Result).Where(r =>
                     r.Publication == null && !string.IsNullOrWhiteSpace(r.Key.PublicationCode)))
        {
            missingPublicationCodes.Add(result.Key.PublicationCode);
        }

        if (missingPublicationCodes.Count == 0)
        {
            return NoLanguageLookupData.Empty;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

        // Load publication metadata (name + category) for LanguageId == null publications.
        var noLanguagePubsRaw = await db.BiblePublications
            .AsNoTracking()
            .Include(p => p.BiblePublicationCategories)
            .ThenInclude(bpc => bpc.Category)
            .Where(p => p.LanguageId == null && missingPublicationCodes.Contains(p.PublicationCode))
            .ToListAsync(CancellationToken.None);

        if (noLanguagePubsRaw.Count == 0)
        {
            return NoLanguageLookupData.Empty;
        }

        var noLanguagePubs = noLanguagePubsRaw
            .Select(p =>
            {
                var firstCat = p.BiblePublicationCategories.FirstOrDefault();
                return new
                {
                    p.Id,
                    p.PublicationCode,
                    p.Name,
                    CategoryId = firstCat?.CategoryId ?? 0,
                    CategoryCode = firstCat?.Category?.CategoryCode ?? JwSourceHelper.GetCategoryCode(p.PublicationCode) ?? string.Empty,
                    p.IsMusic
                };
            })
            .ToList();

        var pubIdByCode = noLanguagePubs
            .GroupBy(p => p.PublicationCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var pubCodeById = pubIdByCode.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

        var publicationsDict = noLanguagePubs
            .GroupBy(p => p.PublicationCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var first = g.First();
                    return new NoLanguagePublicationMeta(first.Name ?? string.Empty, first.CategoryId, first.CategoryCode, first.IsMusic);
                },
                StringComparer.OrdinalIgnoreCase);

        // Load sections (Id + SectionCode + Name) for all relevant no-language publications.
        var pubIds = pubIdByCode.Values.Distinct().ToList();

        var sections = await db.BiblePublicationSections
            .AsNoTracking()
            .Where(s => pubIds.Contains(s.BiblePublicationId))
            .Select(s => new
            {
                s.Id,
                s.BiblePublicationId,
                s.SectionCode,
                s.Name
            })
            .ToListAsync(CancellationToken.None);

        var sectionCodeById = sections
            .Where(s => s.Id > 0)
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => SectionCodeHelper.Normalize(g.First().SectionCode));

        var sectionsDict = new Dictionary<(string PublicationCode, string SectionCode), string>(PublicationLookupKeyComparers.PublicationSection.Instance);
        foreach (var s in sections)
        {
            if (!pubCodeById.TryGetValue(s.BiblePublicationId, out var pubCode))
            {
                continue;
            }

            var normalizedSectionCode = SectionCodeHelper.Normalize(s.SectionCode);
            if (string.IsNullOrWhiteSpace(normalizedSectionCode) || string.IsNullOrWhiteSpace(s.Name))
            {
                continue;
            }

            sectionsDict[(pubCode, normalizedSectionCode)] = s.Name;
        }

        // Load tracks (TrackCode + Title) for relevant no-language publications.
        // We load all tracks for these publications; no-language pubs are expected to be small (e.g. melody discs).
        var tracks = await db.BiblePublicationTracks
            .AsNoTracking()
            .Where(t => pubIds.Contains(t.BiblePublicationId))
            .Select(t => new
            {
                t.BiblePublicationId,
                t.BiblePublicationSectionId,
                t.TrackCode,
                t.Title
            })
            .ToListAsync(CancellationToken.None);

        var trackTitles = new Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string>(
            PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance);

        // Precompute the desired track keys so we only retain titles for tracks we actually need.
        var neededTrackKeys = new HashSet<(string PublicationCode, string? SectionCode, string TrackCode)>(
            PublicationLookupKeyComparers.PublicationNullableSectionTrack.Instance);
        foreach (var key in keys.BibleTrackKeys)
        {
            if (!missingPublicationCodes.Contains(key.PublicationCode))
            {
                continue;
            }

            neededTrackKeys.Add((key.PublicationCode, SectionCodeHelper.Normalize(key.SectionCode), key.TrackCode));
        }

        foreach (var t in tracks)
        {
            if (!pubCodeById.TryGetValue(t.BiblePublicationId, out var pubCode))
            {
                continue;
            }

            string? sectionCode = null;
            if (t.BiblePublicationSectionId.HasValue &&
                sectionCodeById.TryGetValue(t.BiblePublicationSectionId.Value, out var sc))
            {
                sectionCode = sc;
            }

            var normalizedTitle = t.Title?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                continue;
            }

            var trackKey = (pubCode, sectionCode, t.TrackCode);
            if (!neededTrackKeys.Contains(trackKey))
            {
                continue;
            }

            trackTitles[trackKey] = normalizedTitle;
        }

        return new NoLanguageLookupData(publicationsDict, sectionsDict, trackTitles);
    }

    public sealed record LookupData(
        Dictionary<(string LanguageCode, string PublicationCode), BiblePublication> Publications,
        Dictionary<(string LanguageCode, string PublicationCode, string SectionCode), string> Sections,
        Dictionary<string, NoLanguagePublicationMeta> NoLanguagePublications,
        Dictionary<(string PublicationCode, string SectionCode), string> NoLanguageSections,
        Dictionary<(string PublicationCode, string? SectionCode, string TrackCode), string> NoLanguageTrackTitles,
        Dictionary<string, Language> VocalLanguages,
        Dictionary<(string LanguageCode, string PublicationCode), VocalMusic> VocalReleases,
        Dictionary<(string LanguageCode, string PublicationCode), SortedDictionary<int, MusicTrack>> VocalTracks,
        Dictionary<string, SortedDictionary<int, MusicTrack>> MelodyTracksFlat,
        Dictionary<(string PublicationCode, string SectionCode), SortedDictionary<int, MusicTrack>> MelodyTracksBySection,
        Dictionary<string, MelodyMusic> MelodyReleases);

    public sealed record NoLanguagePublicationMeta(string Name, int CategoryId, string CategoryName, bool IsMusic);
}

