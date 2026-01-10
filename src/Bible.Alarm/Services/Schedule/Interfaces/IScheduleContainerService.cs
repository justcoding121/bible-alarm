#nullable enable
using Bible.Alarm.ViewModels.Schedule;

namespace Bible.Alarm.Services.Schedule.Interfaces;

public interface IScheduleContainerService
{
    Task InitializeContainersAsync(
        IServiceProvider serviceProvider,
        Action<BiblePublicationSelectionContainerViewModel, MusicSelectionContainerViewModel, NumberOfTrackContainerViewModel, ScheduleDetailsContainerViewModel> onContainersReady);
}

