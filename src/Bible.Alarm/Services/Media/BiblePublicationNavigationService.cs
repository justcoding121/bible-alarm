using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class BiblePublicationNavigationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IBiblePublicationNavigationService
{

    public async Task<bool> MoveToPreviousSectionAsync(BiblePublicationSchedule schedule)
    {
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextSection = await playlistService.GetPreviousBiblePublicationSection(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = nextSection.Value.Number;
            schedule.TrackNumber = 1;
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
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextSection = await playlistService.GetNextBiblePublicationSection(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = nextSection.Value.Number;
            schedule.TrackNumber = 1;
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
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var prevTrack = await playlistService.GetPreviousBiblePublicationTrack(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value,
                schedule.TrackNumber);

            if (prevTrack.Key == null || prevTrack.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = prevTrack.Key.Number;
            schedule.TrackNumber = prevTrack.Value.Number;
            schedule.FinishedDuration = TimeSpan.Zero;
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
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextTrack = await playlistService.GetNextBiblePublicationTrack(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value,
                schedule.TrackNumber);

            if (nextTrack.Key == null || nextTrack.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = nextTrack.Key.Number;
            schedule.TrackNumber = nextTrack.Value.Number;
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

