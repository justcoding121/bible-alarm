using Bible.Alarm.Models;
using Mvvmicro;
using System;

namespace Bible.Alarm.ViewModels
{
    public class LanguageListViewItemModel(Language language) : ViewModel, IComparable
    {
        public string Name { get; set; } = language.Name;
        public string Code { get; set; } = language.Code;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => this.Set(ref _isSelected, value);
        }

        public int CompareTo(object obj)
        {
            return Name.CompareTo((obj as LanguageListViewItemModel).Name);
        }
    }
}
