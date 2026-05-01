#nullable enable

using Bible.Alarm.Shared.Constants;
using Serilog;

namespace Bible.Alarm.Views.Schedule.MusicSelectionContainerHelpers;

/// <summary>
/// Manages scrolling to expanded content in MusicSelectionContainer.
/// </summary>
public class ScrollManager
{
    private readonly View container;

    public ScrollManager(View container)
    {
        this.container = container;
    }

    public void ScrollToExpandedContent()
    {
        ScrollToElement(container, scrollToBottom: true);
    }

    /// <summary>
    /// Scrolls to a specific element in the parent ScrollView.
    /// </summary>
    /// <param name="element">The element to scroll to</param>
    /// <param name="scrollToBottom">If true, scrolls to the bottom of the ScrollView. If false, scrolls to the element itself.</param>
    public void ScrollToElement(Element element, bool scrollToBottom = false)
    {
        try
        {
            var scrollView = FindAncestorScrollView(container);
            if (scrollView == null)
            {
#if DEBUG
                Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerScrollViewNotFound, container.GetType().Name);
#endif
                return;
            }

#if DEBUG
            LogScrollIntent(scrollToBottom);
#endif

            var containerName = container.GetType().Name;
            container.Dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(150);
                if (scrollToBottom)
                {
                    await ScrollToBottomAsync(scrollView, containerName);
                }
                else
                {
                    await ScrollToElementAsync(scrollView, element, containerName);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerErrorScrolling, container.GetType().Name);
        }
    }

    private static ScrollView? FindAncestorScrollView(Element start)
    {
        var parent = start.Parent;
        while (parent != null)
        {
            if (parent is ScrollView sv)
            {
                return sv;
            }

            parent = parent.Parent;
        }

        return null;
    }

#if DEBUG
    private void LogScrollIntent(bool scrollToBottom)
    {
        var containerName = container.GetType().Name;
        if (scrollToBottom)
        {
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerFoundScrollViewScrollingToBottom, containerName);
        }
        else
        {
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerFoundScrollViewScrollingToElement, containerName);
        }
    }
#endif

    private static async Task ScrollToBottomAsync(ScrollView scrollView, string containerName)
    {
        await Task.Delay(50);

        var contentHeight = scrollView.Content.Height;
        if (contentHeight > 0)
        {
            await scrollView.ScrollToAsync(0, contentHeight, true);
#if DEBUG
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerScrolledToBottomHeight, containerName, contentHeight);
#endif
            return;
        }

        if (scrollView.Content is Layout layout && layout.Children.Count > 0)
        {
            var lastChild = layout.Children[layout.Children.Count - 1];
            if (lastChild is Element lastElement)
            {
                await scrollView.ScrollToAsync(lastElement, ScrollToPosition.End, true);
#if DEBUG
                Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerScrolledToBottomLastChildElement, containerName);
#endif
            }
#if DEBUG
            else
            {
                Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerLastChildNotElementCannotScroll, containerName);
            }
#endif
        }
#if DEBUG
        else
        {
            Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerContentHeightNotAvailableNoChildren, containerName);
        }
#endif
    }

    private static async Task ScrollToElementAsync(ScrollView scrollView, Element element, string containerName)
    {
        await scrollView.ScrollToAsync(element, ScrollToPosition.MakeVisible, true);
#if DEBUG
        Serilog.Log.Debug(AppConstants.Logging.MusicSelectionContainerDiagnosticsLog.ScrollContainerScrolledToElement, containerName);
#endif
    }
}
