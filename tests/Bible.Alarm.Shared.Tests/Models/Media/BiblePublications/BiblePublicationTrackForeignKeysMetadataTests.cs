#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media.BiblePublications;

namespace Bible.Alarm.Shared.Tests;

public sealed class BiblePublicationTrackForeignKeysMetadataTests
{
    [Fact]
    public void Foreign_keys_target_publication_and_section_navigations()
    {
        var fkPub = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.BiblePublicationId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(BiblePublicationTrack.Publication), fkPub.Name);

        var fkSec = typeof(BiblePublicationTrack).GetProperty(nameof(BiblePublicationTrack.BiblePublicationSectionId))!
            .GetCustomAttributes(typeof(ForeignKeyAttribute), inherit: false)
            .Cast<ForeignKeyAttribute>()
            .Single();
        Assert.Equal(nameof(BiblePublicationTrack.Section), fkSec.Name);
    }
}
