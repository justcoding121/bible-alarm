#nullable enable

using Microsoft.Maui.Controls;

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
#if DEBUG
                Serilog.Log.Debug("[MusicSelectionContainer] Found ScrollView, scrolling to bottom");
#endif

                // Scroll to bottom with animation
                container.Dispatcher.DispatchAsync(async () =>
                {
                    await Task.Delay(150); // Small delay to ensure layout is complete after animation

                    // Scroll to the bottom using coordinates
                    // Wait a bit more for content height to be measured
                    await Task.Delay(50);

                    // Try to get the content height
                    var contentHeight = scrollView.Content.Height;
                    if (contentHeight > 0)
                    {
                        await scrollView.ScrollToAsync(0, contentHeight, true);
#if DEBUG
                        Serilog.Log.Debug("[MusicSelectionContainer] Scrolled to bottom (height: {Height})", contentHeight);
#endif
                    }
                    else
                    {
                        // If height not available, try scrolling to the last child element
                        if (scrollView.Content is Layout layout && layout.Children.Count > 0)
                        {
                            var lastChild = layout.Children[layout.Children.Count - 1];
                            if (lastChild is Element element)
                            {
                                await scrollView.ScrollToAsync(element, ScrollToPosition.End, true);
#if DEBUG
                                Serilog.Log.Debug("[MusicSelectionContainer] Scrolled to bottom (last child element)");
#endif
                            }
                            else
                            {
#if DEBUG
                                Serilog.Log.Debug("[MusicSelectionContainer] Last child is not an Element, cannot scroll");
#endif
                            }
                        }
                        else
                        {
#if DEBUG
                            Serilog.Log.Debug("[MusicSelectionContainer] Content height not available and no children found");
#endif
                        }
                    }
                });
            }
            else
            {
#if DEBUG
                Serilog.Log.Debug("[MusicSelectionContainer] ScrollView not found");
#endif
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Serilog.Log.Debug(ex, "[MusicSelectionContainer] Error scrolling");
#endif
        }
    }
}

