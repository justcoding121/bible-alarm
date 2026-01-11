#nullable enable
namespace Bible.Alarm.Common.ViewHelpers;

/// <summary>
/// Helper class for ensuring ScrollView content is fully measured before scrolling.
/// </summary>
internal static class ScrollViewContentMeasurer
{
    /// <summary>
    /// Ensures the ScrollView content is fully measured before scrolling.
    /// Uses InvalidateMeasure() and SizeChanged waiting - production patterns for reliable MAUI scrolling.
    /// </summary>
    public static async Task<bool> EnsureContentMeasuredAsync(ScrollView scrollView, CancellationToken cancellationToken)
    {
        try
        {
            // Validate scrollView and content exist
            if (scrollView?.Content == null)
            {
                return false;
            }

            // First, force a layout pass to ensure measurements are current
            if (scrollView.Content is Layout layout)
            {
                try
                {
                    layout.InvalidateMeasure();
                    await Task.Delay(50, cancellationToken);
                }
                catch
                {
                    // Ignore errors
                }
            }

            // Check if content height is already available and reasonable
            var currentHeight = scrollView.Content.Height;
            if (currentHeight > 0)
            {
                return true; // Content already measured
            }

            // Wait for SizeChanged event to ensure content is fully measured
            var sizeChangedTcs = new TaskCompletionSource<bool>();
            void OnSizeChanged(object? sender, EventArgs e)
            {
                try
                {
                    scrollView.Content.SizeChanged -= OnSizeChanged;
                    sizeChangedTcs.TrySetResult(true);
                }
                catch
                {
                    sizeChangedTcs.TrySetResult(false);
                }
            }

            try
            {
                scrollView.Content.SizeChanged += OnSizeChanged;
            }
            catch
            {
                return false;
            }

            // Timeout after 1 second to avoid hanging
            var timeoutTask = Task.Delay(1000, cancellationToken);
            var completedTask = await Task.WhenAny(sizeChangedTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                // Clean up event handler
                try
                {
                    scrollView.Content.SizeChanged -= OnSizeChanged;
                }
                catch
                {
                    // Ignore cleanup errors
                }
                return false;
            }

            return await sizeChangedTcs.Task;
        }
        catch
        {
            return false;
        }
    }
}
