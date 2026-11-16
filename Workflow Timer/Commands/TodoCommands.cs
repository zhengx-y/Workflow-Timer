using global::System;
using global::System.Windows.Input;
using Workflow_Timer.Helpers;

namespace Workflow_Timer.Commands
{
    public sealed class TodoCommands
    {
        public ICommand AddTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand RetrySaveCommand { get; }

        public TodoCommands(Action add, Action<object?> delete, Action retry)
        {
            AddTaskCommand = new RelayCommand(add);
            DeleteTaskCommand = new RelayCommand(delete);
            RetrySaveCommand = new RelayCommand(retry);
        }
    }
}
