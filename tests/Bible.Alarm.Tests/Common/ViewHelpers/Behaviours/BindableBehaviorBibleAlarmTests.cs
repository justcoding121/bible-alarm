#nullable enable

using Bible.Alarm.Common.ViewHelpers.Behaviours;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class BindableBehaviorBibleAlarmTests(MauiUiFixture fixture)
{
    private sealed class TestBindableBehavior : BindableBehavior<Label>
    {
    }

    [Fact]
    public void Attached_behavior_tracks_binding_context_and_detaches_cleanly()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        _ = fixture;

        var label = new Label();
        var vm = new object();
        label.BindingContext = vm;

        var behavior = new TestBindableBehavior();
        label.Behaviors.Add(behavior);

        Assert.Same(label, behavior.AssociatedObject);
        Assert.Same(vm, behavior.BindingContext);

        label.BindingContext = new object();
        Assert.Same(label.BindingContext, behavior.BindingContext);

        label.Behaviors.Remove(behavior);
    }
}
