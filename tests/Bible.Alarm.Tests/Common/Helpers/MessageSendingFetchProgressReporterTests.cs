#nullable enable

using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Stores.Messages.ListItemProgress;
using CommunityToolkit.Mvvm.Messaging;

namespace Bible.Alarm.Tests;

public sealed class MessageSendingFetchProgressReporterTests
{
    internal sealed class RecordingRecipient : IRecipient<ListItemFetchProgressMessage>, IDisposable
    {
        public double? LastProgress { get; private set; }

        public RecordingRecipient() =>
            WeakReferenceMessenger.Default.Register<ListItemFetchProgressMessage>(this);

        public void Receive(ListItemFetchProgressMessage message) =>
            LastProgress = message.Value.Progress;

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<ListItemFetchProgressMessage>(this);
    }

    [Fact]
    public void Ctor_assigns_cancellation_token_from_argument()
    {
        using var cts = new CancellationTokenSource();
        var sut = new ListItemFetchProgressReporter("ctx", "item", cancellationToken: cts.Token);

        Assert.Equal(cts.Token, sut.CancellationToken);
    }

    [Fact]
    public void UpdateProgress_invokes_callback_once_per_call()
    {
        var calls = 0;
        var sut = new ListItemFetchProgressReporter("ctx", "id", () => calls++, default);

        sut.UpdateProgress(0.1);
        sut.UpdateProgress(0.2);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void UpdateProgress_clamps_high_values_to_one_in_message()
    {
        using var recipient = new RecordingRecipient();
        var sut = new ListItemFetchProgressReporter("MusicPublication", "pub-9", onProgressReported: null, default);

        sut.UpdateProgress(3);

        Assert.Equal(1.0, recipient.LastProgress);
    }

    internal sealed class ThrowingSubscriber : IRecipient<ListItemFetchProgressMessage>, IDisposable
    {
        public ThrowingSubscriber() =>
            WeakReferenceMessenger.Default.Register<ListItemFetchProgressMessage>(this);

        public void Receive(ListItemFetchProgressMessage message) =>
            throw new InvalidOperationException("subscriber");

        public void Dispose() =>
            WeakReferenceMessenger.Default.Unregister<ListItemFetchProgressMessage>(this);
    }

    [Fact]
    public void UpdateProgress_swallows_exceptions_from_list_item_progress_subscribers()
    {
        using var _ = new ThrowingSubscriber();
        var sut = new ListItemFetchProgressReporter("ctx", "id");

        var ex = Record.Exception(() => sut.UpdateProgress(0.5));

        Assert.Null(ex);
    }

    [Fact]
    public void UpdateProgress_swallows_exceptions_from_progress_callback()
    {
        var sut = new ListItemFetchProgressReporter("ctx", "id", () => throw new InvalidOperationException("callback"));

        var ex = Record.Exception(() => sut.UpdateProgress(0.5));

        Assert.Null(ex);
    }

    [Fact]
    public void UpdateProgressText_and_SetIsVisible_are_no_ops()
    {
        var sut = new ListItemFetchProgressReporter("ctx", "id");

        var ex = Record.Exception(() =>
        {
            sut.UpdateProgressText("text");
            sut.SetIsVisible(true);
            sut.SetIsVisible(false);
        });

        Assert.Null(ex);
    }
}
