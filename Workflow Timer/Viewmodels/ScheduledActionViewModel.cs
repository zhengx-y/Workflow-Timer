using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Workflow_Timer.Models;

namespace Workflow_Timer.Viewmodels
{
    public class ScheduledActionViewModel : INotifyPropertyChanged
    {
        public ScheduledAction Action { get; }

        public string Name => Action.Name;

        public string ActionType =>
            Action.ActionType.ToString();

        public string Target => Action.Target;

        public TimeSpan OffsetFromStart =>
            Action.OffsetFromStart;

        public bool Enabled =>
            Action.Enabled;

        public bool IsTriggered { get; private set; }

        public string StatusText
        {
            get
            {
                if (!Enabled)
                    return "Disabled";

                return IsTriggered
                    ? "Triggered"
                    : "Pending";
            }
        }

        public ScheduledActionViewModel(ScheduledAction action)
        {
            Action = action;
        }

        public void MarkTriggered()
        {
            if (IsTriggered)
                return;

            IsTriggered = true;

            OnPropertyChanged(nameof(IsTriggered));
            OnPropertyChanged(nameof(StatusText));
        }

        public void Reset()
        {
            if (!IsTriggered)
                return;

            IsTriggered = false;

            OnPropertyChanged(nameof(IsTriggered));
            OnPropertyChanged(nameof(StatusText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}