#nullable enable
using System.Collections;
using System.Runtime.InteropServices;
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
            await Task.Run(async () =>
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
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            try
                            {
                                if (!ValidateCollectionViewBeforeScroll(collectionView, cancellationToken))
                                {
                                    return;
                                }

#if WINDOWS
                                // Check if item is in the last 10 positions - use native scrolling for those on Windows
                                if (DeviceInfo.Platform == DevicePlatform.WinUI)
                                {
                                    bool isInLast10 = false;
                                        var itemsSource = collectionView.ItemsSource;
                                        if (itemsSource != null)
                                        {
                                            var itemsList = itemsSource as IList ?? itemsSource.Cast<object>().ToList();
                                            if (itemsList.Count > 0)
                                        {
                                            // Find the item index
                                            int itemIndex = -1;
                                            for (int i = 0; i < itemsList.Count; i++)
                                            {
                                                if (ReferenceEquals(itemsList[i], item) ||
                                                    (item is LanguageListViewItemModel targetLang && itemsList[i] is LanguageListViewItemModel listLang && targetLang.Code == listLang.Code))
                                                {
                                                    itemIndex = i;
                                                    break;
                                                }
                                            }

                                            // Check if item is in the last 10 positions
                                            if (itemIndex >= 0 && itemIndex >= itemsList.Count - 10)
                                            {
                                                isInLast10 = true;
                                            }
                                        }
                                    }

                                    // For items in last 10 positions on Windows, try native ScrollViewer scrolling first
                                    if (isInLast10)
                                    {
                                        // First scroll to bottom using native ScrollViewer
                                        var nativeScrollSuccess = await WindowsNativeScrollHelper.ScrollToBottomUsingNativeScrollViewer(collectionView, cancellationToken);
                                        if (nativeScrollSuccess)
                                        {
                                            // Then scroll to the specific item to ensure it's visible and selected
                                            await Task.Delay(200, cancellationToken);
                                            collectionView.ScrollTo(item, position: ScrollToPosition.MakeVisible, animate: false);
                                            return;
                                        }
                                    }
                                }
#endif

                                // Try to find the item's index for more reliable scrolling
                                // This handles cases where the item reference doesn't match due to collection repopulation
                                var scrollSuccessful = false;
                                var scrollItemsSource = collectionView.ItemsSource;
                                if (scrollItemsSource != null)
                                {
                                    var itemsList = scrollItemsSource as IList ?? scrollItemsSource.Cast<object>().ToList();
                                    if (itemsList.Count > 0)
                                    {
                                        int itemIndex = FindItemIndexByValue(itemsList, item);
                                        if (itemIndex >= 0)
                                        {
                                            Log.Logger.Debug("ScrollExecutor: Using index-based scroll to index {Index} for item type {Type}", 
                                                itemIndex, item.GetType().Name);
                                            collectionView.ScrollTo(itemIndex, position: position, animate: animated);
                                            scrollSuccessful = true;
                                        }
                                    }
                                }
                                
                                if (!scrollSuccessful)
                                {
                                    // Fallback to item-based scrolling
                                    collectionView.ScrollTo(item, position: position, animate: animated);
                                }

                                // Delay after scrolling to let the layout settle
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
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Critical error in PerformScrollAsync, aborting all scroll operations");
        }
    }

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
            var parent = collectionView.Parent;
            Microsoft.Maui.Controls.ScrollView? scrollView = null;

            // Safely traverse parent hierarchy
            try
            {
                while (parent != null)
                {
                    if (parent is Microsoft.Maui.Controls.ScrollView sv)
                    {
                        scrollView = sv;
                        break;
                    }
                    parent = parent.Parent;
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Debug(ex, "Error traversing parent hierarchy, proceeding with CollectionView-only approach");
            }

            if (scrollView != null)
            {
                // Virtual scrolling scenario: CollectionView inside ScrollView
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
            else
            {
                // Standalone CollectionView (most common virtual scrolling case)
                // On Windows, try native ScrollViewer scrolling first (more reliable)
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
                        // On Windows, try native ScrollViewer scrolling first (simulates mouse scroll)
                        if (DeviceInfo.Platform == DevicePlatform.WinUI)
                        {
                            var nativeScrollSuccess = await WindowsNativeScrollHelper.ScrollToBottomUsingNativeScrollViewer(collectionView, cancellationToken);
                            if (nativeScrollSuccess)
                            {
                                // Also do a CollectionView.ScrollTo to ensure the item is selected/highlighted
                                await Task.Delay(200, cancellationToken);
                                collectionView.ScrollTo(item, position: ScrollToPosition.MakeVisible, animate: false);
                                return;
                            }
                        }
#endif

                        // Fallback to CollectionView.ScrollTo
                        collectionView.ScrollTo(item, position: position, animate: animated);
                        await Task.Delay(500, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Debug(ex, "Standalone CollectionView scrolling failed");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Debug(ex, "Critical error in PerformEndPositionScrollAsync, aborting scroll operation");
        }
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
            // First try reference equality (fastest)
            if (ReferenceEquals(itemsList[i], item))
            {
                return i;
            }

            // For virtual scrolling scenarios, also try value equality for known model types
            if (item is LanguageListViewItemModel langItem &&
                itemsList[i] is LanguageListViewItemModel listLangItem &&
                langItem.Code == listLangItem.Code)
            {
                return i;
            }
            else if (item is PublicationListViewItemModel pubItem &&
                     itemsList[i] is PublicationListViewItemModel listPubItem &&
                     pubItem.Code == listPubItem.Code)
            {
                return i;
            }
            else if (item is BiblePublicationSectionListViewItemModel sectionItem &&
                     itemsList[i] is BiblePublicationSectionListViewItemModel listSectionItem &&
                     string.Equals(sectionItem.SectionCode, listSectionItem.SectionCode, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
            else if (item is BiblePublicationTrackListViewItemModel trackItem &&
                     itemsList[i] is BiblePublicationTrackListViewItemModel listTrackItem &&
                     trackItem.TrackCode == listTrackItem.TrackCode)
            {
                return i;
            }
            else if (item is MusicTrackListViewItemModel musicTrackItem &&
                     itemsList[i] is MusicTrackListViewItemModel listMusicTrackItem &&
                     Bible.Alarm.Shared.Helpers.CodeComparisonHelper.Equals(musicTrackItem.TrackCode, listMusicTrackItem.TrackCode))
            {
                return i;
            }
        }

        return -1;
    }
}
