using Mvvmicro;
using System;

namespace Bible.Alarm.ViewModels
{
    public class NumberOfChaptersListViewItemModel(int number) : ViewModel, IComparable
    {
        public string Text => $"{Value} {(Value == 1 ? "chapter" : "chapters")}";
        public int Value { get; set; } = number;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => this.Set(ref _isSelected, value);
        }

        public int CompareTo(object obj)
        {
            return Value.CompareTo((obj as NumberOfChaptersListViewItemModel).Value);
        }
    }
}
