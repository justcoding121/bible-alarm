#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing BiblePublicationSection database operations.
/// </summary>
public sealed class BiblePublicationSectionService(IServiceScopeFactory scopeFactory, ILogger logger) : IBiblePublicationSectionService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;

    public async Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, string sectionCode, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                return null;
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Where(x => x.BiblePublication.PublicationCode == publicationCode
                            && x.BiblePublication.Language != null
                            && x.BiblePublication.Language.LanguageCode == languageCode
                            && x.SectionCode == sectionCode)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                "Error getting BiblePublicationSection name. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            throw;
        }
    }

    public async Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Load sections, then filter/order by SectionCode
            var sections = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                .Where(x => x.Language != null && x.Language.LanguageCode == languageCode && x.PublicationCode == publicationCode)
                .SelectMany(x => x.Sections)
                .ToListAsync(cancellationToken);

            // Keep section codes as strings end-to-end.
            // Only numeric parsing should happen inside the comparer (ordering).
            var sectionsByCode = new Dictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in sections.OrderBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase))
            {
                var normalized = SectionCodeHelper.Normalize(section.SectionCode);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                if (!sectionsByCode.TryAdd(normalized, section))
                {
                    logger.Warning(
                        "GetSectionsByPublicationAsync: Duplicate section code {SectionCode} found for language={LanguageCode}, publication={PublicationCode}. Keeping first occurrence.",
                        normalized,
                        languageCode,
                        publicationCode);
                }
            }

            return new SortedDictionary<string, BiblePublicationSection>(sectionsByCode, SectionCodeHelper.SectionCodeComparer);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationSections by publication. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<BiblePublicationSection?> GetSectionAsync(string languageCode, string publicationCode, string sectionCode, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sectionCode))
            {
                return null;
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Where(x => x.BiblePublication.PublicationCode == publicationCode
                            && x.BiblePublication.Language != null
                            && x.BiblePublication.Language.LanguageCode == languageCode
                            && x.SectionCode == sectionCode)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex,
                "Error getting BiblePublicationSection. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionCode={SectionCode}",
                languageCode, publicationCode, sectionCode);
            throw;
        }
    }

    public async Task<SortedDictionary<string, BiblePublicationSection>> GetSectionsByPublicationWithoutLanguageAsync(string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Load sections for publications without language (LanguageId=null)
            // This is data-driven - works for any publication with LanguageId=null, not just Music
            var sections = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.Sections)
                .Where(x => x.LanguageId == null
                    && x.PublicationCode == publicationCode)
                .SelectMany(x => x.Sections)
                .ToListAsync(cancellationToken);

            // Keep section codes as strings end-to-end.
            var sectionsByCode = new Dictionary<string, BiblePublicationSection>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in sections.OrderBy(s => s.SectionCode, StringComparer.OrdinalIgnoreCase))
            {
                var normalized = SectionCodeHelper.Normalize(section.SectionCode);
                if (string.IsNullOrEmpty(normalized))
                {
                    continue;
                }

                if (!sectionsByCode.TryAdd(normalized, section))
                {
                    logger.Warning(
                        "GetSectionsByPublicationWithoutLanguageAsync: Duplicate section code {SectionCode} found for publication={PublicationCode}. Keeping first occurrence.",
                        normalized,
                        publicationCode);
                }
            }

            return new SortedDictionary<string, BiblePublicationSection>(sectionsByCode, SectionCodeHelper.SectionCodeComparer);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting sections for publication without language. PublicationCode={PublicationCode}",
                publicationCode);
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

