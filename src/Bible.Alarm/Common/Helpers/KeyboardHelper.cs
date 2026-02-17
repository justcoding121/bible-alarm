#nullable enable

using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

#if ANDROID
using Android.Content;
using Android.Views.InputMethods;
#endif

namespace Bible.Alarm.Common.Helpers;

/// <summary>
/// Helper class for keyboard operations across platforms.
/// </summary>
public static class KeyboardHelper
{
    /// <summary>
    /// Hides the keyboard by unfocusing the entry and using platform-specific methods.
    /// </summary>
    public static void HideKeyboard(Entry? entry)
    {
        if (entry == null)
        {
            return;
        }

        // First, unfocus the entry
        entry.Unfocus();

#if ANDROID
        // On Android, Unfocus() might not hide the keyboard, so we need to use InputMethodManager
        HideKeyboardAndroid(entry);
#endif
    }


#if ANDROID
    private static void HideKeyboardAndroid(Entry entry)
    {
        try
        {
            var handler = entry.Handler;
            if (handler?.PlatformView is Android.Views.View platformView)
            {
                var context = platformView.Context;
                if (context != null)
                {
                    var inputMethodManager = context.GetSystemService(Context.InputMethodService) as InputMethodManager;
                    if (inputMethodManager != null)
                    {
                        var windowToken = platformView.WindowToken;
                        if (windowToken != null)
                        {
                            inputMethodManager.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
                        }
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Warning(ex, "[KeyboardHelper] Failed to hide keyboard on Android");
        }
    }

#endif
}
