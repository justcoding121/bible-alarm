using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions;
using Bible.Alarm.ViewModels.Redux.Actions.Music;
using Mvvmicro;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Bible.Alarm.ViewModels;

public class MusicSelectionViewModel : ViewModel, IDisposable
{
    private AlarmMusic _current;

    private readonly MediaService _mediaService;
    private readonly INavigationService _navigationService;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly List<IDisposable> _subscriptions = [];

    public MusicSelectionViewModel(MediaService mediaService, INavigationService navigationService, IServiceScopeFactory scopeFactory)
    {
        _mediaService = mediaService;
        _navigationService = navigationService;
        _scopeFactory = scopeFactory;

        //set schedules from initial state.
        //this should fire only once 
        var subscription1 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
            .Select(state => state.CurrentMusic)
            .Where(x => x != null)
            .DistinctUntilChanged()
            .Subscribe(x =>
            {
                _current = x;
                SetSelectedMusicType();
                IsBusy = false;
            });

        _subscriptions.Add(subscription1);


        SongBookSelectionCommand = new Command<MusicTypeListItemViewModel>(async x =>
        {
            IsBusy = true;

            if (x.MusicType == MusicType.Vocals)
            {
                ReduxContainer.Store.Dispatch(new SongBookSelectionAction
                {
                    TentativeMusic = new AlarmMusic
                    {
                        MusicType = MusicType.Vocals,
                        LanguageCode = _current.LanguageCode
                    }
                });

                using var scope = _scopeFactory.CreateScope();
                var viewModel = scope.ServiceProvider.GetRequiredService<SongBookSelectionViewModel>();
                await _navigationService.Navigate(viewModel);
            }
            else
            {
                ReduxContainer.Store.Dispatch(new TrackSelectionAction
                {
                    TentativeMusic = new AlarmMusic
                    {
                        Repeat = _current.Repeat,
                        MusicType = MusicType.Melodies,
                        PublicationCode = "iam"
                    }
                });
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
        set => this.Set(ref _isBusy, value);
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
        set => this.Set(ref _selectedMusicType, value);
    }

    public void Dispose()
    {
        _navigationService.NavigatedBack -= OnNavigated;
        _subscriptions.ForEach(x => x.Dispose());

        _mediaService.Dispose();
    }
}

public class MusicTypeListItemViewModel : ViewModel, IComparable
{
    public MusicType MusicType { get; set; }
    public string Name { get; set; }

    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => this.Set(ref _isSelected, value);
    }

    public int CompareTo(object obj)
    {
        return Name.CompareTo((obj as MusicTypeListItemViewModel).Name);
    }
}