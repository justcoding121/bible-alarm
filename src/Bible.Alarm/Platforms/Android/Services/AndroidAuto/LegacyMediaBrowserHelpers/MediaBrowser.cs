#nullable enable
using _Microsoft.Android.Resource.Designer;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Support.V4.Media;
using AndroidX.Core.Content;
using Bible.Alarm.Shared.Constants;
using Bible.Alarm.Stores.Models;
using Serilog;
using Color = Android.Graphics.Color;

namespace Bible.Alarm.Platforms.Android.Services.AndroidAuto.LegacyMediaBrowserHelpers;

/// <summary>
/// Handles media browsing operations for LegacyMediaBrowserService.
/// </summary>
public sealed class MediaBrowser(ILogger logger)
{
    private const int SectionIconSize = 128;
    private const int SectionOffset = SectionIconSize / 2 - 8;
    private const int BitmapSize = SectionIconSize + SectionOffset;

    /// <summary>
    /// Loads children for the given parent media ID.
    /// </summary>
    public async Task<IList<MediaBrowserCompat.MediaItem>?> LoadChildrenAsync(string parentId, Context? context)
    {
        logger.Debug(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.LoadingChildrenForParentId, parentId);

        try
        {
            // Wait for bootstrap to complete
            await MauiProgram.WaitForBootstrapAsync();
            logger.Debug(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.BootstrapCompletedLoadingSchedulesFromStateForParent, parentId);
        }
        catch (Exception bootstrapEx)
        {
            logger.Warning(bootstrapEx, AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.BootstrapNotReadyOrTimedOutForParentReturningEmptyList, parentId);
            return new List<MediaBrowserCompat.MediaItem>();
        }

        var scheduleItems = AndroidAutoScheduleHelper.LoadScheduleStateItemsFromState();
        if (scheduleItems.Count == 0)
        {
            logger.Warning("No schedules found in state for parent: {ParentId} - state may not be initialized yet", parentId);
            return new List<MediaBrowserCompat.MediaItem>();
        }

        var mediaItems = CreateMediaItemsFromSchedules(scheduleItems, context);
        logger.Information(AppConstants.Logging.LegacyMediaBrowserDiagnosticsLog.CreatedMediaItemsForAndroidAuto, mediaItems.Count);
        return mediaItems;
    }

    /// <summary>
    /// Loads children for the given parent media ID (synchronous version for compatibility).
    /// </summary>
    public IList<MediaBrowserCompat.MediaItem>? LoadChildren(string parentId)
    {
        // This method is kept for compatibility but should not be used
        // The async version LoadChildrenAsync should be used instead
        logger.Debug(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.LoadChildrenCalledSynchronouslyForParentReturningEmptyList, parentId);
        return new List<MediaBrowserCompat.MediaItem>();
    }

    private List<MediaBrowserCompat.MediaItem> CreateMediaItemsFromSchedules(List<ScheduleStateItem> scheduleItems, Context? context)
    {
        var mediaItems = new List<MediaBrowserCompat.MediaItem>();

        foreach (var scheduleItem in scheduleItems)
        {
            try
            {
                var mediaItem = CreateMediaItemFromSchedule(scheduleItem, context);
                if (mediaItem != null)
                {
                    mediaItems.Add(mediaItem);
                    logger.Debug(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.AddedMediaItemForScheduleTitle,
                        scheduleItem.Id, AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem));
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.FailedToCreateMediaItemForSchedule, scheduleItem.Id);
            }
        }

