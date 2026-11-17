using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Workflow_Timer.Models;

namespace Workflow_Timer.Viewmodels
{
    public class ScheduledActionViewModel : INotifyPropertyChanged
    {
        private readonly Action<ScheduledActionViewModel> _save;
        private string _name, _target, _offsetText;
        private bool _enabled;
        private string? _error;
        public ScheduledAction Action { get; }
        public ScheduledActionViewModel(ScheduledAction action, Action<ScheduledActionViewModel> save)
        {
            Action = action;
            _save = save;
            _name = action.Name ?? string.Empty;
            _target = action.Target ?? string.Empty;
            _offsetText = action.OffsetFromStart.ToString("c", CultureInfo.InvariantCulture);
            _enabled = action.Enabled;
        }
        public string Name { get => _name; set { if (_name != value) { _name = value; Changed(); } } }
        public string Target { get => _target; set { if (_target != value) { _target = value; Changed(); } } }
        public string OffsetText { get => _offsetText; set { if (_offsetText != value) { _offsetText = value; Changed(); } } }
        public bool Enabled { get => _enabled; set { if (_enabled != value) { _enabled = value; Changed(); } } }
        public bool IsTriggered { get; private set; }
        public bool HasPendingChanges => _error != null;
        public string? Error => _error;
        public string StatusText => _error ?? (!Enabled ? "Disabled" : IsTriggered ? "Triggered" : "Pending");
        public string ActionType => Action.ActionType.ToString();
        public TimeSpan OffsetFromStart => Action.OffsetFromStart;

        public void SetPending(string? error)
        {
            _error = error;
            OnPropertyChanged(nameof(Error));
            OnPropertyChanged(nameof(HasPendingChanges));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(ActionType));
            OnPropertyChanged(nameof(OffsetFromStart));
        }

        private void Changed([CallerMemberName] string? property = null)
        {
            OnPropertyChanged(property);
            _save(this);
        }

        public void MarkTriggered()
        {
            IsTriggered = true;
            OnPropertyChanged(nameof(IsTriggered));
            OnPropertyChanged(nameof(StatusText));
        }

        public void Reset()
        {
            IsTriggered = false;
            OnPropertyChanged(nameof(IsTriggered));
            OnPropertyChanged(nameof(StatusText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? property = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
