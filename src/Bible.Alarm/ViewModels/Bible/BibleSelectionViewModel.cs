using Bible.Alarm.Common.Helpers;
using Bible.Alarm.Contracts.UI;
using Bible.Alarm.Models;
using Bible.Alarm.Services;
using Bible.Alarm.Services.Contracts;
using Bible.Alarm.ViewModels.Redux;
using Bible.Alarm.ViewModels.Redux.Actions.Bible;
using Mvvmicro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bible.Alarm.ViewModels
{
    public class BibleSelectionViewModel : ViewModel, IListViewModel, IDisposable
    {
        private readonly IContainer container;

        private MediaService mediaService;
        private INavigationService navigationService;

        private BibleReadingSchedule current;
        private BibleReadingSchedule tentative;

        private List<IDisposable> subscriptions = new List<IDisposable>();

        public ICommand BackCommand { get; set; }
        public ICommand BookSelectionCommand { get; set; }
        public ICommand OpenModalCommand { get; set; }
        public ICommand CloseModalCommand { get; set; }
        public ICommand SelectLanguageCommand { get; set; }
        public ICommand SelectSongBookCommand { get; set; }

        public BibleSelectionViewModel(IContainer container)
        {
            this.container = container;

            this.mediaService = this.container.Resolve<MediaService>();
            this.navigationService = this.container.Resolve<INavigationService>();

            //set schedules from initial state.
            //this should fire only once 
            var subscription1 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
                 .Select(state => new { state.CurrentBibleReadingSchedule, state.TentativeBibleReadingSchedule })
                 .Where(x => x.CurrentBibleReadingSchedule != null && x.TentativeBibleReadingSchedule != null)
                 .DistinctUntilChanged()
                 .Take(1)
                 .Subscribe(async x =>
                 {
                     current = x.CurrentBibleReadingSchedule;
                     tentative = x.TentativeBibleReadingSchedule;

                     await initialize(tentative.LanguageCode);

                     IsBusy = false;
                 });

            subscriptions.Add(subscription1);

            var subscription2 = ReduxContainer.Store.ObserveOn(Scheduler.CurrentThread)
             .Select(state => new { state.CurrentBibleReadingSchedule, state.TentativeBibleReadingSchedule })
             .Where(x => x.CurrentBibleReadingSchedule != null && x.TentativeBibleReadingSchedule != null)
             .DistinctUntilChanged()
             .Skip(1)
             .Subscribe(x =>
             {
                 current = x.CurrentBibleReadingSchedule;
                 tentative = x.TentativeBibleReadingSchedule;
             });

            subscriptions.Add(subscription2);

            BookSelectionCommand = new Command<PublicationListViewItemModel>(async x =>
            {
                IsBusy = true;
                ReduxContainer.Store.Dispatch(new BookSelectionAction()
                {
                    TentativeBibleReadingSchedule = new BibleReadingSchedule()
                    {
                        PublicationCode = x.Code,
                        LanguageCode = CurrentLanguage.Code
                    }
                });
                var viewModel = this.container.Resolve<BookSelectionViewModel>();
                await navigationService.Navigate(viewModel);

                IsBusy = false;
            });

            OpenModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.ShowModal("LanguageModal", this);
                IsBusy = false;
            });

            BackCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.GoBack();
                IsBusy = false;
            });

            CloseModalCommand = new Command(async () =>
            {
                IsBusy = true;
                await navigationService.CloseModal();
                IsBusy = false;
            });

            SelectLanguageCommand = new Command<LanguageListViewItemModel>(async x =>
            {
                IsBusy = true;
                if (CurrentLanguage != null)
                {
                    CurrentLanguage.IsSelected = false;
                }

                CurrentLanguage = x;
                CurrentLanguage.IsSelected = true;

                await navigationService.CloseModal();
                await populateTranslations(x.Code);

                IsBusy = false;
            });

            navigationService.NavigatedBack += onNavigated;
        }

        private void onNavigated(object viewModal)
        {
            if (viewModal.GetType() == this.GetType())
            {
                setSelectedTranslation();
            }
        }

        private void setSelectedTranslation()
        {
            if (current.LanguageCode == tentative.LanguageCode)
            {
                if (SelectedTranslation != null)
                {
                    SelectedTranslation.IsSelected = false;

                }

                SelectedTranslation = translationVMsMapping[current.PublicationCode];
                SelectedTranslation.IsSelected = true;
            }
        }

        private ObservableCollection<PublicationListViewItemModel> translations;
        public ObservableCollection<PublicationListViewItemModel> Translations
        {
            get => translations;
            set => this.Set(ref translations, value);
        }

        private ObservableCollection<LanguageListViewItemModel> languages;
        public ObservableCollection<LanguageListViewItemModel> Languages
        {
            get => languages;
            set => this.Set(ref languages, value);
        }

        public PublicationListViewItemModel SelectedTranslation { get; set; }

        private LanguageListViewItemModel currentLanguage;
        public LanguageListViewItemModel CurrentLanguage
        {
            get => currentLanguage;
            set => this.Set(ref currentLanguage, value);
        }

        private bool isBusy;
        public bool IsBusy
        {
            get => isBusy;
            set => this.Set(ref isBusy, value);
        }

        public string PublicationCode
        {
            get => tentative.PublicationCode;
            set => this.Set(tentative.PublicationCode, value);
        }

        private string languageSearchTerm;
        public string LanguageSearchTerm
        {
            get => languageSearchTerm;
            set => this.Set(ref languageSearchTerm, value);
        }

        public object SelectedItem => CurrentLanguage;

        private async Task initialize(string languageCode)
        {
            await populateLanguages();
            await populateTranslations(languageCode);

            var subscription = Observable.FromEvent<PropertyChangedEventHandler, KeyValuePair<string, object>>(
                                              onNextHandler => (object sender, PropertyChangedEventArgs e)
                                              => onNextHandler(new KeyValuePair<string, object>(e.PropertyName, sender)),
                                              handler => PropertyChanged += handler,
                                              handler => PropertyChanged -= handler)
                          .Where(x => x.Key == "LanguageSearchTerm")
                          .Do(async x => await populateLanguages(LanguageSearchTerm))
                          .Subscribe();

            subscriptions.Add(subscription);
        }


        private async Task populateLanguages(string searchTerm = null)
        {
            var languages = await mediaService.GetBibleLanguages();
            var languageVMs = new ObservableCollection<LanguageListViewItemModel>();

            foreach (var language in languages.Select(x => x.Value)
                .Where(x => searchTerm == null
                    || x.Name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(x => x.Name))
            {
                var languageVM = new LanguageListViewItemModel(language);

                languageVMs.Add(languageVM);

                if (languageVM.Code == tentative.LanguageCode)
                {
                    languageVM.IsSelected = true;
                    CurrentLanguage = languageVM;
                }
            }

            Languages = languageVMs;

        }

        private Dictionary<string, PublicationListViewItemModel> translationVMsMapping
            = new Dictionary<string, PublicationListViewItemModel>();

        private async Task populateTranslations(string languageCode)
        {
            translationVMsMapping.Clear();

            var translations = await mediaService.GetBibleTranslations(languageCode);
            var translationVMs = new ObservableCollection<PublicationListViewItemModel>();

            foreach (var translation in translations.Select(x => x.Value))
            {
                var translationVM = new PublicationListViewItemModel(translation);

                translationVMs.Add(translationVM);
                translationVMsMapping.Add(translationVM.Code, translationVM);

                if (current.LanguageCode == languageCode
                    && current.PublicationCode == translation.Code)
                {
                    translationVM.IsSelected = true;
                    SelectedTranslation = translationVM;
                }
            }

            Translations = translationVMs;
        }

        public void Dispose()
        {
            navigationService.NavigatedBack -= onNavigated;

            subscriptions.ForEach(x => x.Dispose());
            mediaService.Dispose();
        }
    }
}
