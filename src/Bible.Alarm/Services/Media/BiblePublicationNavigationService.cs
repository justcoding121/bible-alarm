#nullable enable
using Bible.Alarm.Services.Media.Interfaces;
using Bible.Alarm.Shared.Models.Schedule;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class BiblePublicationNavigationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IBiblePublicationNavigationService
{
    /// <summary>
    /// Converts SectionCode (string) to int for media service calls.
    /// Tries to parse SectionCode to int.
    /// Returns 0 for null/empty (non-sectioned publications).
    /// </summary>
    private Task<int> ConvertSectionCodeToIntAsync(string? sectionCode, string languageCode, string publicationCode)
    {
        if (string.IsNullOrEmpty(sectionCode))
        {
            return Task.FromResult(0);
        }

        // Try to parse SectionCode directly to int
        if (int.TryParse(sectionCode, out var sectionIndex))
        {
            return Task.FromResult(sectionIndex);
        }

        // If parsing fails, SectionCode is not numeric (e.g., "gen" for Genesis)
        // For non-numeric section codes, return 0
        return Task.FromResult(0);
    }

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
            var sectionCode = await ConvertSectionCodeToIntAsync(
                schedule.SectionCode,
                schedule.LanguageCode,
                schedule.PublicationCode);
            
            var nextSection = await playlistService.GetPreviousBiblePublicationSection(
                schedule.LanguageCode,
                schedule.PublicationCode,
                sectionCode);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionCode = nextSection.Value.SectionCode;
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
        if (schedule == null || string.IsNullOrEmpty(schedule.SectionCode))
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var sectionCode = await ConvertSectionCodeToIntAsync(
                schedule.SectionCode,
                schedule.LanguageCode,
                schedule.PublicationCode);
            
            var nextSection = await playlistService.GetNextBiblePublicationSection(
                schedule.LanguageCode,
                schedule.PublicationCode,
                sectionCode);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionCode = nextSection.Value.SectionCode;
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
        if (schedule == null || string.IsNullOrEmpty(schedule.SectionCode))
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var sectionCode = await ConvertSectionCodeToIntAsync(
                schedule.SectionCode,
                schedule.LanguageCode,
                schedule.PublicationCode);
            
            var prevTrack = await playlistService.GetPreviousBiblePublicationTrack(
                schedule.LanguageCode,
                schedule.PublicationCode,
                sectionCode,
                schedule.TrackNumber);

            if (prevTrack.Key == null || prevTrack.Value == null)
            {
                return false;
            }

            schedule.SectionCode = prevTrack.Key?.SectionCode;
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
        if (schedule == null || string.IsNullOrEmpty(schedule.SectionCode))
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var sectionCode = await ConvertSectionCodeToIntAsync(
                schedule.SectionCode,
                schedule.LanguageCode,
                schedule.PublicationCode);
            
            var nextTrack = await playlistService.GetNextBiblePublicationTrack(
                schedule.LanguageCode,
                schedule.PublicationCode,
                sectionCode,
                schedule.TrackNumber);

            if (nextTrack.Key == null || nextTrack.Value == null)
            {
                return false;
            }

            schedule.SectionCode = nextTrack.Key?.SectionCode;
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

