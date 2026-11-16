using System;
using System.Windows.Input;

namespace Workflow_Timer.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?>? _executeWithParameter;
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;


        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }


        public RelayCommand(Action<object?> execute)
        {
            _executeWithParameter = execute;
        }


        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }


        public void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
                return;
            if (_execute != null)
                _execute();

            else
                _executeWithParameter?.Invoke(parameter);
        }


        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
