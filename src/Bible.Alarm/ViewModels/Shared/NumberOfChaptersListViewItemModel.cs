using Mvvmicro;
using System;

namespace Bible.Alarm.ViewModels
{
    public class NumberOfChaptersListViewItemModel(int number) : ViewModel, IComparable
    {
        public string Text => $"{Value} {(Value == 1 ? "chapter" : "chapters")}";
        public int Value { get; set; } = number;

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set => this.Set(ref isSelected, value);
        }

        public int CompareTo(object obj)
        {
            return Value.CompareTo((obj as NumberOfChaptersListViewItemModel).Value);
        }
    }
}
