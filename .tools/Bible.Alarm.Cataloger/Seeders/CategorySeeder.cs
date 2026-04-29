#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Bible.Alarm.Cataloger.Seeders;

/// <summary>
/// Helper class for seeding categories. Display names are provided by JSON resources in the shared project (e.g. CategoryNames/E.json).
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
        var categoryCodes = new[]
        {
            AppConstants.Media.BiblePublicationCategoryBible,
            AppConstants.Media.BiblePublicationCategoryDramas,
            AppConstants.Media.BiblePublicationCategoryMusic,
            AppConstants.Media.BiblePublicationCategoryFaithAndBible,
            AppConstants.Media.BiblePublicationCategoryBooks,
            AppConstants.Media.BiblePublicationCategoryYearbooks,
            AppConstants.Media.BiblePublicationCategoryBroadcasting,
            AppConstants.Media.BiblePublicationCategoryBrochuresAndBooklets,
            AppConstants.Media.BiblePublicationCategoryChildren,
            AppConstants.Media.BiblePublicationCategoryFamily,
            AppConstants.Media.BiblePublicationCategoryInterviewsAndExperiences,
            AppConstants.Media.BiblePublicationCategoryMeetingsAndMinistry,
            AppConstants.Media.BiblePublicationCategoryProgramsAndEvents,
            AppConstants.Media.BiblePublicationCategorySeries,
            AppConstants.Media.BiblePublicationCategoryTeenagers,
            AppConstants.Media.BiblePublicationCategoryActivities,
            AppConstants.Media.BiblePublicationCategoryOrganization,
            AppConstants.Media.BiblePublicationCategoryArticleSeries,
            AppConstants.Media.BiblePublicationCategoryWatchtowerMagazine,
            AppConstants.Media.BiblePublicationCategoryAwakeMagazine
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
    }
}
