#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class BiblePublicationNavigationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IBiblePublicationNavigationService
{
    public async Task<bool> MoveToPreviousSectionAsync(BiblePublicationSchedule schedule)
    {
        if (schedule == null || string.IsNullOrEmpty(schedule.SectionCode))
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            
            var languageCode = schedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
            var nextSection = await playlistService.GetPreviousBiblePublicationSection(
                languageCode,
                schedule.PublicationCode,
                schedule.SectionCode);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionCode = nextSection.Value.SectionCode;
            schedule.TrackCode = "1";
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to previous section");
            return false;
        }
    }

    public async Task<bool> MoveToNextSectionAsync(BiblePublicationSchedule schedule)
    {
        if (schedule == null || string.IsNullOrEmpty(schedule.SectionCode))
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            
            var languageCode = schedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
            var nextSection = await playlistService.GetNextBiblePublicationSection(
                languageCode,
                schedule.PublicationCode,
                schedule.SectionCode);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionCode = nextSection.Value.SectionCode;
            schedule.TrackCode = "1";
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to next section");
            return false;
        }
    }

    public async Task<bool> MoveToPreviousTrackAsync(BiblePublicationSchedule schedule)
    {
        if (schedule == null)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            
            var languageCode = schedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
            var prevTrack = await playlistService.GetPreviousBiblePublicationTrack(
                languageCode,
                schedule.PublicationCode,
                schedule.SectionCode,
                schedule.TrackCode);

            if (prevTrack.Key == null || prevTrack.Value == null)
            {
                return false;
            }

            schedule.SectionCode = prevTrack.Key?.SectionCode;
            schedule.TrackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(prevTrack.Value);
            schedule.FinishedDuration = TimeSpan.Zero;
            if (prevTrack.Value.Publication != null)
            {
                schedule.PublicationCode = prevTrack.Value.Publication.PublicationCode;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to previous track");
            return false;
        }
    }

    public async Task<bool> MoveToNextTrackAsync(BiblePublicationSchedule schedule)
    {
        if (schedule == null)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            
            var languageCode = schedule.LanguageCode ?? AppConstants.Media.DefaultLanguageCode;
            var nextTrack = await playlistService.GetNextBiblePublicationTrack(
                languageCode,
                schedule.PublicationCode,
                schedule.SectionCode,
                schedule.TrackCode);

            if (nextTrack.Key == null || nextTrack.Value == null)
            {
                return false;
            }

            schedule.SectionCode = nextTrack.Key?.SectionCode;
            schedule.TrackCode = Bible.Alarm.Shared.Helpers.TrackCodeHelper.GetFromTrack(nextTrack.Value);
            if (nextTrack.Value.Publication != null)
            {
                schedule.PublicationCode = nextTrack.Value.Publication.PublicationCode;
            }
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to next track");
            return false;
        }
    }

}

