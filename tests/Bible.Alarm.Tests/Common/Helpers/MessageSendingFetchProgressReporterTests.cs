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
}