        return mediaItems;
    }

    private MediaBrowserCompat.MediaItem? CreateMediaItemFromSchedule(ScheduleStateItem scheduleItem, Context? context)
    {
        var title = AndroidAutoScheduleHelper.BuildScheduleTitle(scheduleItem);
        var subtitle = AndroidAutoScheduleHelper.BuildScheduleSubtitle(scheduleItem);
        var description = BuildScheduleDescription(scheduleItem);

        var mediaDescription = CreateMediaDescriptionForSchedule(scheduleItem, title, subtitle, description, context);
        if (mediaDescription == null)
        {
            return null;
        }

        return new MediaBrowserCompat.MediaItem(
            mediaDescription,
            MediaBrowserCompat.MediaItem.FlagPlayable);
    }

    private static string BuildScheduleDescription(ScheduleStateItem scheduleItem)
    {
        var description = $"Schedule ID: {scheduleItem.Id}";
        if (!string.IsNullOrWhiteSpace(scheduleItem.BiblePublicationCode))
        {
            description += $", {scheduleItem.BiblePublicationCode}";
        }
        return description;
    }

    /// <summary>
    /// Creates a MediaDescriptionCompat for a schedule item.
    /// Used for both initial load and item-level updates.
    /// 
    /// CRITICAL: MediaId must remain stable (use schedule ID, not name).
    /// If MediaId changes, Android Auto treats it as a new item, causing:
    /// - Loss of scroll position
    /// - Loss of "playing" icon indicator
    /// - UI jump/flash
    /// If MediaId stays the same but Title changes, Android Auto updates the text in place.
    /// </summary>
    private MediaDescriptionCompat? CreateMediaDescriptionForSchedule(ScheduleStateItem scheduleItem, string title, string subtitle, string description, Context? context)
    {
        var descriptionBuilder = new MediaDescriptionCompat.Builder();
        // CRITICAL: Use schedule ID (stable) as MediaId, not name (can change)
        // This ensures Android Auto updates items in place when name changes
        descriptionBuilder.SetMediaId(scheduleItem.Id.ToString());
        descriptionBuilder.SetTitle(title);
        descriptionBuilder.SetSubtitle(subtitle);
        descriptionBuilder.SetDescription(description);

        if (context != null)
        {
            var sectionIconBitmap = CreateSectionIconBitmap(context);
            if (sectionIconBitmap != null)
            {
                descriptionBuilder.SetIconBitmap(sectionIconBitmap);
                logger.Debug("Set section icon bitmap for schedule {ScheduleId} - Size: {Width}x{Height}",
                    scheduleItem.Id, sectionIconBitmap.Width, sectionIconBitmap.Height);
            }
            else
            {
                logger.Warning(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.FailedToCreateSectionIconBitmapForSchedule, scheduleItem.Id);
            }
        }

        return descriptionBuilder.Build();
    }

    private Bitmap? CreateSectionIconBitmap(Context context)
    {
        try
        {
            var sectionDrawable = GetSectionDrawable(context);
            if (sectionDrawable == null)
            {
                return null;
            }

            var bitmap = CreateIconBitmap();
            var canvas = new Canvas(bitmap);

            DrawSectionIcon(canvas, sectionDrawable);

            logger.Debug("Created section icon bitmap - Size: {WidthPx}x{HeightPx}", BitmapSize, BitmapSize);
            return bitmap;
        }
        catch (Exception ex)
        {
            logger.Warning(ex, AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.FailedToCreateSectionIconBitmapItemsWillDisplayWithoutIcon);
            return null;
        }
    }

    private Drawable? GetSectionDrawable(Context context)
    {
        var sectionDrawable = ContextCompat.GetDrawable(context, ResourceConstant.Drawable.ic_book_open);
        if (sectionDrawable == null)
        {
            logger.Warning(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.CouldNotGetAppDrawableForSectionIcon);
        }
        return sectionDrawable;
    }

    private static Bitmap CreateIconBitmap()
    {
        var config = Bitmap.Config.Argb8888 ?? throw new InvalidOperationException("Bitmap.Config.Argb8888 is null");
        var bitmap = Bitmap.CreateBitmap(BitmapSize, BitmapSize, config);
        bitmap.EraseColor(Color.Transparent);
        return bitmap;
    }

    private static void DrawSectionIcon(Canvas canvas, Drawable sectionDrawable)
    {
        sectionDrawable.SetBounds(SectionOffset, SectionOffset, SectionOffset + SectionIconSize, SectionOffset + SectionIconSize);
        sectionDrawable.Draw(canvas);
    }

    /// <summary>
    /// Gets the media item for the given media ID.
    /// </summary>
    public MediaBrowserCompat.MediaItem? GetMediaItem(string mediaId)
    {
        // This would implement logic to get a specific media item
        logger.Debug(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.GettingMediaItemForId, mediaId);
        return null;
    }

    /// <summary>
    /// Searches for media items matching the query.
    /// </summary>
    public void Search(string query, Bundle? extras)
    {
        // This would implement search functionality
        logger.Debug(AppConstants.Logging.LegacyMediaBrowserBrowseOperationsDiagnosticsLog.SearchingForQuery, query);
    }
}
