using Android.Content.Res;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Handlers;

namespace Bible.Alarm.Platforms.Android.Handlers;

public class SwitchHandler : Microsoft.Maui.Handlers.SwitchHandler
{
    protected override SwitchCompat CreatePlatformView()
    {
        return base.CreatePlatformView();
    }

    protected override void ConnectHandler(SwitchCompat platformView)
    {
        base.ConnectHandler(platformView);
        ApplySwitchStyling(platformView);
    }

    private void ApplySwitchStyling(SwitchCompat switchCompat)
    {
        if (switchCompat?.Context == null)
            return;

        // Get the app's primary color (#483D8B) - Dark Slate Blue
        var primaryColor = global::Android.Graphics.Color.Argb(255, 72, 61, 139);
        
        // Gray color for knob when off and track
        // Medium gray
        var grayColor = global::Android.Graphics.Color.Argb(255, 158, 158, 158);
        
        // Create a more visible version for the track when on (60% opacity for better visibility)
        // Primary color with 60% opacity
        var trackOnColor = global::Android.Graphics.Color.Argb(153, 72, 61, 139);
        
        // Create color state lists for smooth transitions
        // StateChecked = 16842914 (from Android.Resource.Attribute.StateChecked)
        var trackColorStates = new ColorStateList(
            [
                // StateChecked
                [16842914],
                // -StateChecked
                [-16842914]
            ],
            [trackOnColor, grayColor]
        );
        
        // Thumb (knob): Gray when off, Slate blue when on
        var thumbColorStates = new ColorStateList(
            [
                // StateChecked
                [16842914],
                // -StateChecked
                [-16842914]
            ],
            [primaryColor, grayColor]
        );
        
        // Apply the color state lists
        switchCompat.TrackTintList = trackColorStates;
        switchCompat.ThumbTintList = thumbColorStates;
        
        // Use Multiply mode to blend with existing drawable and remove border effect
        switchCompat.TrackTintMode = global::Android.Graphics.PorterDuff.Mode.Multiply;
        switchCompat.ThumbTintMode = global::Android.Graphics.PorterDuff.Mode.SrcAtop;
        
        // Try to remove border by accessing the track drawable and modifying it
        // For Material 3, we need to set the track drawable after applying tints
        var trackDrawable = switchCompat.TrackDrawable;
        if (trackDrawable != null)
        {
            // Create a new drawable without stroke/border
            var gradientDrawable = new global::Android.Graphics.Drawables.GradientDrawable();
            gradientDrawable.SetShape(global::Android.Graphics.Drawables.ShapeType.Rectangle);
            // Rounded corners
            gradientDrawable.SetCornerRadius(switchCompat.Context.Resources.DisplayMetrics.Density * 12);
            gradientDrawable.SetColor(global::Android.Graphics.Color.Transparent);
            switchCompat.TrackDrawable = gradientDrawable;
        }
    }
}

