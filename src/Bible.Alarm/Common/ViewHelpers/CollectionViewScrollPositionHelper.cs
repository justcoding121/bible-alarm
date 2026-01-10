#nullable enable
using System.Collections;
using System.Linq;
using Serilog;
using Microsoft.Maui.Controls;
using MauiCollectionView = Microsoft.Maui.Controls.CollectionView;
using Bible.Alarm.ViewModels.Shared;
using Bible.Alarm.ViewModels.Bible;

namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Helper class for determining optimal scroll positions based on item location in the list.
/// </summary>
internal static class CollectionViewScrollPositionHelper
{
    /// <summary>
    /// Determines the optimal scroll position based on where the item is located in the list.
    /// - First item: ScrollToPosition.Start (scroll to beginning)
    /// - Last item: ScrollToPosition.End (scroll to end)
    /// - Items in last 3 positions: ScrollToPosition.MakeVisible (to avoid close button overlap)
    /// - Middle items: ScrollToPosition.Center (center with equal items visible above and below)
    /// </summary>
    public static ScrollToPosition DetermineOptimalScrollPosition(
        MauiCollectionView collectionView,
        object item,
        ScrollToPosition requestedPosition)
    {
        // If position is explicitly set to something other than Center, use it
        if (requestedPosition != ScrollToPosition.Center)
        {
            return requestedPosition;
        }

        try
        {
            // Get the ItemsSource to determine item position
            var itemsSource = collectionView.ItemsSource;
            if (itemsSource == null)
            {
                return requestedPosition;
            }

            // Convert to enumerable to find index
            var itemsList = itemsSource as IList ?? itemsSource.Cast<object>().ToList();
            if (itemsList == null || itemsList.Count == 0)
            {
                return requestedPosition;
            }

            // Find the index of the item
            int itemIndex = FindItemIndex(itemsList, item);

            if (itemIndex == -1)
            {
                // Item not found, use requested position
                return requestedPosition;
            }

            var totalItems = itemsList.Count;

            // First item: scroll to start
            if (itemIndex == 0)
            {
                return ScrollToPosition.Start;
            }

            // Last item: use End to position at bottom (accounting for close button margin)
            if (itemIndex == totalItems - 1)
            {
                return ScrollToPosition.End;
            }

            // Items in the last 3 positions: use MakeVisible to ensure they're not hidden by close button
            // This is especially important for items like Yoruba (second-to-last) which might be hidden
            if (itemIndex >= totalItems - 3)
            {
                return ScrollToPosition.MakeVisible;
            }

            // Middle items: center with equal items visible above and below
            return ScrollToPosition.Center;
        }
        catch
        {
            // If we can't determine position, fall back to requested position
            return requestedPosition;
        }
    }

    private static int FindItemIndex(IList itemsList, object item)
    {
        for (int i = 0; i < itemsList.Count; i++)
        {
            // First try reference equality (fastest)
            if (ReferenceEquals(itemsList[i], item))
            {
                return i;
            }

            // For virtual scrolling scenarios, also try value equality for known model types
            // This handles cases where the item passed is a different instance than the one in the collection
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
                     sectionItem.Number == listSectionItem.Number)
            {
                return i;
            }
            else if (item is BiblePublicationTrackListViewItemModel trackItem &&
                     itemsList[i] is BiblePublicationTrackListViewItemModel listTrackItem &&
                     trackItem.Number == listTrackItem.Number)
            {
                return i;
            }
        }

        return -1;
    }
}
