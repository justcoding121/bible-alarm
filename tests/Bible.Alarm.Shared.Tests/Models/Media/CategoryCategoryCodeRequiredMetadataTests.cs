#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bible.Alarm.Shared.Models.Media;

namespace Bible.Alarm.Shared.Tests;

public sealed class CategoryCategoryCodeRequiredMetadataTests
{
    [Fact]
    public void CategoryCode_is_marked_required()
    {
        var p = typeof(Category).GetProperty(nameof(Category.CategoryCode))!;
        Assert.NotNull(p.GetCustomAttribute<RequiredAttribute>());
    }
}
