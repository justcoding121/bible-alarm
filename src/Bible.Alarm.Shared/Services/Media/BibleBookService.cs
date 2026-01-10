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
/// Service for accessing BibleSection database operations.
/// </summary>
public sealed class BibleSectionService(IServiceScopeFactory scopeFactory, ILogger logger) : IBibleSectionService
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

            return await dbContext.BibleSections
                .AsNoTracking()
                .Where(x => x.BiblePublication.Code == publicationCode
                            && x.BiblePublication.Language.Code == languageCode
                            && x.Number == sectionNumber)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BibleSection name. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
                languageCode, publicationCode, sectionNumber);
            throw;
        }
    }

    public async Task<SortedDictionary<int, BibleSection>> GetSectionsByPublicationAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var sections = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);

            return new SortedDictionary<int, BibleSection>(sections.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BibleSections by publication. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<BibleSection?> GetSectionAsync(string languageCode, string publicationCode, int sectionNumber, CancellationToken cancellationToken = default)
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
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BibleSection. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, SectionNumber={SectionNumber}",
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

