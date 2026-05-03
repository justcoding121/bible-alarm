#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.BiblePublications.BiblePublicationSectionSelectionHelpers;

namespace Bible.Alarm.Tests;

public sealed class SectionSelectionResolverTests
{
    private static BiblePublicationSectionListViewItemModel Item(string sectionCode) =>
        new(new BiblePublicationSection
        {
            SectionCode = sectionCode,
            Name = sectionCode,
            BiblePublication = null!,
        });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_or_empty_section_code_returns_null(string? sectionCode)
    {
        var sections = new[] { Item("GEN") };

        Assert.Null(SectionSelectionResolver.FindSectionToSelect(sectionCode, sections));
    }

    [Fact]
    public void Matches_section_case_insensitively()
    {
        var sections = new[] { Item("GEN"), Item("exo") };

        var found = SectionSelectionResolver.FindSectionToSelect("gen", sections);

        Assert.NotNull(found);
        Assert.Equal("GEN", found.Section.SectionCode);
    }

    [Fact]
    public void No_matching_section_returns_null()
    {
        var sections = new[] { Item("GEN") };

        Assert.Null(SectionSelectionResolver.FindSectionToSelect("missing", sections));
    }
}
