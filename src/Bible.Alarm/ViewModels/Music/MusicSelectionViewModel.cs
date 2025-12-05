using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class MusicSelectionViewModel : ObservableObject, IDisposable
{
    private AlarmMusic _current;

    private readonly IState<ApplicationState> _state;
    private readonly IDispatcher _dispatcher;

    public MusicSelectionViewModel(IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService)
    {
        _state = state;
        _dispatcher = dispatcher;

        // Initialize _current from state if available
        if (_state.Value.CurrentMusic != null)
        {
            _current = _state.Value.CurrentMusic;
        }

        _state.StateChanged += OnStateOnStateChanged;

        SongBookSelectionCommand = new AsyncRelayCommand<MusicTypeListItemViewModel>(async x =>
        {
            if (x == null) return;
            
            IsBusy = true;

            // Ensure _current is set from state if it's null
            if (_current == null)
            {
                _current = _state.Value.CurrentMusic;
            }

            if (x.MusicType == MusicType.Vocals)
            {
                await navigationService.NavigateToSongBookSelectionAsync();

                _dispatcher.Dispatch(new SongBookSelectionAction(new AlarmMusic
                {
                    MusicType = MusicType.Vocals,
                    LanguageCode = _current?.LanguageCode
                }));
  
            }
            else
            {
                await navigationService.NavigateToTrackSelectionAsync();

                _dispatcher.Dispatch(new TrackSelectionAction(new AlarmMusic
                {
                    Repeat = _current?.Repeat ?? false,
                    MusicType = MusicType.Melodies,
                    PublicationCode = "iam"
                }));
               
            }

            IsBusy = false;
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await navigationService.PopAsync();
            IsBusy = false;
        });
    }

    private void OnStateOnStateChanged(object o, EventArgs eventArgs)
    {
        var stateValue = _state.Value;
        if (stateValue.CurrentMusic == null) return;
        _current = stateValue.CurrentMusic;
        Task.Run(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = true);
            
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SetSelectedMusicType();
            });
            
            // CollectionView needs a moment to render before hiding the busy indicator
            // Add a small delay to prevent blank page flash (following chapter/track selection pattern)
            // Give CollectionView time to render
            await Task.Delay(100);
            
            // Set IsBusy to false after collection is assigned and rendered
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        });
    }

    private void SetSelectedMusicType()
    {
        if (SelectedMusicType != null) SelectedMusicType.IsSelected = false;

        if (_current == null) return;

        var musicType = MusicTypes.FirstOrDefault(y => y.MusicType == _current.MusicType);
        if (musicType == null) return;
        
        SelectedMusicType = musicType;
        SelectedMusicType.IsSelected = true;
    }

    // Start as true to show busy indicator immediately
    private bool _isBusy = true;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public ICommand BackCommand { get; set; }
    public ICommand SongBookSelectionCommand { get; set; }

    public ObservableCollection<MusicTypeListItemViewModel> MusicTypes { get; set; }
        = new(
        [
            new MusicTypeListItemViewModel
            {
                MusicType = MusicType.Melodies,
                Name = "Orchestral Melodies"
            },
            new MusicTypeListItemViewModel
            {
                MusicType = MusicType.Vocals,
                Name = "Vocals"
            }
        ]);

    private MusicTypeListItemViewModel _selectedMusicType;

    public MusicTypeListItemViewModel SelectedMusicType
    {
        get => _selectedMusicType;
        set => SetProperty(ref _selectedMusicType, value);
    }

    public void Dispose()
    {
        _state.StateChanged -= OnStateOnStateChanged;
    }
}

public class MusicTypeListItemViewModel : ObservableObject, IComparable
{
    public MusicType MusicType { get; set; }
    public string Name { get; set; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int CompareTo(object obj)
    {
        return obj is not MusicTypeListItemViewModel other ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);
    }
}