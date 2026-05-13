#nullable enable

using CommunityToolkit.Maui.Services;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class SafeFireAndForgetExtensionsTests
{
    [Fact]
    public void SafeFireAndForget_invokes_exception_handler_when_task_faults()
    {
        Exception? caught = null;
        Task.FromException(new InvalidOperationException("failed")).SafeFireAndForget(ex => caught = ex);

        Assert.NotNull(caught);
        Assert.Equal("failed", caught.Message);
    }

    [Fact]
    public void SafeFireAndForget_does_not_invoke_exception_handler_when_task_completes()
    {
        Exception? caught = null;
        Task.CompletedTask.SafeFireAndForget(ex => caught = ex);

        Assert.Null(caught);
    }

    [Fact]
    public void SafeFireAndForget_Typed_invokes_handler_only_for_matching_exception_type()
    {
        InvalidOperationException? caught = null;
        Task.FromException(new InvalidOperationException("typed")).SafeFireAndForget<InvalidOperationException>(
            ex => caught = ex);

        Assert.NotNull(caught);
        Assert.Equal("typed", caught.Message);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void SafeFireAndForget_with_ConfigureAwaitOptions_invokes_exception_handler_when_task_faults()
    {
        Exception? caught = null;
        Task.FromException(new IOException("io")).SafeFireAndForget(
            ConfigureAwaitOptions.None,
            ex => caught = ex);

        Assert.NotNull(caught);
        Assert.Equal("io", caught.Message);
    }
#endif
}
