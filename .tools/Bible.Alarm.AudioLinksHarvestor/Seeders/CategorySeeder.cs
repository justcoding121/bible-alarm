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
        categoryCode switch
        {
            "FaithAndBible" => "Faith and Bible",
            "BrochuresAndBooklets" => "Brochures and Booklets",
            "InterviewsAndExperiences" => "Interviews and Experiences",
            "MeetingsAndMinistry" => "Meetings and Ministry",
            "ProgramsAndEvents" => "Programs and Events",
            "Activities" => "Activities",
            "Organization" => "Organization",
            "Series" => "Video Series",
            "ArticleSeries" => "Article Series",
            _ => categoryCode
        };

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
            "Yearbooks",
            "Broadcasting",
            "BrochuresAndBooklets",
            "Children",
            "Family",
            "InterviewsAndExperiences",
            "MeetingsAndMinistry",
            "ProgramsAndEvents",
            "Series",
            "Teenagers",
            "Activities",
            "Organization",
            "ArticleSeries"
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
