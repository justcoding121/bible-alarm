#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
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
                .Include(x => x.Category)
                .Include(x => x.Sections)
                    .ThenInclude(s => s.Tracks)
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

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // For dramas, use case-sensitive publication codes: "Dramas" or "DramaticBibleReadings"
            // For others (e.g., "gnj"), preserve exact case
            var lowerCode = publicationCode.ToLowerInvariant();
            var isDrama = PublicationTypeHelper.IsDrama(lowerCode);
            string publicationCodeForDb;
            if (isDrama)
            {
                publicationCodeForDb = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                    ? "Dramas"
                    : "DramaticBibleReadings";
            }
            else
            {
                publicationCodeForDb = publicationCode; // Preserve exact case (e.g., "gnj")
            }

            // Load publication with only non-sectioned tracks (tracks directly under publication, not under a section)
            var publication = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Tracks.Where(t => t.BiblePublicationSectionId == null))
                .Where(x => x.PublicationCode == publicationCodeForDb && x.Language != null && x.Language.LanguageCode == normalizedLanguageCode)
                .FirstOrDefaultAsync(cancellationToken);

            logger.Debug("GetByLanguageAndCodeWithTracksAsync: Loaded publication={PublicationName}, TracksCount={TracksCount} for language={LanguageCode}, code={PublicationCode} (dbCode={DbCode})",
                publication?.Name ?? "(null)", publication?.Tracks?.Count ?? 0, languageCode, publicationCode, publicationCodeForDb);

            return publication;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublication with Tracks. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, BiblePublication>> GetByLanguageCodeAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var query = dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.Language != null && x.Language.LanguageCode == languageCode);

            // Filter by category if provided
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryName == categoryName);
            }

            var publicationsList = await query.ToListAsync(cancellationToken);

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

    public async Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(string? categoryName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Use PublicationLanguage table for discovery - it's designed for this purpose
            // This table tracks which languages are available for each publication code in each category
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => x.Language != null);

            // Filter by category if provided
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryName == categoryName);
            }

            var publicationLanguagesCount = await query.CountAsync(cancellationToken);
            var distinctLanguages = await query
                .Select(x => x.Language!)
                .Distinct()
                .ToListAsync(cancellationToken);

            logger.Debug("BiblePublicationService.GetDistinctLanguagesAsync: Found {PublicationLanguageCount} PublicationLanguage entries across {LanguageCount} distinct languages",
                publicationLanguagesCount, distinctLanguages.Count);

            return distinctLanguages.ToDictionary(x => x.LanguageCode, x => x);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting distinct Languages from PublicationLanguages");
            throw;
        }
    }

    public async Task<List<string>> GetAvailablePublicationCodesAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Query PublicationLanguages table for discovery - shows all available publications
            // Include both publications WITH language and publications WITHOUT language (LanguageId == null)
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => (x.Language != null && x.Language.LanguageCode == normalizedLanguageCode) ||
                           (x.LanguageId == null));

            // Filter by category if provided
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryName == categoryName);
            }

            var publicationCodes = await query
                .Select(x => x.PublicationCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Remove duplicates by normalizing case for comparison, but preserve original case
            // For dramas, use case-sensitive codes: "Dramas", "DramaticBibleReadings" (preserve exact case)
            var uniqueCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in publicationCodes)
            {
                var lowerCode = code.ToLowerInvariant();
                // For dramas, normalize to correct case-sensitive format
                if (PublicationTypeHelper.IsDrama(lowerCode))
                {
                    var normalizedDramaCode = lowerCode.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                        ? "Dramas"
                        : "DramaticBibleReadings";
                    if (!uniqueCodes.ContainsKey(normalizedDramaCode))
                    {
                        uniqueCodes[normalizedDramaCode] = normalizedDramaCode;
                    }
                }
                else
                {
                    // For non-dramas, preserve original case (e.g., "gnj")
                    if (!uniqueCodes.ContainsKey(code))
                    {
                        uniqueCodes[code] = code;
                    }
                }
            }

            var result = uniqueCodes.Values.OrderBy(x => x).ToList();

            logger.Debug("GetAvailablePublicationCodesAsync: Found {Count} available publication codes (deduplicated from {OriginalCount}) for language={LanguageCode}, category={CategoryName}",
                result.Count, publicationCodes.Count, languageCode, categoryName ?? "all");

            return result;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting available publication codes from PublicationLanguages. LanguageCode={LanguageCode}, CategoryName={CategoryName}",
                languageCode, categoryName);
            throw;
        }
    }

    public async Task<string?> GetFirstPublicationCodeByOrderAsync(string languageCode, string? categoryName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var normalizedLanguageCode = languageCode.ToUpperInvariant();

            // Query PublicationLanguages table ordered by Id (first by ID order)
            var query = dbContext.PublicationLanguages
                .AsNoTracking()
                .Include(x => x.Language)
                .Include(x => x.Category)
                .Where(x => x.Language != null && x.Language.LanguageCode == normalizedLanguageCode);

            // Filter by category if provided
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                query = query.Where(x => x.Category != null && x.Category.CategoryName == categoryName);
            }

            // Order by Id to get the first publication by ID order
            var firstPublicationCode = await query
                .OrderBy(x => x.Id)
                .Select(x => x.PublicationCode)
                .FirstOrDefaultAsync(cancellationToken);

            // Normalize drama publication codes to prevent duplicates
            if (!string.IsNullOrEmpty(firstPublicationCode))
            {
                var normalized = firstPublicationCode.ToLowerInvariant();
                if (PublicationTypeHelper.IsDrama(normalized))
                {
                    firstPublicationCode = normalized.Equals("dramas", StringComparison.OrdinalIgnoreCase)
                        ? "Dramas"
                        : "DramaticBibleReadings";
                }
            }

            logger.Debug("GetFirstPublicationCodeByOrderAsync: Found first publication code={PublicationCode} for language={LanguageCode}, category={CategoryName}",
                firstPublicationCode ?? "(null)", languageCode, categoryName ?? "all");

            return firstPublicationCode;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting first publication code by order from PublicationLanguages. LanguageCode={LanguageCode}, CategoryName={CategoryName}",
                languageCode, categoryName);
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

