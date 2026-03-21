#nullable enable
using System.Linq;
using System.Threading.Tasks;
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
            "ArticleSeries",
            "WatchtowerMagazine",
            "AwakeMagazine"
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
