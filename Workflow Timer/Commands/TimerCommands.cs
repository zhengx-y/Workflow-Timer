using System;
using System.Windows.Input;
using Workflow_Timer.Helpers;

namespace Workflow_Timer.Commands
{
    public class TimerCommands
    {
        public ICommand StartCommand { get; }

        public ICommand PauseCommand { get; }

        public ICommand ResumeCommand { get; }

        public ICommand StopCommand { get; }
        public ICommand FinishCommand { get; }

        public ICommand RepeatCommand { get; }

        public ICommand ExtendCommand { get; }


        public TimerCommands(
            Action start,
            Action pause,
            Action resume,
            Action stop,
            Action repeat,
            Action<object?> extend,
            Func<bool>? canStart = null,
            Func<bool>? canRepeat = null,
            Action? finish = null,
            Func<bool>? canFinish = null)
        {
            StartCommand =
                new RelayCommand(start, canStart);

            PauseCommand =
                new RelayCommand(pause);

            ResumeCommand =
                new RelayCommand(resume);

            StopCommand =
                new RelayCommand(stop);

            FinishCommand = new RelayCommand(
                () => finish?.Invoke(),
                () => finish != null && (canFinish?.Invoke() ?? true));

            RepeatCommand =
                new RelayCommand(repeat, canRepeat);

            ExtendCommand =
                new RelayCommand(extend);
        }

        public void RefreshAvailability()
        {
            ((RelayCommand)StartCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RepeatCommand).RaiseCanExecuteChanged();
            ((RelayCommand)FinishCommand).RaiseCanExecuteChanged();
        }
    }
}
