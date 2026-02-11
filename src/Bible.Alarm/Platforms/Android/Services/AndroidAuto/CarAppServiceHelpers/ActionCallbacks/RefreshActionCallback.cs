#nullable enable

using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers.ActionCallbacks;

/// <summary>
/// Callback for refresh action.
/// </summary>
internal class RefreshActionCallback(Screen screen, CarScreenActionHandler handler) : Object, IOnClickListener
{
    public void OnClick()
    {
        handler.HandleRefresh();
        screen?.Invalidate();
    }
}
