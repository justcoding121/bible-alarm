#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryAnnotationsTests
{
    [Fact]
    public void Has_unique_index_on_category_code()
    {
        var idx = Assert.Single(
            typeof(Category).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());

        Assert.True(idx.IsUnique);
        Assert.Equal(nameof(Category.CategoryCode), Assert.Single(idx.PropertyNames));
    }
}
