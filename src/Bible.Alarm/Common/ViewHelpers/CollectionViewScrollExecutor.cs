#nullable enable
using System;
using System.Collections;
using System.Runtime.InteropServices;
using Bible.Alarm.Shared.Helpers;
using Bible.Alarm.ViewModels.BiblePublications;
using Bible.Alarm.ViewModels.Music;
using Bible.Alarm.ViewModels.Shared;
using Serilog;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;
#if WINDOWS
using Microsoft.Maui.Essentials;
#endif

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Helper class for executing scroll operations on CollectionView.
/// </summary>
internal static class CollectionViewScrollExecutor
{
    /// <summary>
    /// Scrolls to an item and waits for the scroll operation to complete.
    /// Uses Dispatcher.Dispatch to ensure the scroll is processed, then waits for visual completion.
    /// </summary>
    public static async Task ScrollAndWaitAsync(
        MauiCollectionView collectionView,
        object itemOrIndex,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        // Dispatch the scroll operation to ensure it's processed on the UI thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (itemOrIndex is int index)
            {
                // Index-based scrolling
                collectionView.ScrollTo(index, position: position, animate: animated);
            }
            else
            {
                // Item-based scrolling
                collectionView.ScrollTo(itemOrIndex, position: position, animate: animated);
            }
        });

        // Wait for the UI thread to process the scroll
        await Task.Yield();

        // Additional delay to ensure the scroll is visually complete
        // For non-animated scrolls, this gives Windows' ScrollViewer time to update
        await Task.Delay(animated ? 300 : 200, cancellationToken);

        // On Windows, give extra time for ScrollViewer to update its extent
#if WINDOWS
        if (DeviceInfo.Platform == DevicePlatform.WinUI)
        {
            await Task.Delay(100, cancellationToken);
        }
#endif
    }

    /// <summary>
    /// Performs the scroll operation based on the position and item location.
    /// </summary>
    public static async Task PerformScrollAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(
                () => RunScrollOperationAsync(collectionView, item, position, animated, cancellationToken),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Critical error in PerformScrollAsync, aborting all scroll operations");
        }
    }

    private static async Task RunScrollOperationAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        try
        {
            // For End position (last item), use production-ready approach
            // On Windows, ScrollToPosition.End may not scroll far enough, so we use a more reliable method
            if (position == ScrollToPosition.End)
            {
                await PerformEndPositionScrollAsync(collectionView, item, position, animated, cancellationToken);
            }
            else
            {
                await PerformStandardPositionScrollOnMainThreadAsync(
                    collectionView, item, position, animated, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - this is expected, don't log
        }
        catch (Exception ex)
        {
            HandleScrollException(ex);
        }
    }

    private static Task PerformStandardPositionScrollOnMainThreadAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (!ValidateCollectionViewBeforeScroll(collectionView, cancellationToken))
                {
                    return;
                }

#if WINDOWS
                if (await TryWindowsNativeScrollLastTenAsync(collectionView, item, cancellationToken))
                {
                    return;
                }
#endif

                await ScrollToItemPreferringIndexAsync(collectionView, item, position, animated);
                await Task.Delay(150, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when user taps an item and cancellation token is signalled
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "Error in MainThread scrolling operation");
            }
        });

    /// <summary>
    /// Performs scrolling for items at the end position (last item).
    /// </summary>
    private static async Task PerformEndPositionScrollAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        try
        {
            var scrollView = TryFindParentScrollView(collectionView.Parent);

            if (scrollView != null)
            {
                await InvokeEndScrollInsideScrollViewAsync(collectionView, scrollView, item, position, animated, cancellationToken);
            }
            else
            {
                await InvokeEndScrollStandaloneCollectionViewAsync(collectionView, item, position, animated, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Critical error in PerformEndPositionScrollAsync, aborting scroll operation");
        }
    }

#if WINDOWS
    private static bool IsItemInLastTenPositions(MauiCollectionView collectionView, object item)
    {
        var itemsSource = collectionView.ItemsSource;
        if (itemsSource == null)
        {
            return false;
        }

        var itemsList = itemsSource as IList ?? itemsSource.Cast<object>().ToList();
        if (itemsList.Count == 0)
        {
            return false;
        }

        var itemIndex = FindItemIndexByValue(itemsList, item);
        return itemIndex >= 0 && itemIndex >= itemsList.Count - 10;
    }

    private static async Task<bool> TryWindowsNativeScrollLastTenAsync(
        MauiCollectionView collectionView,
        object item,
        CancellationToken cancellationToken)
    {
        if (!IsItemInLastTenPositions(collectionView, item))
        {
            return false;
        }

        var nativeScrollSuccess =
            await WindowsNativeScrollHelper.ScrollToBottomUsingNativeScrollViewer(collectionView, cancellationToken);
        if (!nativeScrollSuccess)
        {
            return false;
        }

        await Task.Delay(200, cancellationToken);
        collectionView.ScrollTo(item, position: ScrollToPosition.MakeVisible, animate: false);
        return true;
    }
#endif

    private static async Task ScrollToItemPreferringIndexAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated)
    {
        var scrollItemsSource = collectionView.ItemsSource;
        if (scrollItemsSource != null)
        {
            var itemsList = scrollItemsSource as IList ?? scrollItemsSource.Cast<object>().ToList();
            if (itemsList.Count > 0)
            {
                var itemIndex = FindItemIndexByValue(itemsList, item);
                if (itemIndex >= 0)
                {
                    Log.Logger.Debug(
                        "ScrollExecutor: Using index-based scroll to index {Index} for item type {Type}",
                        itemIndex,
                        item.GetType().Name);
                    collectionView.ScrollTo(itemIndex, position: position, animate: animated);
                    return;
                }
            }
        }

        collectionView.ScrollTo(item, position: position, animate: animated);
    }

    private static Microsoft.Maui.Controls.ScrollView? TryFindParentScrollView(Microsoft.Maui.Controls.Element? parent)
    {
        try
        {
            while (parent != null)
            {
                if (parent is Microsoft.Maui.Controls.ScrollView sv)
                {
                    return sv;
                }

                parent = parent.Parent;
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Error traversing parent hierarchy, proceeding with CollectionView-only approach");
        }

        return null;
    }

    private static async Task InvokeEndScrollInsideScrollViewAsync(
        MauiCollectionView collectionView,
        Microsoft.Maui.Controls.ScrollView scrollView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (!ValidateCollectionViewBeforeScroll(collectionView, cancellationToken))
                {
                    return;
                }

                await Task.Delay(300, cancellationToken);
                collectionView.ScrollTo(item, position: position, animate: animated);
                await Task.Delay(400, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "CollectionView.ScrollTo failed, trying ScrollView approach");
                try
                {
                    var contentHeight = scrollView.Content.Height;
                    if (contentHeight > 0)
                    {
                        await scrollView.ScrollToAsync(0, contentHeight, animated);
                    }
                }
                catch (Exception ex2)
                {
                    Log.Logger.Debug(ex2, "ScrollView fallback also failed");
                }
            }
        });
    }

    private static async Task InvokeEndScrollStandaloneCollectionViewAsync(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition position,
        bool animated,
        CancellationToken cancellationToken)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                if (!ValidateCollectionViewBeforeScroll(collectionView, cancellationToken))
                {
                    return;
                }

                await Task.Delay(400, cancellationToken);

