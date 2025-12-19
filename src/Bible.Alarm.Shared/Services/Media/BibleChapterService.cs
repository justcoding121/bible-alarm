#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BibleChapter database operations.
/// </summary>
public class BibleChapterService(IServiceScopeFactory scopeFactory, ILogger logger) : IBibleChapterService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;

    public async Task<SortedDictionary<int, BibleChapter>> GetChaptersByBookAsync(string languageCode, string publicationCode, int bookNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var chapters = await dbContext.BibleTranslations
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);

            return new SortedDictionary<int, BibleChapter>(chapters.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting BibleChapters by book. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, BookNumber={BookNumber}",
                languageCode, publicationCode, bookNumber);
            throw;
        }
    }

    public async Task<BibleChapter?> GetChapterAsync(string languageCode, string publicationCode, int bookNumber, int chapterNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BibleTranslations
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .Where(x => x.Number == chapterNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting BibleChapter. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, BookNumber={BookNumber}, ChapterNumber={ChapterNumber}",
                languageCode, publicationCode, bookNumber, chapterNumber);
            throw;
        }
    }

    public async Task UpdateChapterUrlAsync(string languageCode, string publicationCode, int bookNumber, int chapterNumber, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var chapter = await dbContext.BibleTranslations
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Books)
                .Where(x => x.Number == bookNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .Where(x => x.Number == chapterNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (chapter?.Source != null)
            {
                chapter.Source.Url = url;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating BibleChapter URL. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, BookNumber={BookNumber}, ChapterNumber={ChapterNumber}",
                languageCode, publicationCode, bookNumber, chapterNumber);
            throw;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        try
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error during cancellation token source disposal in BibleChapterService");
        }
    }
}

