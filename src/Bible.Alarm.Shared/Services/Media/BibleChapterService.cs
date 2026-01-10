#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.Bible;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BiblePublicationChapter database operations.
/// </summary>
public sealed class BibleChapterService(IServiceScopeFactory scopeFactory, ILogger logger) : IBibleChapterService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    public async Task<SortedDictionary<int, BiblePublicationChapter>> GetChaptersBySectionAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var chapters = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);

            return new SortedDictionary<int, BiblePublicationChapter>(chapters.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationChapters by section. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
                languageCode, publicationCode, sectionNumber);
            throw;
        }
    }

    public async Task<BiblePublicationChapter?> GetChapterAsync(string languageCode, string publicationCode, int sectionNumber, int chapterNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .Where(x => x.Number == chapterNumber)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationChapter. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}, ChapterNumber={ChapterNumber}",
                languageCode, publicationCode, sectionNumber, chapterNumber);
            throw;
        }
    }

    public async Task UpdateChapterUrlAsync(string languageCode, string publicationCode, int sectionNumber, int chapterNumber, string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var chapter = await dbContext.BiblePublications
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
                .SelectMany(x => x.Chapters)
                .Include(x => x.Source)
                .ThenInclude(s => s!.BaseUrlEntity)
                .Where(x => x.Number == chapterNumber)
                .FirstOrDefaultAsync(cancellationToken);

            if (chapter?.Source != null)
            {
                // Extract path from the new URL and update UrlPath
                var (baseUrl, urlPath) = ExtractBaseUrlAndPath(url);
                chapter.Source.UrlPath = urlPath;
                
                // Update BaseUrl if it changed (rare, but handle it)
                if (chapter.Source.BaseUrlEntity.BaseUrl != baseUrl)
                {
                    var existingBaseUrl = await dbContext.AudioSourceBaseUrls
                        .FirstOrDefaultAsync(x => x.BaseUrl == baseUrl, cancellationToken);
                    if (existingBaseUrl != null)
                    {
                        chapter.Source.BaseUrlEntity = existingBaseUrl;
                    }
                    else
                    {
                        chapter.Source.BaseUrlEntity = new AudioSourceBaseUrl { BaseUrl = baseUrl };
                    }
                }
                
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error updating BiblePublicationChapter URL. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}, ChapterNumber={ChapterNumber}",
                languageCode, publicationCode, sectionNumber, chapterNumber);
            throw;
        }
    }

    private static (string BaseUrl, string UrlPath) ExtractBaseUrlAndPath(string fullUrl)
    {
        if (string.IsNullOrEmpty(fullUrl))
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            var uri = new Uri(fullUrl);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var urlPath = uri.PathAndQuery;
            return (baseUrl, urlPath);
        }
        catch (UriFormatException)
        {
            return (string.Empty, fullUrl);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
    }
}

