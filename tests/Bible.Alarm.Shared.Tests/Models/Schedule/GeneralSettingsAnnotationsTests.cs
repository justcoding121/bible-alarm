#nullable enable

using System.Linq;
using Bible.Alarm.Shared.Models.Schedule;
using Microsoft.EntityFrameworkCore;

namespace Bible.Alarm.Shared.Tests;

public sealed class GeneralSettingsAnnotationsTests
{
    [Fact]
    public void Has_unique_index_on_settings_key()
    {
        var idx = Assert.Single(
            typeof(GeneralSettings).GetCustomAttributes(typeof(IndexAttribute), inherit: false).Cast<IndexAttribute>());

        Assert.True(idx.IsUnique);
        Assert.Equal(nameof(GeneralSettings.Key), Assert.Single(idx.PropertyNames));
    }
}
