#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using AndroidX.Car.App.Model;
using AndroidX.Core.Content;
using AndroidX.Core.Graphics.Drawable;
using Serilog;
using Color = Android.Graphics.Color;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.CarAppServiceHelpers;

/// <summary>
/// Builds icons for car screen UI elements.
/// </summary>
public sealed class CarScreenIconBuilder(Context context, ILogger logger)
{
    /// <summary>
    /// Creates a CarIcon for playlist items to display in Android Auto.
    /// Uses a custom open section icon to represent Bible reading schedules.
    /// </summary>
    public CarIcon? CreateSectionIcon()
    {
        try
        {
            const int SectionIconSize = 128;
            const int SectionOffset = SectionIconSize / 2 - 8;
            const int BitmapSize = SectionIconSize + SectionOffset;

            var sectionDrawable = ContextCompat.GetDrawable(context, ResourceConstant.Drawable.ic_section_open);
            if (sectionDrawable == null)
            {
                logger.Warning("Could not get app drawable for section icon");
                return null;
            }

            var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
            var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, config);
            bitmap.EraseColor(Color.Transparent);

            var canvas = new Canvas(bitmap);

            // Draw section icon
            sectionDrawable.SetBounds(SectionOffset, SectionOffset, SectionOffset + SectionIconSize, SectionOffset + SectionIconSize);
            sectionDrawable.Draw(canvas);

            // Convert bitmap to IconCompat
            var iconCompat = IconCompat.CreateWithBitmap(bitmap);
            return new CarIcon.Builder(iconCompat).Build();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create section icon - Rows will display without icon");
            return null;
        }
    }

    /// <summary>
    /// Converts a Drawable to a Bitmap.
    /// </summary>
    public static Bitmap? DrawableToBitmap(Drawable? drawable)
    {
        if (drawable == null)
        {
            return null;
        }

        if (drawable is BitmapDrawable bitmapDrawable && bitmapDrawable.Bitmap != null)
        {
            return bitmapDrawable.Bitmap;
        }

        var width = drawable.IntrinsicWidth > 0 ? drawable.IntrinsicWidth : 64;
        var height = drawable.IntrinsicHeight > 0 ? drawable.IntrinsicHeight : 64;

        var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888 ?? Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
        drawable.Draw(canvas);
        return bitmap;
    }
}

