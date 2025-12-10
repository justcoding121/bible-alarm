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
/// Service for accessing BibleBook database operations.
/// </summary>
public class BibleBookService(IServiceScopeFactory scopeFactory, ILogger logger) : IBibleBookService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private bool _isDisposed;

    public async Task<string?> GetBookNameAsync(string languageCode, string publicationCode, int bookNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            return await dbContext.BibleBook
                .AsNoTracking()
                .Where(x => x.BibleTranslation.Code == publicationCode
                            && x.BibleTranslation.Language.Code == languageCode
                            && x.Number == bookNumber)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting BibleBook name. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, BookNumber={BookNumber}", 
                languageCode, publicationCode, bookNumber);
            throw;
        }
    }

    public async Task<SortedDictionary<int, BibleBook>> GetBooksByTranslationAsync(string languageCode, string publicationCode, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            var books = await dbContext.BibleTranslations
                .AsNoTracking()
                .Where(x => x.Language.Code == languageCode && x.Code == publicationCode)
                .SelectMany(x => x.Books)
                .OrderBy(x => x.Number)
                .ToListAsync(cancellationToken);
            
            return new SortedDictionary<int, BibleBook>(books.ToDictionary(x => x.Number, x => x));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting BibleBooks by translation. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}", 
                languageCode, publicationCode);
            throw;
        }
    }

    public async Task<BibleBook?> GetBookAsync(string languageCode, string publicationCode, int bookNumber, CancellationToken cancellationToken = default)
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
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting BibleBook. LanguageCode={LanguageCode}, PublicationCode={PublicationCode}, BookNumber={BookNumber}", 
                languageCode, publicationCode, bookNumber);
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
            _logger.Warning(ex, "Error during cancellation token source disposal in BibleBookService");
        }
    }
}

