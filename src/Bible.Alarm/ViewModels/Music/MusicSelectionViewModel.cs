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
    private AlarmMusic current;

    private readonly IState<ApplicationState> state;
    private readonly IDispatcher dispatcher;
    private readonly IMapper mapper;

    public MusicSelectionViewModel(IServiceScopeFactory scopeFactory, IState<ApplicationState> state, IDispatcher dispatcher, INavigationService navigationService, IMapper mapper)
    {
        this.state = state;
        this.dispatcher = dispatcher;
        this.mapper = mapper;

        // Initialize _current from state if available (map DTO to entity)
        if (this.state.Value.CurrentMusic != null)
        {
            current = this.mapper.Map<AlarmMusic>(this.state.Value.CurrentMusic);
        }

        this.state.StateChanged += OnStateOnStateChanged;

        SongBookSelectionCommand = new AsyncRelayCommand<MusicTypeListItemViewModel>(async x =>
        {
            if (x == null)
            {
                return;
            }

            IsBusy = true;

            // Ensure _current is set from state if it's null
            if (current == null)
            {
                var currentItem = this.state.Value.CurrentMusic;
                if (currentItem != null)
                {
                    current = this.mapper.Map<AlarmMusic>(currentItem);
                }
            }

            if (x.MusicType == MusicType.Vocals)
            {
                await navigationService.NavigateToSongBookSelectionAsync();

                // Map entity to DTO before dispatching
                var songBookItem = new MusicStateItem
                {
                    MusicType = MusicType.Vocals,
                    LanguageCode = current?.LanguageCode
                };
                this.dispatcher.Dispatch(new SongBookSelectionAction(songBookItem));

            }
            else
            {
                await navigationService.NavigateToTrackSelectionAsync();

                // Map entity to DTO before dispatching
                var trackItem = new MusicStateItem
                {
                    Repeat = current?.Repeat ?? false,
                    MusicType = MusicType.Melodies,
                    PublicationCode = "iam"
                };
                this.dispatcher.Dispatch(new TrackSelectionAction(trackItem));

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
        var stateValue = state.Value;
        if (stateValue.CurrentMusic == null)
        {
            return;
        }
        // Map DTO to entity
        current = mapper.Map<AlarmMusic>(stateValue.CurrentMusic);
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

        if (current == null)
        {
            return;
        }

        var musicType = MusicTypes.FirstOrDefault(y => y.MusicType == current.MusicType);
        if (musicType == null)
        {
            return;
        }

        SelectedMusicType = musicType;
        SelectedMusicType.IsSelected = true;
    }

    // Start as true to show busy indicator immediately
    private bool isBusy = true;

    public bool IsBusy
    {
        get => isBusy;
        set => SetProperty(ref isBusy, value);
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

    private MusicTypeListItemViewModel selectedMusicType;

    public MusicTypeListItemViewModel SelectedMusicType
    {
        get => selectedMusicType;
        set => SetProperty(ref selectedMusicType, value);
    }

    public void Dispose() => state.StateChanged -= OnStateOnStateChanged;
}

public class MusicTypeListItemViewModel : ObservableObject, IComparable
{
    public MusicType MusicType { get; set; }
    public string Name { get; set; }

    private bool isSelected;

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    public int CompareTo(object obj) => obj is not MusicTypeListItemViewModel other ? 1 : string.Compare(Name, other.Name, StringComparison.Ordinal);
}
