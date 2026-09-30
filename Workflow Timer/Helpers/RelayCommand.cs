using System;
using System.Windows.Input;

namespace Workflow_Timer.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?>? _executeWithParameter;
        private readonly Action? _execute;


        public RelayCommand(Action execute)
        {
            _execute = execute;
        }


        public RelayCommand(Action<object?> execute)
        {
            _executeWithParameter = execute;
        }


        public bool CanExecute(object? parameter)
        {
            return true;
        }


        public void Execute(object? parameter)
        {
            if (_execute != null)
                _execute();

            else
                _executeWithParameter?.Invoke(parameter);
        }


        public event EventHandler? CanExecuteChanged;
    }
}