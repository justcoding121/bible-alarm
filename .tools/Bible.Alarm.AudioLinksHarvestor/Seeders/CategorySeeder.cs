#nullable enable
using System.Collections.Generic;
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
    private const string EnglishLanguageCode = "E";

    private static string DisplayNameForCategoryCode(string categoryCode) =>
        categoryCode == "FaithAndBible" ? "Faith & Bible" : categoryCode;

    private readonly ILogger logger;

    public CategorySeeder(ILogger logger)
    {
        this.logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
    }

    public async Task SeedDefaultCategoriesAndApiUrls(MediaDbContext db)
    {
        var categoryCodes = new[]
        {
            "Bible",
            "Dramas",
            "Music",
            "FaithAndBible",
            "Books",
            "Broadcasting",
            "Brochures and Booklets",
            "Children",
            "Family",
            "Interviews and Experiences",
            "Meetings and Ministry",
            "Programs and Events",
            "Series",
            "Teenagers"
        };

        foreach (var code in categoryCodes)
        {
            var existing = await db.Categories.FirstOrDefaultAsync(c => c.CategoryCode == code);
            if (existing == null)
            {
                db.Categories.Add(new Category { CategoryCode = code });
                logger.Information("Seeding category: {CategoryCode}", code);
            }
        }

        await db.SaveChangesAsync();

        await SeedCategoryDisplayNamesAsync(db, categoryCodes);
    }

    private async Task SeedCategoryDisplayNamesAsync(MediaDbContext db, IReadOnlyList<string> categoryCodes)
    {
        foreach (var categoryCode in categoryCodes)
        {
            var category = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryCode == categoryCode);
            if (category == null)
            {
                continue;
            }

            var hasName = await db.CategoryNamesByLanguage
                .AnyAsync(cnl => cnl.CategoryId == category.Id && cnl.LanguageCode == EnglishLanguageCode);
            if (hasName)
            {
                continue;
            }

            var displayName = DisplayNameForCategoryCode(categoryCode);
            db.CategoryNamesByLanguage.Add(new CategoryNameByLanguage
            {
                CategoryId = category.Id,
                LanguageCode = EnglishLanguageCode,
                Name = displayName
            });
            logger.Information("Seeding category display name: {CategoryCode} -> {DisplayName} (language={Lang})",
                categoryCode, displayName, EnglishLanguageCode);
        }

        await db.SaveChangesAsync();
    }
}
