using Bible.Alarm.Models;
using Mvvmicro;
using System;

namespace Bible.Alarm.ViewModels
{
    public class LanguageListViewItemModel(Language language) : ViewModel, IComparable
    {
        public string Name { get; set; } = language.Name;
        public string Code { get; set; } = language.Code;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.Set(ref isSelected, value);
        }

        public int CompareTo(object obj)
        {
            return Name.CompareTo((obj as LanguageListViewItemModel).Name);
        }
    }
}
