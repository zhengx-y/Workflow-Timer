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

        public ICommand RepeatCommand { get; }

        public ICommand ExtendCommand { get; }


        public TimerCommands(
            Action start,
            Action pause,
            Action resume,
            Action stop,
            Action repeat,
            Action<object?> extend)
        {
            StartCommand =
                new RelayCommand(start);

            PauseCommand =
                new RelayCommand(pause);

            ResumeCommand =
                new RelayCommand(resume);

            StopCommand =
                new RelayCommand(stop);

            RepeatCommand =
                new RelayCommand(repeat);

            ExtendCommand =
                new RelayCommand(extend);
        }
    }
}