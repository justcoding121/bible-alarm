#nullable enable

using Bible.Alarm.Shared.Database;
using Bible.Alarm.Shared.Models.Media;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Bible.Alarm.Shared.Tests;

/// <summary>
/// Asserts <see cref="MediaDbContext"/> fluent configuration in <see cref="MediaDbContext.OnModelCreating"/> at runtime,
/// independently of migrations.
/// </summary>
public sealed class MediaDbContextModelTests
{
    [Fact]
    public void Parameterless_constructor_creates_context_without_throwing_on_disposal()
    {
        using var ctx = new MediaDbContext();
        Assert.NotNull(ctx);
    }

    [Fact]
    public async Task OnModelCreating_Applies_Index_Fk_and_Delete_behavior_Rules_On_Sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var ctx = new MediaDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        var model = ctx.Model;

        AssertLanguageCodeUnique(model);
        TrackUrl_Has_Optional_Tie_To_BiblePublicationTrack(model);

        PublicationLanguage_Language_Fk_optional(model);

        LanguageNameCascade(model);

        BiblePublicationCategory_Composite_primary_key(model);

        Section_Language_FKs_and_PublicationLanguage_cascade(model);
    }

    private static void AssertLanguageCodeUnique(IModel model)
    {
        var lang = model.FindEntityType(typeof(Language));
        Assert.NotNull(lang);

        Assert.Contains(
            lang.GetIndexes(),
            index => index is { IsUnique: true } &&
                     index.Properties.Count == 1 &&
                     index.Properties[0].Name == nameof(Language.LanguageCode));
    }

    private static void TrackUrl_Has_Optional_Tie_To_BiblePublicationTrack(IModel model)
    {
        var trackUrlType = model.FindEntityType(typeof(TrackUrl));
        Assert.NotNull(trackUrlType);

        var fk = Assert.Single(trackUrlType.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(BiblePublicationTrack));

        Assert.False(fk.IsRequired);
    }

    private static void PublicationLanguage_Language_Fk_optional(IModel model)
    {
        var publicationLanguage = model.FindEntityType(typeof(PublicationLanguage));
        Assert.NotNull(publicationLanguage);

        var fk = Assert.Single(publicationLanguage.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(Language));

        Assert.False(fk.IsRequired);

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    private static void LanguageNameCascade(IModel model)
    {
        var ln = model.FindEntityType(typeof(LanguageNameByLanguage));
        Assert.NotNull(ln);

        var fkLang = Assert.Single(ln.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(Language));

        Assert.Equal(DeleteBehavior.Cascade, fkLang.DeleteBehavior);
    }

    private static void BiblePublicationCategory_Composite_primary_key(IModel model)
    {
        var j = model.FindEntityType(typeof(BiblePublicationCategory));
        Assert.NotNull(j);

        var pk = j.FindPrimaryKey();
        Assert.NotNull(pk);

        Assert.Equal(
            new HashSet<string>(
            [
                nameof(BiblePublicationCategory.BiblePublicationId),
                nameof(BiblePublicationCategory.CategoryId),
            ]),
            pk.Properties.Select(p => p.Name).ToHashSet());
    }

    private static void Section_Language_FKs_and_PublicationLanguage_cascade(IModel model)
    {
        var sl = model.FindEntityType(typeof(SectionLanguage));
        Assert.NotNull(sl);

        var fkLanguage = Assert.Single(sl.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(Language));

        Assert.False(fkLanguage.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, fkLanguage.DeleteBehavior);

        var fkPl = Assert.Single(sl.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(PublicationLanguage));

        Assert.True(fkPl.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, fkPl.DeleteBehavior);
    }
}
