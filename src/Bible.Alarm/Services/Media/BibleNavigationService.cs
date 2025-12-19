using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media.Interfaces;
using Serilog;

namespace Bible.Alarm.Services.Media;

public class BibleNavigationService(
    ILogger logger,
    IServiceScopeFactory scopeFactory)
    : IBibleNavigationService, IDisposable
{
    private readonly ILogger _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private bool _isDisposed;

    public async Task<bool> MoveToPreviousBookAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null)
        {
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextBook = await playlistService.GetPreviousBibleBook(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.BookNumber);

            if (nextBook.Value == null)
            {
                return false;
            }

            schedule.BookNumber = nextBook.Value.Number;
            schedule.ChapterNumber = 1;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error moving to previous book");
            return false;
        }
    }

    public async Task<bool> MoveToNextBookAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null)
        {
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextBook = await playlistService.GetNextBibleBook(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.BookNumber);

            if (nextBook.Value == null)
            {
                return false;
            }

            schedule.BookNumber = nextBook.Value.Number;
            schedule.ChapterNumber = 1;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error moving to next book");
            return false;
        }
    }

    public async Task<bool> MoveToPreviousChapterAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null)
        {
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var prevChapter = await playlistService.GetPreviousBibleChapter(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.BookNumber,
                schedule.ChapterNumber);

            if (prevChapter.Key == null || prevChapter.Value == null)
            {
                return false;
            }

            schedule.BookNumber = prevChapter.Key.Number;
            schedule.ChapterNumber = prevChapter.Value.Number;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error moving to previous chapter");
            return false;
        }
    }

    public async Task<bool> MoveToNextChapterAsync(BibleReadingSchedule schedule)
    {
        if (schedule == null)
        {
            return false;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var playlistService = scope.ServiceProvider.GetRequiredService<IPlaylistService>();
            var nextChapter = await playlistService.GetNextBibleChapter(
                schedule.LanguageCode,
                schedule.PublicationCode,
                schedule.BookNumber,
                schedule.ChapterNumber);

            if (nextChapter.Key == null || nextChapter.Value == null)
            {
                return false;
            }

            schedule.BookNumber = nextChapter.Key.Number;
            schedule.ChapterNumber = nextChapter.Value.Number;
            schedule.FinishedDuration = TimeSpan.Zero;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error moving to next chapter");
            return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        // IServiceScopeFactory is a singleton, so don't dispose it
        // No event handlers to unsubscribe
    }
}

