#nullable enable

using Bible.Alarm.Services.UI.NavigationServiceHelpers;

namespace Bible.Alarm.Tests;

public sealed class NavigationStackTypeInspectorTests
{
    private sealed class MarkerPage;

    [Fact]
    public void ContainsPageWithRuntimeType_returns_true_when_matching_page_present()
    {
        object marker = new MarkerPage();
        var stack = new List<object?> { null, marker };

        Assert.True(NavigationStackTypeInspector.ContainsPageWithRuntimeType(stack, typeof(MarkerPage)));
    }

    [Fact]
    public void ContainsPageWithRuntimeType_returns_false_when_runtime_types_do_not_include_expected_page()
    {
        var stack = new List<object?> { new object(), new MarkerPage() };

        Assert.False(NavigationStackTypeInspector.ContainsPageWithRuntimeType(stack, typeof(string)));
    }
}
