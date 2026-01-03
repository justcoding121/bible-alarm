#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using AndroidX.Car.App.Model;
using AndroidX.Core.Content;
using AndroidX.Core.Graphics.Drawable;
using Bible.Alarm.Common;
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
    /// Uses a custom open book icon to represent Bible reading schedules.
    /// </summary>
    public CarIcon? CreateBookIcon()
    {
        try
        {
            const int BookIconSize = 128;
            const int BookOffset = BookIconSize / 2 - 8;
            const int BitmapSize = BookIconSize + BookOffset;

            var bookDrawable = ContextCompat.GetDrawable(context, ResourceConstant.Drawable.ic_book_open);
            if (bookDrawable == null)
            {
                logger.Warning("Could not get app drawable for book icon");
                return null;
            }

            var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
            var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, config);
            bitmap.EraseColor(Color.Transparent);

            var canvas = new Canvas(bitmap);

            // Draw book icon
            bookDrawable.SetBounds(BookOffset, BookOffset, BookOffset + BookIconSize, BookOffset + BookIconSize);
            bookDrawable.Draw(canvas);

            // Convert bitmap to IconCompat
            var iconCompat = IconCompat.CreateWithBitmap(bitmap);
            return new CarIcon.Builder(iconCompat).Build();
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "Failed to create book icon - Rows will display without icon");
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

