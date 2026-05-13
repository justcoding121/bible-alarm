#nullable enable

using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageNameByLanguageAnnotationsTests
{
    [Fact]
    public void Type_is_mapped_to_LanguageNamesByLanguage_with_unique_composite_index()
    {
        var table = typeof(LanguageNameByLanguage).GetCustomAttributes(typeof(TableAttribute), inherit: false)
            .Cast<TableAttribute>()
            .Single();
        Assert.Equal("LanguageNamesByLanguage", table.Name);

        var indexes = typeof(LanguageNameByLanguage).GetCustomAttributes(typeof(IndexAttribute), inherit: false)
            .Cast<IndexAttribute>()
            .ToList();

        _ = Assert.Single(indexes, a =>
            a.IsUnique &&
            a.PropertyNames.SequenceEqual(new[] { nameof(LanguageNameByLanguage.LanguageId), nameof(LanguageNameByLanguage.DisplayLanguageCode) }));
    }
}
