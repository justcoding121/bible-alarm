using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Bible.Alarm.Common.Interfaces.UI;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Fluxor;

namespace Bible.Alarm.ViewModels.Music;

public class MusicSelectionViewModel : ObservableObject, IDisposable
{
    private AlarmMusic _current;

    private readonly MediaService _mediaService;
    private readonly INavigationService _navigationService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Fluxor.IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    private readonly List<IDisposable> _subscriptions = [];

    public MusicSelectionViewModel(MediaService mediaService, INavigationService navigationService, IServiceScopeFactory scopeFactory, Fluxor.IDispatcher dispatcher, IState<ApplicationState> state)
    {
        _mediaService = mediaService;
        _navigationService = navigationService;
        _scopeFactory = scopeFactory;
        _dispatcher = dispatcher;
        _state = state;

        //set schedules from initial state.
        _state.StateChanged += (sender, e) =>
        {
            var state = _state.Value;
            if (state.CurrentMusic != null)
            {
                _current = state.CurrentMusic;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetSelectedMusicType();
                    IsBusy = false;
                });
            }
        };


        SongBookSelectionCommand = new Command<MusicTypeListItemViewModel>(async x =>
        {
            IsBusy = true;

            if (x.MusicType == MusicType.Vocals)
            {
                _dispatcher.Dispatch(new SongBookSelectionAction(new AlarmMusic
                {
                    MusicType = MusicType.Vocals,
                    LanguageCode = _current.LanguageCode
                }));

                using var scope = _scopeFactory.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<SongBookSelectionViewModel>();
                await _navigationService.Navigate(viewModel);
            }
            else
            {
                _dispatcher.Dispatch(new TrackSelectionAction(new AlarmMusic
                {
                    Repeat = _current.Repeat,
                    MusicType = MusicType.Melodies,
                    PublicationCode = "iam"
                }));
                using var scope = _scopeFactory.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<TrackSelectionViewModel>();
                await _navigationService.Navigate(viewModel);
            }

            IsBusy = false;
        });

        BackCommand = new Command(async () =>
        {
            IsBusy = true;
            await _navigationService.GoBack();
            IsBusy = false;
        });

        _navigationService.NavigatedBack += OnNavigated;
    }

    private void OnNavigated(object viewModal)
    {
        if (viewModal.GetType() == GetType()) SetSelectedMusicType();
    }

    private void SetSelectedMusicType()
    {
        if (SelectedMusicType != null) SelectedMusicType.IsSelected = false;

        SelectedMusicType = MusicTypes.First(y => y.MusicType == _current.MusicType);
        SelectedMusicType.IsSelected = true;
    }

    private bool _isBusy;

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
            new()
            {
                MusicType = MusicType.Melodies,
                Name = "Orchestral Melodies"
            },
            new()
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
        _navigationService.NavigatedBack -= OnNavigated;
        _subscriptions.ForEach(x => x.Dispose());

        // Note: _mediaService (MediaService) is a singleton and should not be 
        // disposed here as it is managed by the DI container
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
        return Name.CompareTo((obj as MusicTypeListItemViewModel).Name);
    }
}