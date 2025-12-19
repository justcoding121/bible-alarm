using System.Collections.ObjectModel;
using System.Windows.Input;
using AutoMapper;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.UI.Interfaces;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Stores.Models;
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
    private readonly IMapper _mapper;

    public MusicSelectionViewModel(IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        _state = state;
        _dispatcher = dispatcher;
        _mapper = mapper;

        // Initialize _current from state if available (map DTO to entity)
        if (_state.Value.CurrentMusic != null)
        {
            _current = _mapper.Map<AlarmMusic>(_state.Value.CurrentMusic);
        }

        _state.StateChanged += OnStateOnStateChanged;

        SongBookSelectionCommand = new AsyncRelayCommand<MusicTypeListItemViewModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            // Ensure _current is set from state if it's null
            if (_current == null)
            {
                var currentItem = _state.Value.CurrentMusic;
                if (currentItem != null)
                {
                    _current = _mapper.Map<AlarmMusic>(currentItem);
                }
            }

            if (x.MusicType == MusicType.Vocals)
            {
                await navigationService.NavigateToSongBookSelectionAsync();

                // Map entity to DTO before dispatching
                var songBookItem = new MusicStateItem
                {
                    MusicType = MusicType.Vocals,
                    LanguageCode = _current?.LanguageCode
                };
                _dispatcher.Dispatch(new SongBookSelectionAction(songBookItem));

            }
            else
            {
                await navigationService.NavigateToTrackSelectionAsync();

                // Map entity to DTO before dispatching
                var trackItem = new MusicStateItem
                {
                    Repeat = _current?.Repeat ?? false,
                    MusicType = MusicType.Melodies,
                    PublicationCode = "iam"
                };
                _dispatcher.Dispatch(new TrackSelectionAction(trackItem));

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
        if (stateValue.CurrentMusic == null)
        {
            return;
        }
        // Map DTO to entity
        _current = _mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
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
        if (SelectedMusicType != null)
        {
            SelectedMusicType.IsSelected = false;
        }

        if (_current == null)
        {
            return;
        }

        var musicType = MusicTypes.FirstOrDefault(y => y.MusicType == _current.MusicType);
        if (musicType == null)
        {
            return;
        }

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