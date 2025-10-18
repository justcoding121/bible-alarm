using Bible.Alarm.Common.Mvvm;
using Mvvmicro;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.ViewModels.Shared
{
    public class MediaProgressViewModal : ViewModel
    {
        public MediaProgressViewModal()
        {
            var syncContext = ServiceProviderManager.GetService<TaskScheduler>();

            Messenger<object>.Subscribe(MvvmMessages.MediaProgress, async vm =>
            {
                await Task.Delay(0).ContinueWith((x) =>
                {
                    var kv = vm as Tuple<int, int>;
                    _loadedTracks = kv.Item1;
                    _totalTracks = kv.Item2;
                    Progress = (double)kv.Item1 / (double)kv.Item2;
                    Raise("ProgressText");
                    Raise("Progress");
                }, syncContext);
            });
        }

        private int _loadedTracks;
        private int _totalTracks;

        public string ProgressText { get => $"Preparing tracks {(_totalTracks > 0 ? $"{_loadedTracks}/{_totalTracks}" : "")}.."; }
        public double Progress { get; private set; }

        public void Dispose()
        {

        }
    }
}
