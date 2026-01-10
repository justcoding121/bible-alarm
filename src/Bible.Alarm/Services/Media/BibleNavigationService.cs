using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public sealed class BibleNavigationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IBibleNavigationService
{

    public async Task<bool> MoveToPreviousSectionAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextSection = await playlistService.GetPreviousBibleSection(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = nextSection.Value.Number;
            schedule.ChapterNumber = 1;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to previous section");
            return false;
        }
    }

    public async Task<bool> MoveToNextSectionAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextSection = await playlistService.GetNextBibleSection(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value);

            if (nextSection.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = nextSection.Value.Number;
            schedule.ChapterNumber = 1;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to next section");
            return false;
        }
    }

    public async Task<bool> MoveToPreviousChapterAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var prevChapter = await playlistService.GetPreviousBibleChapter(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value,
                schedule.ChapterNumber);

            if (prevChapter.Key == null || prevChapter.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = prevChapter.Key.Number;
            schedule.ChapterNumber = prevChapter.Value.Number;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to previous chapter");
            return false;
        }
    }

    public async Task<bool> MoveToNextChapterAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null || !schedule.SectionNumber.HasValue)
        {
            return false;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextChapter = await playlistService.GetNextBibleChapter(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.SectionNumber.Value,
                schedule.ChapterNumber);

            if (nextChapter.Key == null || nextChapter.Value == null)
            {
                return false;
            }

            schedule.SectionNumber = nextChapter.Key.Number;
            schedule.ChapterNumber = nextChapter.Value.Number;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error moving to next chapter");
            return false;
        }
    }

}

