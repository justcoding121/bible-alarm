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
}