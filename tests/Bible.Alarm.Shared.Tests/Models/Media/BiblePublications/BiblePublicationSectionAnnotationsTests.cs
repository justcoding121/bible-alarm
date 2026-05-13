#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionAnnotationsTests
{
    [Fact]
    public void Has_non_unique_index_on_publication_for_section_queries()
    {
        var idx = Assert.Single(
            typeof(BiblePublicationSection).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());

        Assert.False(idx.IsUnique);
        Assert.Equal(nameof(BiblePublicationSection.BiblePublicationId), Assert.Single(idx.PropertyNames));
    }
}
