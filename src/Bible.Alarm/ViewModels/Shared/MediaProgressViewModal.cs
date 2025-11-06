using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Bible.Alarm.Common.Messenger;

namespace Bible.Alarm.ViewModels.Shared;

public class MediaProgressViewModal : ObservableObject, IDisposable, IRecipient<MediaProgressMessage>
{
    private readonly TaskScheduler _taskScheduler;

    public MediaProgressViewModal(TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        WeakReferenceMessenger.Default.Register<MediaProgressMessage>(this);
    }

    public void Receive(MediaProgressMessage message)
    {
        Task.Delay(0).ContinueWith((x) =>
        {
            var kv = message.Value as Tuple<int, int>;
            if (kv != null)
            {
                _loadedTracks = kv.Item1;
                _totalTracks = kv.Item2;
                Progress = (double)kv.Item1 / (double)kv.Item2;
                OnPropertyChanged(nameof(ProgressText));
                OnPropertyChanged(nameof(Progress));
            }
        }, _taskScheduler);
    }

    private int _loadedTracks;
    private int _totalTracks;

    public string ProgressText => $"Preparing tracks {(_totalTracks > 0 ? $"{_loadedTracks}/{_totalTracks}" : "")}..";
    public double Progress { get; private set; }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Unregister<MediaProgressMessage>(this);
    }
}