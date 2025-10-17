using Bible.Alarm.Common.Mvvm;
using Mvvmicro;
using System;
using System.Threading.Tasks;

namespace Bible.Alarm.ViewModels.Shared
{
    public class MediaProgressViewModal : ViewModel
    {
        private readonly IContainer container;

        public MediaProgressViewModal(IContainer container)
        {
            this.container = container;

            var syncContext = this.container.Resolve<TaskScheduler>();

            Messenger<object>.Subscribe(MvvmMessages.MediaProgress, async vm =>
            {
                await Task.Delay(0).ContinueWith((x) =>
                {
                    var kv = vm as Tuple<int, int>;
                    loadedTracks = kv.Item1;
                    totalTracks = kv.Item2;
                    Progress = (double)kv.Item1 / (double)kv.Item2;
                    Raise("ProgressText");
                    Raise("Progress");
                }, syncContext);
            });
        }

        private int loadedTracks;
        private int totalTracks;

        public string ProgressText { get => $"Preparing tracks {(totalTracks > 0 ? $"{loadedTracks}/{totalTracks}" : "")}.."; }
        public double Progress { get; private set; }

        public void Dispose()
        {

        }
    }
}
