#nullable enable

using AndroidX.Car.App;
using AndroidX.Car.App.Model;
using Object = Java.Lang.Object;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;

/// <summary>
/// Callback for pause action.
/// </summary>
internal class PauseActionCallback(Screen screen, CarScreenActionHandler handler) : Object, IOnClickListener
{
    public void OnClick()
    {
        handler.HandlePause();
        screen?.Invalidate();
    }
}
