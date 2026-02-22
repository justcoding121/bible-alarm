#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.AudioLinksHarvestor.Seeders;

/// <summary>
/// Helper class for seeding categories.
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
        var categories = new[]
        {
            new Category { CategoryCode = "Bible" },
            new Category { CategoryCode = "Dramas" },
            new Category { CategoryCode = "Music" },
            new Category { CategoryCode = "FaithAndBible" },
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

        await db.SaveChangesAsync();
    }
}
