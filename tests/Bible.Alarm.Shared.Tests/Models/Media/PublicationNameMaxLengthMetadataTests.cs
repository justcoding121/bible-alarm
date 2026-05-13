#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class PublicationNameMaxLengthMetadataTests
{
    [Fact]
    public void Name_max_length_matches_EF_contract()
    {
        var max = typeof(Publication).GetProperty(nameof(Publication.Name))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(255, max.Length);
    }
}
