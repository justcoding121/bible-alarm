namespace Bible.Alarm.Common.ViewHelpers;

public class AnimateUtils
{
    public static void FlickUponTouched(View view, uint duration, string hexColorInitial,
        string hexColorFinal, int repeatCountMax)
    {
        var repeatCount = 0;
        view.Animate("changedBG", new Animation(_ =>
        {
            if (repeatCount == 0)
                view.BackgroundColor = Color.FromArgb(hexColorInitial);
            else
                view.BackgroundColor = Color.FromArgb(hexColorFinal);
        }), duration, finished: (_, _) => { repeatCount++; }, repeat: () => { return repeatCount < repeatCountMax; });
    }
    
    /// <summary>
    /// Provides touch feedback animation for list items (press/release effect)
    /// </summary>
    public static void AnimateTouchFeedback(View view)
    {
        if (view == null) return;
        
        // Default to White if BackgroundColor is not set
        var originalColor = view.BackgroundColor ?? Colors.White;
        // Light gray for pressed state
        var pressedColor = ThemeColors.Animation.PressedBackground;
        
        // Animate to pressed state
        view.Animate("touchPress", new Animation(v =>
        {
            if (view == null) return;
            view.BackgroundColor = Color.FromRgba(
                originalColor.Red + (pressedColor.Red - originalColor.Red) * v,
                originalColor.Green + (pressedColor.Green - originalColor.Green) * v,
                originalColor.Blue + (pressedColor.Blue - originalColor.Blue) * v,
                originalColor.Alpha);
        }), 
        length: 100, 
        finished: (_, _) =>
        {
            // Animate back to original state
            if (view == null) return;
            view.Animate("touchRelease", new Animation(v =>
            {
                if (view == null) return;
                view.BackgroundColor = Color.FromRgba(
                    pressedColor.Red + (originalColor.Red - pressedColor.Red) * v,
                    pressedColor.Green + (originalColor.Green - pressedColor.Green) * v,
                    pressedColor.Blue + (originalColor.Blue - pressedColor.Blue) * v,
                    originalColor.Alpha);
            }), 
            length: 150);
        });
    }
}