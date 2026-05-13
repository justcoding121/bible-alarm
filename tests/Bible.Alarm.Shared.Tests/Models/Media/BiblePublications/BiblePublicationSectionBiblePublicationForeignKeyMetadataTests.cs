#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationSectionBiblePublicationForeignKeyMetadataTests
{
    [Fact]
    public void BiblePublicationId_foreign_key_targets_publication_navigation()
    {
        var fk = typeof(BiblePublicationSection).GetProperty(nameof(BiblePublicationSection.BiblePublicationId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(BiblePublicationSection.BiblePublication), fk.Name);
    }
}
