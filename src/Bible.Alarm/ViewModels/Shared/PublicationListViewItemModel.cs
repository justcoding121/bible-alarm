using Bible.Alarm.Models;
using Mvvmicro;
using System;

namespace Bible.Alarm.ViewModels
{
    public class PublicationListViewItemModel : ViewModel, IComparable
    {
        private readonly Publication publication;

        public PublicationListViewItemModel(Publication publication)
        {
            this.publication = publication;
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.Set(ref isSelected, value);
        }

        public string Name => publication.Name;
        public string Code => publication.Code;

        public int CompareTo(object obj)
        {
            return Name.CompareTo((obj as PublicationListViewItemModel).Name);
        }
    }
}
