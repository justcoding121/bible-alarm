using Redux;
using System;

namespace Bible.Alarm.ViewModels.Redux.Actions
{
    public class BackAction : IAction
    {
        public IDisposable CurrentViewModel { get; set; }
        public BackAction(IDisposable currentViewModel)
        {
            CurrentViewModel = currentViewModel;
        }
    }
}
