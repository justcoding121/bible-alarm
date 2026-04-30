#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Services.Media.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Bible.Alarm.Shared.Services.Media;

/// <summary>
/// Service for accessing Category database operations.
/// </summary>
public sealed class CategoryService(IServiceScopeFactory scopeFactory, ILogger logger) : ICategoryService
{
    private readonly IServiceScopeFactory scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private bool isDisposed;
    private readonly object categoriesCacheLock = new();
    private List<Category>? cachedCategories;

    public async Task<List<Category>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            lock (categoriesCacheLock)
            {
                if (cachedCategories != null)
                {
                    // Return a copy so callers can't mutate the cache.
                    return new List<Category>(cachedCategories);
                }
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MediaDbContext>();

            var categories = await dbContext.Categories
                .AsNoTracking()
                .OrderBy(c => c.CategoryCode == AppConstants.Media.BiblePublicationCategoryBible ? 0 : 1)
                .ThenBy(c => c.CategoryCode)
                .ToListAsync(cancellationToken);

            logger.Debug("CategoryService.GetAllCategoriesAsync: Found {CategoryCount} categories", categories.Count);

            lock (categoriesCacheLock)
            {
                cachedCategories = categories;
            }

            return categories;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error getting all categories.", ex);
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
