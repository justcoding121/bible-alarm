#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryCategoryCodeMaxLengthMetadataTests
{
    [Fact]
    public void CategoryCode_max_length_matches_EF_contract()
    {
        var max = typeof(Category).GetProperty(nameof(Category.CategoryCode))!
            .GetCustomAttributes(typeof(MaxLengthAttribute), inherit: false)
            .Cast<MaxLengthAttribute>()
            .Single();
        Assert.Equal(100, max.Length);
    }
}
