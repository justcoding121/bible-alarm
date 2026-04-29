#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Populates modal item counts for Bible and Music rows in the schedule editor.
/// </summary>
public static class ScheduleEffectsModalCountPopulator
{
    public static async Task<ScheduleStateItem?> TryPopulateModalCountsAsync(
        ScheduleStateItem currentSchedule,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var updated = currentSchedule.DeepClone();

            updated.BiblePublicationModalItemCount = await GetBiblePublicationModalItemCountAsync(db, updated);
            updated.BiblePublicationSectionModalItemCount = await GetBiblePublicationSectionModalItemCountAsync(db, updated);
            updated.BiblePublicationTrackModalItemCount = await GetBiblePublicationTrackModalItemCountAsync(scope.ServiceProvider, updated);

            if (updated.MusicEnabled && HasMusicConfigured(updated))
            {
                updated.MusicPublicationModalItemCount = await GetMusicPublicationModalItemCountAsync(db, updated);
                updated.MusicSectionModalItemCount = await GetMusicSectionModalItemCountAsync(db, updated);
            }
            else
            {
                updated.MusicPublicationModalItemCount ??= currentSchedule.MusicPublicationModalItemCount;
                updated.MusicSectionModalItemCount ??= currentSchedule.MusicSectionModalItemCount;
            }

            return updated;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "ScheduleEffects: Error populating modal counts. ScheduleId={ScheduleId}", currentSchedule.Id);
            return null;
        }
    }

    public static bool AreModalCountsEquivalent(ScheduleStateItem a, ScheduleStateItem b) =>
        a.BiblePublicationModalItemCount == b.BiblePublicationModalItemCount &&
        a.BiblePublicationSectionModalItemCount == b.BiblePublicationSectionModalItemCount &&
        a.BiblePublicationTrackModalItemCount == b.BiblePublicationTrackModalItemCount &&
        a.MusicPublicationModalItemCount == b.MusicPublicationModalItemCount &&
        a.MusicSectionModalItemCount == b.MusicSectionModalItemCount;

    private static bool HasMusicConfigured(ScheduleStateItem schedule) =>
        !string.IsNullOrWhiteSpace(schedule.MusicPublicationCode);

    private static async Task<int?> GetBiblePublicationTrackModalItemCountAsync(IServiceProvider serviceProvider, ScheduleStateItem schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.BiblePublicationCode))
        {
            return null;
        }

        var mediaService = serviceProvider.GetRequiredService<IMediaService>();
        var languageCode = schedule.BiblePublicationLanguageCode ?? string.Empty;
        var publicationCode = schedule.BiblePublicationCode;
        var normalizedSectionCode = SectionCodeHelper.Normalize(schedule.BiblePublicationSectionCode);

        try
        {
            var tracks = string.IsNullOrWhiteSpace(languageCode)
                ? await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, normalizedSectionCode)
                : await mediaService.GetBiblePublicationTracks(languageCode, publicationCode, normalizedSectionCode);
            if (tracks.Count == 0 && !string.IsNullOrWhiteSpace(languageCode))
            {
                tracks = await mediaService.GetBiblePublicationTracks(string.Empty, publicationCode, normalizedSectionCode);
            }

            return tracks.Count;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int?> GetBiblePublicationModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var categoryName = schedule.BiblePublicationCategoryName;
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var languageCode = schedule.BiblePublicationLanguageCode;
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryCode == categoryName);

        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            query = query.Where(pl =>
                (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
                pl.LanguageId == null);
        }
        else
        {
            query = query.Where(pl => pl.LanguageId == null);
        }

        var publicationCodes = await query
            .Select(pl => pl.PublicationCode)
            .ToListAsync();

        if (publicationCodes.Count == 0)
        {
            return 0;
        }

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in publicationCodes)
        {
            var lower = code.ToLowerInvariant();
            if (PublicationTypeHelper.IsDrama(lower))
            {
                unique.Add(lower.Equals("dramas", StringComparison.OrdinalIgnoreCase) ? AppConstants.Media.BiblePublicationCategoryDramas : AppConstants.Media.BiblePublicationCodeDramaticBibleReadings);
            }
            else
            {
                unique.Add(code);
            }
        }

        return unique.Count;
    }

    private static async Task<int?> GetBiblePublicationSectionModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var publicationCode = schedule.BiblePublicationCode;
        if (string.IsNullOrWhiteSpace(publicationCode) || !PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return 0;
        }

        var languageCode = schedule.BiblePublicationLanguageCode;
        var normalizedLanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.ToUpperInvariant();

        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        if (!string.IsNullOrWhiteSpace(normalizedLanguageCode))
        {
            query = query.Where(sl =>
                (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
                sl.LanguageId == null);
        }
        else
        {
            query = query.Where(sl => sl.LanguageId == null);
        }

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync();
    }

    private static async Task<int?> GetMusicPublicationModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryCode == AppConstants.Media.BiblePublicationCategoryMusic);

        query = query.Where(pl =>
            (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
            pl.LanguageId == null);

        return await query
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .CountAsync();
    }

    private static async Task<int?> GetMusicSectionModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var publicationCode = schedule.MusicPublicationCode;
        if (string.IsNullOrWhiteSpace(publicationCode) || !PublicationTypeHelper.HasSectionStructure(publicationCode))
        {
            return 0;
        }

        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();

        var query = db.SectionLanguages
            .AsNoTracking()
            .Where(sl => sl.PublicationCode == publicationCode);

        query = query.Where(sl =>
            (sl.Language != null && sl.Language.LanguageCode == normalizedLanguageCode) ||
            sl.LanguageId == null);

        return await query
            .Select(sl => sl.SectionCode)
            .Distinct()
            .CountAsync();
    }
}
