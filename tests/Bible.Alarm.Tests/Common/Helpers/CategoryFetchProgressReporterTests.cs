#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Stores.Messages.CategoryProgress;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Tests;

public sealed class CategoryFetchProgressReporterTests
{
    internal sealed class RecordingRecipient : IRecipient<CategoryFetchProgressMessage>, IDisposable
    {
        public double? LastProgress { get; private set; }

        public RecordingRecipient() =>
            WeakReferenceMessenger.Default.Register<CategoryFetchProgressMessage>(this);

        public void Receive(CategoryFetchProgressMessage message) =>
            LastProgress = message.Value.Progress;

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<CategoryFetchProgressMessage>(this);
    }

    [Fact]
    public void UpdateProgress_clamps_negative_values_to_zero_in_message()
    {
        using var recipient = new RecordingRecipient();
        var sut = new CategoryFetchProgressReporter(7);

        sut.UpdateProgress(-0.5);

        Assert.Equal(0.0, recipient.LastProgress);
    }

    [Fact]
    public void UpdateProgress_clamps_values_above_one_in_message()
    {
        using var recipient = new RecordingRecipient();
        var sut = new CategoryFetchProgressReporter(3);

        sut.UpdateProgress(1.9);

        Assert.Equal(1.0, recipient.LastProgress);
    }

    [Fact]
    public void Ctor_assigns_cancellation_token_from_argument()
    {
        using var cts = new CancellationTokenSource();
        var sut = new CategoryFetchProgressReporter(2, cts.Token);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    internal sealed class ThrowingSubscriber : IRecipient<CategoryFetchProgressMessage>, IDisposable
    {
        public ThrowingSubscriber() =>
            WeakReferenceMessenger.Default.Register<CategoryFetchProgressMessage>(this);

        public void Receive(CategoryFetchProgressMessage message) =>
            throw new InvalidOperationException("subscriber");

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<CategoryFetchProgressMessage>(this);
    }

    [Fact]
    public void UpdateProgress_swallows_exceptions_from_category_progress_subscribers()
    {
        using var _ = new ThrowingSubscriber();
        var sut = new CategoryFetchProgressReporter(4);

        var ex = Record.Exception(() => sut.UpdateProgress(0.25));

        Assert.Null(ex);
    }

    [Fact]
    public void UpdateProgressText_and_SetIsVisible_are_no_ops()
    {
        var sut = new CategoryFetchProgressReporter(1);

        var ex = Record.Exception(() =>
        {
            sut.UpdateProgressText("loading");
            sut.SetIsVisible(true);
            sut.SetIsVisible(false);
        });

        Assert.Null(ex);
    }
}
