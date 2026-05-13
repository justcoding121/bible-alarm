#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Media;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class LanguageAnnotationsTests
{
    [Fact]
    public void Has_unique_index_on_language_code()
    {
        var idx = Assert.Single(
            typeof(Language).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());

        Assert.True(idx.IsUnique);
        Assert.Equal(nameof(Language.LanguageCode), Assert.Single(idx.PropertyNames));
    }
}
