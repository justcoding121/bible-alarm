#nullable enable

using Bible.Alarm.Common.Extensions;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Stores.Actions.Schedule;
using Bible.Alarm.Stores.Models;
using Fluxor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.Stores.Effects.ScheduleEffectsHelpers;

/// <summary>
/// Populates MusicSectionName in the state item when we have a track number but no section name for instrumental music.
/// </summary>
public static class ScheduleEffectsMusicSectionPopulator
{
    public static async Task PopulateMusicSectionNameForStateAsync(
        ScheduleStateItem scheduleStateItem,
        IDispatcher dispatcher,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        try
        {
            var isMelodyMusic = string.IsNullOrEmpty(scheduleStateItem.MusicLanguageCode);
            if (!isMelodyMusic ||
                string.IsNullOrWhiteSpace(scheduleStateItem.MusicTrackCode) ||
                string.IsNullOrWhiteSpace(scheduleStateItem.MusicPublicationCode))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(scheduleStateItem.MusicSectionName))
            {
                return;
            }

            if (!PublicationTypeHelper.HasSectionStructure(scheduleStateItem.MusicPublicationCode))
            {
                if (!string.IsNullOrWhiteSpace(scheduleStateItem.MusicSectionCode))
                {
                    var updatedSchedule = scheduleStateItem.DeepClone();
                    updatedSchedule.MusicSectionCode = null;
                    updatedSchedule.MusicSectionName = null;

                    logger.Debug("ScheduleEffects: Cleared MusicSectionCode and MusicSectionName for non-sectioned publication {PublicationCode}",
                        scheduleStateItem.MusicPublicationCode);

                    dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
                }
                return;
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var trackCode = scheduleStateItem.MusicTrackCode ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trackCode))
            {
                return;
            }

            var sectionInfo = await dbContext.BiblePublicationTracks
                .AsNoTracking()
                .Include(t => t.Section)
                    .ThenInclude(s => s!.BiblePublication)
                        .ThenInclude(p => p.Category)
                .Where(t => t.BiblePublicationSectionId != null
                    && t.TrackCode == trackCode
                    && t.Publication.PublicationCode == scheduleStateItem.MusicPublicationCode
                    && t.Publication.Category.CategoryName == "Music"
                    && t.Publication.LanguageId == null)
                .Select(t => new { t.Section!.SectionCode, t.Section.Name })
                .FirstOrDefaultAsync();

            if (sectionInfo != null && !string.IsNullOrWhiteSpace(sectionInfo.SectionCode))
            {
                var updatedSchedule = scheduleStateItem.DeepClone();
                updatedSchedule.MusicSectionCode = sectionInfo.SectionCode;
                updatedSchedule.MusicSectionName = sectionInfo.Name;

                logger.Debug("ScheduleEffects: Populated MusicSectionCode '{MusicSectionCode}' and MusicSectionName '{MusicSectionName}' from track {TrackCode}",
                    sectionInfo.SectionCode, sectionInfo.Name, scheduleStateItem.MusicTrackCode);

                dispatcher.Dispatch(new UpdateScheduleFromViewModelAction(updatedSchedule, musicUpdated: true, biblePublicationUpdated: false, shouldSave: false));
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "ScheduleEffects: Error populating music section name for state");
        }
    }
}
