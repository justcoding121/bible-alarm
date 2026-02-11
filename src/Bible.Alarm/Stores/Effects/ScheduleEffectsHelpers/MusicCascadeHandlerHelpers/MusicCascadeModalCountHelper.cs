#nullable enable

using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Models;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers.MusicCascadeHandlerHelpers;

/// <summary>
/// Modal item counts for Music publication/section modals. Mirror of ScheduleEffects GetMusic*ModalItemCountAsync.
/// </summary>
public static class MusicCascadeModalCountHelper
{
    /// <summary>
    /// Category is always "Music"; when MusicLanguageCode is null, effective language is "E".
    /// </summary>
    public static async Task<int?> GetMusicPublicationModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
    {
        var languageCode = schedule.MusicLanguageCode;
        var effectiveLanguageCode = string.IsNullOrEmpty(languageCode) ? AppConstants.Media.DefaultLanguageCode : languageCode;
        var normalizedLanguageCode = effectiveLanguageCode.ToUpperInvariant();

        var query = db.PublicationLanguages
            .AsNoTracking()
            .Where(pl => pl.Category != null && pl.Category.CategoryName == "Music");

        query = query.Where(pl =>
            (pl.Language != null && pl.Language.LanguageCode == normalizedLanguageCode) ||
            pl.LanguageId == null);

        return await query
            .Select(pl => pl.PublicationCode)
            .Distinct()
            .CountAsync();
    }

    /// <summary>
    /// When MusicLanguageCode is null, effective language is "E".
    /// </summary>
    public static async Task<int?> GetMusicSectionModalItemCountAsync(MediaDbContext db, ScheduleStateItem schedule)
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
