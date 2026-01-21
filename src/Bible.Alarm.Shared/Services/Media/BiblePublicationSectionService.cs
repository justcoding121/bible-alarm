#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
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

    public async Task<string?> GetSectionNameAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Use SectionCode directly since BookNum is the same as SectionCode
            var sectionCodeString = sectionNumber.ToString();
            return await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Include(x => x.UrlParams)
                .Where(x => x.BiblePublication.PublicationCode == publicationCode
                            && x.BiblePublication.Language != null
                            && x.BiblePublication.Language.LanguageCode == languageCode
                            && x.SectionCode == sectionCodeString)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationSection name. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
                languageCode, publicationCode, sectionNumber);
            throw;
        }
    }

    public async Task<SortedDictionary<int, BiblePublicationSection>> GetSectionsByPublicationAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Load sections, then filter/order by SectionCode
            var sections = await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                    .ThenInclude(s => s.UrlParams)
                .Where(x => x.Language != null && x.Language.LanguageCode == languageCode && x.PublicationCode == publicationCode)
                .SelectMany(x => x.Sections)
                .ToListAsync(cancellationToken);

            // Filter sections that have numeric SectionCode and order by it
            var sectionsByNumber = sections
                .Where(s => int.TryParse(s.SectionCode, out _))
                .OrderBy(x => int.TryParse(x.SectionCode, out var code) ? code : int.MaxValue)
                .ToDictionary(x => int.TryParse(x.SectionCode, out var code) ? code : 0, x => x);

            return new SortedDictionary<int, BiblePublicationSection>(sectionsByNumber);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationSections by publication. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<BiblePublicationSection?> GetSectionAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            // Use SectionCode directly since BookNum is the same as SectionCode
            var sectionCodeString = sectionNumber.ToString();
            return await dbContext.BiblePublications
                .AsNoTracking()
                .Include(x => x.Sections)
                    .ThenInclude(s => s.UrlParams)
                .Where(x => x.Language != null && x.Language.LanguageCode == languageCode && x.PublicationCode == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.SectionCode == sectionCodeString)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BiblePublicationSection. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
                languageCode, publicationCode, sectionNumber);
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

