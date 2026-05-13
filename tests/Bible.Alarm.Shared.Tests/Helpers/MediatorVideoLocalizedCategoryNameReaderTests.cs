#nullable enable

using System.Text.Json;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Shared.Helpers;

namespace Bible.Alarm.Shared.Tests;

public sealed class MediatorVideoLocalizedCategoryNameReaderTests
{
    [Fact]
    public void TryReadLocalizedDisplayName_returns_false_when_category_object_missing_name_property()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{}}}}");

        Assert.False(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out _));
    }

    [Fact]
    public void TryReadLocalizedDisplayName_returns_true_when_category_name_present_and_nonempty()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{\"{nm}\":\"Hello Video\"}}}}");

        Assert.True(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out var name));

        Assert.Equal("Hello Video", name);
    }

    [Fact]
    public void TryReadLocalizedDisplayName_decodes_nbsp_entities_before_accepting_value()
    {
        var cat = AppConstants.Media.PubMediaJson.Category;
        var nm = AppConstants.Media.PubMediaJson.Name;
        using var doc = JsonDocument.Parse($"{{\"{cat}\":{{\"{nm}\":\"A&nbsp;B\"}}}}");

        Assert.True(MediatorVideoLocalizedCategoryNameReader.TryReadLocalizedDisplayName(doc.RootElement, out var name));

        Assert.Equal("A B", name);
    }
}
