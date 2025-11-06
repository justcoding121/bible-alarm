namespace Bible.Alarm.Common.ViewHelpers;

public static class ColorUtils
{
    public static string ToHexString(Color color)
    {
        var red = (int)(color.Red * 255);
        var green = (int)(color.Green * 255);
        var blue = (int)(color.Blue * 255);
        var alpha = (int)(color.Alpha * 255);
        var hex = $"#{alpha:X2}{red:X2}{green:X2}{blue:X2}";

        return hex;
    }
}