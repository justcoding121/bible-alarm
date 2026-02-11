#nullable enable

using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers.ActionCallbacks;

/// <summary>
/// Callback for play action.
/// </summary>
internal class PlayActionCallback(Screen screen, CarScreenActionHandler handler) : Object, IOnClickListener
{
    public void OnClick()
    {
        handler.HandlePlay();
        screen?.Invalidate();
    }
}
