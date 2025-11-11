using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Services.Media;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Views.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class MusicSelectionViewModel : ObservableObject, IDisposable
{
    private AlarmMusic _current;

    private readonly MediaService _mediaService;
    private readonly INavigation _navigation;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    private readonly List<IDisposable> _subscriptions = [];

    public MusicSelectionViewModel(MediaService mediaService, INavigation navigation, IServiceProvider serviceProvider, IDispatcher dispatcher, IState<ApplicationState> state)
    {
        _mediaService = mediaService;
        _navigation = navigation;
        _serviceProvider = serviceProvider;
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


        SongBookSelectionCommand = new AsyncRelayCommand<MusicTypeListItemViewModel>(async x =>
        {
            IsBusy = true;

            if (x.MusicType == MusicType.Vocals)
            {
                _dispatcher.Dispatch(new SongBookSelectionAction(new AlarmMusic
                {
                    MusicType = MusicType.Vocals,
                    LanguageCode = _current.LanguageCode
                }));

                var viewModel = _serviceProvider.GetRequiredService<SongBookSelectionViewModel>();
                var page = _serviceProvider.GetRequiredService<SongBookSelection>();
                page.BindingContext = viewModel;
                await _navigation.PushAsync(page);
            }
            else
            {
                _dispatcher.Dispatch(new TrackSelectionAction(new AlarmMusic
                {
                    Repeat = _current.Repeat,
                    MusicType = MusicType.Melodies,
                    PublicationCode = "iam"
                }));
                var viewModel = _serviceProvider.GetRequiredService<TrackSelectionViewModel>();
                var page = _serviceProvider.GetRequiredService<TrackSelection>();
                page.BindingContext = viewModel;
                await _navigation.PushAsync(page);
            }

            IsBusy = false;
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            await _navigation.PopAsync();
            IsBusy = false;
        });
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