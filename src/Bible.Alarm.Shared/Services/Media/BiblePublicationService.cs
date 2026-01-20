#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BiblePublication database operations.
/// </summary>
public sealed class BiblePublicationService(IServiceScopeFactory scopeFactory, ILogger logger) : IBiblePublicationService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    public async Task<BiblePublication?> GetByLanguageAndCodeWithSectionsAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                .Where(x => x.PublicationCode == publicationCode && x.Language != null && x.Language.LanguageCode == languageCode)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublication with Sections. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<BiblePublication?> GetByLanguageAndCodeWithTracksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Load publication with only non-sectioned tracks (tracks directly under publication, not under a section)
            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.PublicationCode == publicationCode && x.Language != null && x.Language.LanguageCode == languageCode)
                .FirstOrDefaultAsync(cancellationToken);

            logger.Debug("GetByLanguageAndCodeWithTracksAsync: Loaded publication={PublicationName}, TracksCount={TracksCount} for language={LanguageCode}, code={PublicationCode}",
                publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0, languageCode, publicationCode);

            return publication;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublication with Tracks. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var publicationsList = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language != null && x.Language.LanguageCode == languageCode)
                .ToListAsync(cancellationToken);

            logger.Debug("GetByLanguageCodeAsync: Found {PublicationCount} publications for language={LanguageCode}",
                publicationsList.Count, languageCode);

            // Handle potential duplicates gracefully - use first occurrence
            var result = new Dictionary<string, BiblePublication>();
            foreach (var publication in publicationsList)
            {
                logger.Debug("GetByLanguageCodeAsync: Publication code={Code}, name={Name}",
                    publication.PublicationCode, publication.Name);
                if (!result.ContainsKey(publication.PublicationCode))
                {
                    result[publication.PublicationCode] = publication;
                }
                else
                {
                    logger.Warning("Duplicate BiblePublication entry found. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}", languageCode, publication.PublicationCode);
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublications by language code. LanguageCode={LanguageCode}", languageCode);
            throw;
        }
    }

    public async Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var biblePublicationsCount = await dbContext.BiblePublications.CountAsync(cancellationToken);
            var distinctLanguages = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language != null)
                .Select(x => x.Language!)
                .Distinct()
                .ToListAsync(cancellationToken);

            logger.Information("BiblePublicationService.GetDistinctLanguagesAsync: Found {PublicationCount} Bible publications across {LanguageCount} distinct languages: {LanguageCodes}",
                biblePublicationsCount, distinctLanguages.Count, string.Join(", ", distinctLanguages.Select(l => $"{l.LanguageCode}:{l.Name}")));

            return distinctLanguages.ToDictionary(x => x.LanguageCode, x => x);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting distinct Languages from BiblePublications");
            throw;
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

