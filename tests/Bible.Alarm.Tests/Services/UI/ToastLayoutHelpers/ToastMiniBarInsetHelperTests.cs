#nullable enable

using System.Runtime.CompilerServices;
using Bible.Alarm.Services.UI.ToastLayoutHelpers;
using Bible.Alarm.ViewModels.Shared;

namespace Bible.Alarm.Tests;

public sealed class ToastMiniBarInsetHelperTests
{
    [Fact]
    public void GetBottomInsetDip_parameterless_overload_delegates_without_throwing()
    {
        var inset = ToastMiniBarInsetHelper.GetBottomInsetDip();

        Assert.True(inset >= 0);
    }

    [Fact]
    public void GetBottomInsetDip_returns_zero_when_mini_bar_view_model_uninitialized()
    {
        Assert.Equal(0, ToastMiniBarInsetHelper.GetBottomInsetDip(null, lastMeasuredHeightDip: 0));
    }

    [Fact]
    public void GetBottomInsetDip_returns_zero_when_mini_bar_hidden()
    {
        var vm = CreateVisibleMiniBarViewModel(isVisible: false);

        Assert.Equal(0, ToastMiniBarInsetHelper.GetBottomInsetDip(vm, lastMeasuredHeightDip: 120));
    }

    [Fact]
    public void GetBottomInsetDip_uses_measured_height_when_positive()
    {
        var vm = CreateVisibleMiniBarViewModel(isVisible: true);

        Assert.Equal(116, ToastMiniBarInsetHelper.GetBottomInsetDip(vm, lastMeasuredHeightDip: 100));
    }

    [Fact]
    public void GetBottomInsetDip_uses_fallback_height_when_not_yet_measured()
    {
        var vm = CreateVisibleMiniBarViewModel(isVisible: true);

        Assert.Equal(96, ToastMiniBarInsetHelper.GetBottomInsetDip(vm, lastMeasuredHeightDip: 0));
    }

    private static MiniPlaybackBarViewModel CreateVisibleMiniBarViewModel(bool isVisible)
    {
        var vm = (MiniPlaybackBarViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MiniPlaybackBarViewModel));
        var field = typeof(MiniPlaybackBarViewModel).GetField(
            "isVisible",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, isVisible);
        return vm;
    }
}
