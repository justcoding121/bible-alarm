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
            new Category { CategoryName = "Bible" },
            new Category { CategoryName = "Dramas" },
            new Category { CategoryName = "Music" },
            new Category { CategoryName = "Article Series" },
            new Category { CategoryName = "Books" },
            new Category { CategoryName = "Broadcasting" },
            new Category { CategoryName = "Brochures and Booklets" },
            new Category { CategoryName = "Children" },
            new Category { CategoryName = "Family" },
            new Category { CategoryName = "Interviews and Experiences" },
            new Category { CategoryName = "Meetings and Ministry" },
            new Category { CategoryName = "Programs and Events" },
            new Category { CategoryName = "Series" },
            new Category { CategoryName = "Teenagers" }
        };

        foreach (var category in categories)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.CategoryName == category.CategoryName);
            if (existing == null)
            {
                db.Categories.Add(category);
                logger.Information("Seeding category: {CategoryName}", category.CategoryName);
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
