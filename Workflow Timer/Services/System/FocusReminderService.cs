using global::System;
using global::System.Windows.Threading;

namespace Workflow_Timer.Services.System
{
    // All calls run on the WPF dispatcher, like the workflow countdown.
    public sealed class FocusReminderService : IDisposable
    {
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private bool _enabled;
        private bool _pending;
        private bool _snoozed;
        private bool _disposed;
        private DateTimeOffset _nextReminderAt;
        private int _intervalMinutes = 5;
        private int _snoozeMinutes = 10;

        public bool IsVisible => _pending;
        public bool IsAttentionActive { get; private set; }
        public bool IsBlinkOn { get; private set; }
        public bool IsSnoozed => _snoozed;
        public TimeSpan Remaining => _pending
            ? TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((_nextReminderAt - DateTimeOffset.UtcNow).TotalSeconds)))
            : TimeSpan.Zero;
        public event EventHandler? StateChanged;
        public event EventHandler? ReminderDue;

        public FocusReminderService()
        {
            _timer.Tick += Timer_Tick;
        }

        public void Configure(bool enabled, int intervalMinutes, int snoozeMinutes)
        {
            if (_disposed)
                return;

            bool delayChanged = _snoozed
                ? _snoozeMinutes != snoozeMinutes
                : _intervalMinutes != intervalMinutes;
            _enabled = enabled;
            _intervalMinutes = intervalMinutes;
            _snoozeMinutes = snoozeMinutes;

            if (!_enabled)
                Cancel();
            else if (_pending && delayChanged)
                Schedule(_snoozed ? _snoozeMinutes : _intervalMinutes);
        }

        // Called only after a break finishes naturally.
        public void Begin()
        {
            Cancel();
            if (!_enabled || _disposed)
                return;
            _pending = true;
            Present();
        }

        public void Dismiss()
        {
            if (!_pending || !IsAttentionActive)
                return;
            _snoozed = false;
            IsAttentionActive = IsBlinkOn = false;
            Schedule(_intervalMinutes);
        }

        public void Snooze()
        {
            if (!_pending)
                return;
            _snoozed = true;
            IsAttentionActive = IsBlinkOn = false;
            Schedule(_snoozeMinutes);
        }

        public void Cancel()
        {
            _timer.Stop();
            _pending = false;
            _snoozed = false;
            IsAttentionActive = IsBlinkOn = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Schedule(int minutes)
        {
            _timer.Stop();
            if (!_pending || !_enabled || _disposed)
                return;
            _nextReminderAt = DateTimeOffset.UtcNow.AddMinutes(minutes);
            _timer.Start();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_pending || !_enabled || _disposed)
                return;
            if (DateTimeOffset.UtcNow >= _nextReminderAt)
            {
                Present();
                return;
            }
            if (IsAttentionActive)
                IsBlinkOn = !IsBlinkOn;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Present()
        {
            _snoozed = false;
            IsAttentionActive = IsBlinkOn = true;
            // Re-prompt even if the previous panel was left unanswered.
            Schedule(_intervalMinutes);
            ReminderDue?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Cancel();
            _timer.Tick -= Timer_Tick;
        }
    }
}
