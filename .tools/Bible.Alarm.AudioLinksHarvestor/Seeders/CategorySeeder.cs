#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Seeders;

/// <summary>
/// Helper class for seeding categories and API URLs.
/// </summary>
internal sealed class CategorySeeder
{
    private readonly ILogger logger;

    public CategorySeeder(ILogger logger)
    {
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    public async Task SeedDefaultCategoriesAndApiUrls(MediaDbContext db)
    {
        // Seed Categories (Bible first so display order is stable; app orders Bible first then alphabetical)
        var categories = new[]
        {
            new Category { CategoryCode = "Bible" },
            new Category { CategoryCode = "Dramas" },
            new Category { CategoryCode = "Music" },
            new Category { CategoryCode = "Books" },
            new Category { CategoryCode = "Broadcasting" },
            new Category { CategoryCode = "Brochures and Booklets" },
            new Category { CategoryCode = "Children" },
            new Category { CategoryCode = "Family" },
            new Category { CategoryCode = "Interviews and Experiences" },
            new Category { CategoryCode = "Meetings and Ministry" },
            new Category { CategoryCode = "Programs and Events" },
            new Category { CategoryCode = "Series" },
            new Category { CategoryCode = "Teenagers" }
        };

        foreach (var category in categories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.CategoryCode == category.CategoryCode);
            if (existing == null)
            {
                db.Categories.Add(category);
                logger.Information("Seeding category: {CategoryCode}", category.CategoryCode);
            }
        }

        // Seed ApiUrls
        var apiUrls = new[]
        {
            new ApiUrl
            {
                Url = "https://b.jw-cdn.org",
                PathPrefix = "apis/pub-media/GETPUBMEDIALINKS"
            },
            new ApiUrl
            {
                Url = "https://app.jw-cdn.org",
                PathPrefix = "apis/pub-media/GETPUBMEDIALINKS"
            }
        };

        foreach (var apiUrl in apiUrls)
        {
            // Check if this specific URL and PathPrefix combination already exists
            var existingApiUrl = await db.ApiUrls.FirstOrDefaultAsync(a => 
                a.Url == apiUrl.Url && a.PathPrefix == apiUrl.PathPrefix);
            if (existingApiUrl == null)
            {
                db.ApiUrls.Add(apiUrl);
                await db.SaveChangesAsync(); // Save to get the ID
                
                // Seed UrlParam with output=json for this base URL
                var urlParam = new UrlParam
                {
                    ApiUrlId = apiUrl.Id,
                    Key = "output",
                    Value = "json",
                    IsQueryParam = true
                };
                db.UrlParams.Add(urlParam);
                
                logger.Information("Seeding ApiUrl: {ApiUrl} / {PathPrefix} with output=json", apiUrl.Url, apiUrl.PathPrefix);
            }
            else
            {
                // Check if output=json param already exists for this base URL
                var existingParam = await db.UrlParams.FirstOrDefaultAsync(p => 
                    p.ApiUrlId == existingApiUrl.Id && p.Key == "output" && p.Value == "json");
                if (existingParam == null)
                {
                    var urlParam = new UrlParam
                    {
                        ApiUrlId = existingApiUrl.Id,
                        Key = "output",
                        Value = "json",
                        IsQueryParam = true
                    };
                    db.UrlParams.Add(urlParam);
                    logger.Information("Adding output=json param to existing ApiUrl: {ApiUrl} / {PathPrefix}", existingApiUrl.Url, existingApiUrl.PathPrefix);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}
