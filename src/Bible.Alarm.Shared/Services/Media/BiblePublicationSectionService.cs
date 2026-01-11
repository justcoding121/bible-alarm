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

            return await dbContext.BiblePublicationSections
                .AsNoTracking()
                .Where(x => x.BiblePublication.Code == publicationCode
                            && x.BiblePublication.Language.Code == languageCode
                            && x.Number == sectionNumber)
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

            var sections = await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);

            return new SortedDictionary<int, BiblePublicationSection>(sections.ToDictionary(x => x.Number, x => x));
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

            return await dbContext.BiblePublications
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Sections)
                .Where(x => x.Number == sectionNumber)
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

