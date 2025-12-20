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
/// Service for accessing BibleTranslation database operations.
/// </summary>
public class BibleTranslationService(IServiceScopeFactory scopeFactory, ILogger logger) : IBibleTranslationService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private bool isDisposed;

    public async Task<BibleTranslation?> GetByLanguageAndCodeWithBooksAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BibleTranslations
                .AsNoTracking()
                .Include(x => x.Books)
                .Where(x => x.Code == publicationCode && x.Language.Code == languageCode)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BibleTranslation with Books. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}",
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<Dictionary<string, BibleTranslation>> GetByLanguageCodeAsync(string languageCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BibleTranslations
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode)
                .ToDictionaryAsync(x => x.Code, x => x, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting BibleTranslations by language code. LanguageCode={LanguageCode}", languageCode);
            throw;
        }
    }

    public async Task<Dictionary<string, Language>> GetDistinctLanguagesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            return await dbContext.BibleTranslations
                .AsNoTracking()
                .Select(x => x.Language)
                .Distinct()
                .ToDictionaryAsync(x => x.Code, x => x, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Error getting distinct Languages from BibleTranslations");
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

        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Error during cancellation token source disposal in BibleTranslationService");
        }
    }
}

