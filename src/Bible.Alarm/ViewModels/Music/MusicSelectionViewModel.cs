using System.Collections.ObjectModel;
using System.Windows.Input;
using Bible.Alarm.Common;
using Bible.Alarm.Models.Schedule;
using Bible.Alarm.Shared.Models.Enums;
using Bible.Alarm.Stores;
using Bible.Alarm.Stores.Actions.Music;
using Bible.Alarm.Views.Music;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxor;
using IDispatcher = Fluxor.IDispatcher;

namespace Bible.Alarm.ViewModels.Music;

public class MusicSelectionViewModel : ObservableObject
{
    private AlarmMusic _current;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDispatcher _dispatcher;
    private readonly IState<ApplicationState> _state;

    public MusicSelectionViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _state = MauiAppHolder.Services.GetRequiredService<IState<ApplicationState>>();
        _dispatcher = MauiAppHolder.Services.GetRequiredService<IDispatcher>();

        _state.StateChanged += OnStateOnStateChanged;

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

                using var scope = _scopeFactory.CreateScope();
                var navigation = scope.ServiceProvider.GetRequiredService<INavigation>();
                var viewModel = scope.ServiceProvider.GetRequiredService<SongBookSelectionViewModel>();
                var page = scope.ServiceProvider.GetRequiredService<SongBookSelection>();
                page.BindingContext = viewModel;
                await navigation.PushAsync(page);
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
                var navigation = scope.ServiceProvider.GetRequiredService<INavigation>();
                var viewModel = scope.ServiceProvider.GetRequiredService<TrackSelectionViewModel>();
                var page = scope.ServiceProvider.GetRequiredService<TrackSelection>();
                page.BindingContext = viewModel;
                await navigation.PushAsync(page);
            }

            IsBusy = false;
        });

        BackCommand = new AsyncRelayCommand(async () =>
        {
            IsBusy = true;
            using var scope = _scopeFactory.CreateScope();
            var navigation = scope.ServiceProvider.GetRequiredService<INavigation>();
            await navigation.PopAsync();
            IsBusy = false;
        });
        return;

        void OnStateOnStateChanged(object o, EventArgs eventArgs)
        {
            var stateValue = _state.Value;
            if (stateValue.CurrentMusic == null) return;
            _current = stateValue.CurrentMusic;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                SetSelectedMusicType();
                IsBusy = false;
            });
        }
    }

    private void SetSelectedMusicType()
    {
        if (SelectedMusicType != null) SelectedMusicType.IsSelected = false;

        var musicType = MusicTypes.FirstOrDefault(y => y.MusicType == _current.MusicType);
        if (musicType == null) return;
        
        SelectedMusicType = musicType;
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