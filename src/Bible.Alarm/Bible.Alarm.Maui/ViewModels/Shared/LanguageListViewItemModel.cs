using Bible.Alarm.Models;
using Mvvmicro;
using System;

namespace Bible.Alarm.ViewModels
{
    public class LanguageListViewItemModel : ViewModel, IComparable
    {
        public string Name { get; set; }
        public string Code { get; set; }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.Set(ref isSelected, value);
        }

        public LanguageListViewItemModel(Language language)
        {
            Name = language.Name;
            Code = language.Code;
        }

        public int CompareTo(object obj)
        {
            return Name.CompareTo((obj as LanguageListViewItemModel).Name);
        }
    }
}
