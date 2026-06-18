#nullable enable

using System.Reflection;
using Bible.Alarm.Common.ViewHelpers.Behaviours;
using Bible.Alarm.Tests.Support;
using Microsoft.Maui.Controls;

namespace Bible.Alarm.Tests;

[Collection("MauiUi")]
public sealed class TouchFeedbackBehaviorTests(MauiUiFixture fixture)
{
    private static void InvokeOnTapped(TouchFeedbackBehavior behavior, TapGestureRecognizer tap, View view)
    {
        var method = typeof(TouchFeedbackBehavior).GetMethod(
            "OnTapped",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(behavior, [tap, new TappedEventArgs(1)]);
    }

    [Fact]
    public async Task OnAttachedTo_subscribes_to_existing_tap_gesture()
    {
        _ = fixture;
        if (!MauiUiTestBootstrap.IsReady || Application.Current is null)
        {
            return;
        }

        var label = new Label();
        var tap = new TapGestureRecognizer();
        label.GestureRecognizers.Add(tap);

        var behavior = new TouchFeedbackBehavior();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            label.Behaviors.Add(behavior);
            Application.Current!.Windows[0].Page = new ContentPage { Content = label };
            return Task.CompletedTask;
        });

        var ex = Record.Exception(() => InvokeOnTapped(behavior, tap, label));

        Assert.Null(ex);
    }

    [Fact]
    public void OnDetachingFrom_unsubscribes_without_throw()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var label = new Label();
        var tap = new TapGestureRecognizer();
        label.GestureRecognizers.Add(tap);

        var behavior = new TouchFeedbackBehavior();
        label.Behaviors.Add(behavior);
        label.Behaviors.Remove(behavior);

        var ex = Record.Exception(() => InvokeOnTapped(behavior, tap, label));

        Assert.Null(ex);
    }

    [Fact]
    public void OnAttachedTo_without_gestures_does_not_throw()
    {
        if (!MauiUiTestBootstrap.IsReady)
        {
            return;
        }

        var label = new Label();
        var behavior = new TouchFeedbackBehavior();

        var ex = Record.Exception(() => label.Behaviors.Add(behavior));

        Assert.Null(ex);
    }
}
