#nullable enable

using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using AndroidX.Core.Content;
using AndroidX.Media3.Common;
using Bible.Alarm.Platforms.Android.Services.AndroidAuto;
using Bible.Alarm.Stores.Models;
using Color = Android.Graphics.Color;

namespace Bible.Alarm.Platforms.Android.Services.Media3;

/// <summary>
/// Same browse tree as the legacy media browser: one playable item per schedule,
/// stable media id, section icon, title and subtitle.
/// </summary>
internal static class Media3LibraryCatalog
{
    public const string RootId = "__ID_ROOT__";
    private const int SectionIconSize = 128;
    private const int SectionOffset = SectionIconSize / 2 - 8;
    private const int BitmapSize = SectionIconSize + SectionOffset;

    public static IList<MediaItem> LoadChildren(Context? context)
    {
        var schedules = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
        var items = new List<MediaItem>(schedules.Count);
        foreach (var schedule in schedules)
        {
            items.Add(CreateItem(schedule, context));
        }

        return items;
    }

    public static MediaItem? Find(string? mediaId, Context? context)
    {
        if (!int.TryParse(mediaId, out var scheduleId))
        {
            return null;
        }

        var schedule = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState()
            .FirstOrDefault(item => item.Id == scheduleId);
        return schedule == null ? null : CreateItem(schedule, context);
    }

    private static MediaItem CreateItem(ScheduleStateItem schedule, Context? context)
    {
        var title = AndroidAutoScheduleHelper.BuildScheduleTitle(schedule);
        var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(schedule);
        var description = $"Schedule ID: {schedule.Id}";
        if (!string.IsNullOrWhiteSpace(schedule.BiblePublicationCode))
        {
            description += $", {schedule.BiblePublicationCode}";
        }

        var metadata = new MediaMetadata.Builder()
            .SetTitle(title)!
            .SetArtist(subtitle)!
            .SetSubtitle(subtitle)!
            .SetDescription(description)!
            .SetIsPlayable(Java.Lang.Boolean.True)!
            .SetIsBrowsable(Java.Lang.Boolean.False)!;
        if (context != null && CreateSectionIcon(context) is { } icon)
        {
            using var stream = new MemoryStream();
            icon.Compress(Bitmap.CompressFormat.Png!, 100, stream);
            metadata.SetArtworkData(stream.ToArray(), (Java.Lang.Integer)MediaMetadata.PictureTypeFrontCover);
        }

        return new MediaItem.Builder()
            .SetMediaId(schedule.Id.ToString())!
            .SetMediaMetadata(metadata.Build())!
            .Build()!;
    }

    private static Bitmap? CreateSectionIcon(Context context)
    {
        var drawable = ContextCompat.GetDrawable(context, ResourceConstant.Drawable.ic_book_open);
        if (drawable == null)
        {
            return null;
        }

        var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap config is null.");
        var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, config);
        if (bitmap == null)
        {
            return null;
        }

        bitmap.EraseColor(Color.Transparent);
        var canvas = new Canvas(bitmap);
        drawable.SetBounds(SectionOffset, SectionOffset, SectionOffset + SectionIconSize, SectionOffset + SectionIconSize);
        drawable.Draw(canvas);
        return bitmap;
    }
}
