#nullable enable

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
            // Find the parent ScrollView by traversing up the visual tree
            var parent = container.Parent;
            ScrollView? scrollView = null;

            while (parent != null)
            {
                if (parent is ScrollView sv)
                {
                    scrollView = sv;
                    break;
                }
                parent = parent.Parent;
            }

            if (scrollView != null)
            {
                var containerName = container.GetType().Name;
#if DEBUG
                if (scrollToBottom)
                {
                    Serilog.Log.Debug("[{ContainerName}] Found ScrollView, scrolling to bottom", containerName);
                }
                else
                {
                    Serilog.Log.Debug("[{ContainerName}] Found ScrollView, scrolling to element", containerName);
                }
#endif

                // Scroll with animation
                container.Dispatcher.DispatchAsync(async () =>
                {
                    await Task.Delay(150); // Small delay to ensure layout is complete after animation

                    if (scrollToBottom)
                    {
                        // Scroll to the bottom using coordinates
                        // Wait a bit more for content height to be measured
                        await Task.Delay(50);

                        // Try to get the content height
                        var contentHeight = scrollView.Content.Height;
                        if (contentHeight > 0)
                        {
                            await scrollView.ScrollToAsync(0, contentHeight, true);
#if DEBUG
                            Serilog.Log.Debug("[{ContainerName}] Scrolled to bottom (height: {Height})", containerName, contentHeight);
#endif
                        }
                        else
                        {
                            // If height not available, try scrolling to the last child element
                            if (scrollView.Content is Layout layout && layout.Children.Count > 0)
                            {
                                var lastChild = layout.Children[layout.Children.Count - 1];
                                if (lastChild is Element lastElement)
                                {
                                    await scrollView.ScrollToAsync(lastElement, ScrollToPosition.End, true);
#if DEBUG
                                    Serilog.Log.Debug("[{ContainerName}] Scrolled to bottom (last child element)", containerName);
#endif
                                }
                                else
                                {
#if DEBUG
                                    Serilog.Log.Debug("[{ContainerName}] Last child is not an Element, cannot scroll", containerName);
#endif
                                }
                            }
                            else
                            {
#if DEBUG
                                Serilog.Log.Debug("[{ContainerName}] Content height not available and no children found", containerName);
#endif
                            }
                        }
                    }
                    else
                    {
                        // Scroll to the specific element
                        await scrollView.ScrollToAsync(element, ScrollToPosition.MakeVisible, true);
#if DEBUG
                        Serilog.Log.Debug("[{ContainerName}] Scrolled to element", containerName);
#endif
                    }
                });
            }
            else
            {
#if DEBUG
                Serilog.Log.Debug("[{ContainerName}] ScrollView not found", container.GetType().Name);
#endif
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{ContainerName}] Error scrolling", container.GetType().Name);
        }
    }
}

