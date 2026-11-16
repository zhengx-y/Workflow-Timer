using System;
using System.Windows.Input;
using Workflow_Timer.Helpers;

namespace Workflow_Timer.Commands
{
    public class SettingsCommands
    {
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        public SettingsCommands(Action save, Action cancel, Action resetDefaults)
        {
            SaveCommand = new RelayCommand(save);
            CancelCommand = new RelayCommand(cancel);
            ResetDefaultsCommand = new RelayCommand(resetDefaults);
        }
    }
}
