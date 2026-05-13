#nullable enable

using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionTests
{
    [Fact]
    public void CompareTo_orders_section_codes_numerically_when_both_parse_as_integers()
    {
        var ten = new BiblePublicationSection { SectionCode = "10" };
        var two = new BiblePublicationSection { SectionCode = "2" };

        Assert.True(two.CompareTo(ten) < 0);
    }

    [Fact]
    public void CompareTo_object_returns_positive_when_argument_is_not_section()
    {
        var sut = new BiblePublicationSection { SectionCode = "1" };

        Assert.True(sut.CompareTo(new object()) > 0);
    }
}
