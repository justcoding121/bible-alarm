#nullable enable
using System.ComponentModel;

namespace Bible.Alarm.ViewModels.Music.MusicPublicationSelectionViewModelHelpers;

/// <summary>
/// Forwards PropertyChanged events from MusicPublicationSelectionPropertyManager to the ViewModel.
/// </summary>
public static class MusicPublicationSelectionPropertyForwarder
{
    public static PropertyChangedEventHandler CreateHandler(Action<string> onPropertyChanged)
    {
        return (_, e) =>
        {
            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.IsBusy))
            {
                onPropertyChanged("IsBusy");
                onPropertyChanged("ShowCancelButton");
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.ShowProgress))
            {
                onPropertyChanged("ShowProgress");
                onPropertyChanged("ShowCancelButton");
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.ProgressPercent))
            {
                onPropertyChanged("ProgressPercent");
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.ProgressText))
            {
                onPropertyChanged("ProgressText");
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.CanCancelFetch))
            {
                onPropertyChanged("CanCancelFetch");
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.HasFetchError))
            {
                onPropertyChanged("HasFetchError");
                onPropertyChanged("ShowCancelButton");
                return;
            }

            if (e.PropertyName == nameof(MusicPublicationSelectionPropertyManager.IsCancelBusy))
            {
                onPropertyChanged("IsCancelBusy");
                return;
            }
        };
    }
}