#if WINDOWS
                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                {
                    var nativeScrollSuccess =
                        await WindowsNativeScrollHelper.ScrollToBottomUsingNativeScrollViewer(collectionView, cancellationToken);
                    if (nativeScrollSuccess)
                    {
                        await Task.Delay(200, cancellationToken);
                        collectionView.ScrollTo(item, position: ScrollToPosition.MakeVisible, animate: false);
                        return;
                    }
                }
#endif

                collectionView.ScrollTo(item, position: position, animate: animated);
                await Task.Delay(500, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "Standalone CollectionView scrolling failed");
            }
        });
    }

    private static bool ValidateCollectionViewBeforeScroll(MauiCollectionView collectionView, CancellationToken cancellationToken)
    {
        if (collectionView == null || collectionView.Parent == null || collectionView.Handler == null || collectionView.Handler.PlatformView == null)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static void HandleScrollException(Exception ex)
    {
        // Only log unexpected exceptions (not cancellation or COM exceptions which are expected during initialization)
        if (ex is not OperationCanceledException and not COMException and not NullReferenceException)
        {
            Log.Logger.Warning(ex, "Unexpected exception while scrolling CollectionView");
        }
    }

    /// <summary>
    /// Finds the index of an item in the collection using value-based comparison.
    /// This handles cases where the item reference doesn't match due to collection repopulation.
    /// </summary>
    private static int FindItemIndexByValue(IList itemsList, object item)
    {
        for (int i = 0; i < itemsList.Count; i++)
        {
            if (ReferenceEquals(itemsList[i], item))
            {
                return i;
            }

            if (ValueEqualsCollectionItem(itemsList[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ValueEqualsCollectionItem(object? listItem, object item) =>
        item switch
        {
            LanguageListViewItemModel langItem when listItem is LanguageListViewItemModel listLangItem =>
                string.Equals(langItem.Code, listLangItem.Code, StringComparison.OrdinalIgnoreCase),
            PublicationListViewItemModel pubItem when listItem is PublicationListViewItemModel listPubItem =>
                string.Equals(pubItem.Code, listPubItem.Code, StringComparison.OrdinalIgnoreCase),
            BiblePublicationSectionListViewItemModel sectionItem when listItem is BiblePublicationSectionListViewItemModel listSectionItem =>
                string.Equals(sectionItem.SectionCode, listSectionItem.SectionCode, StringComparison.OrdinalIgnoreCase),
            BiblePublicationTrackListViewItemModel trackItem when listItem is BiblePublicationTrackListViewItemModel listTrackItem =>
                CodeComparisonHelper.Equals(trackItem.TrackCode, listTrackItem.TrackCode),
            MusicTrackListViewItemModel musicTrackItem when listItem is MusicTrackListViewItemModel listMusicTrackItem =>
                Bible.Alarm.Shared.Helpers.CodeComparisonHelper.Equals(musicTrackItem.TrackCode, listMusicTrackItem.TrackCode),
            _ => false
        };
}
