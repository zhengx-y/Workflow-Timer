using System;
using System.Windows;
using Workflow_Timer.Views;

namespace Workflow_Timer.Services.TimerEngine
{
    public class OverlayService
    {
        private OverlayWindow? _window;

        public event EventHandler? PauseRequested;
        public event EventHandler? ResumeRequested;

        public void Show()
        {
            EnsureWindow();
            if (!_window!.IsVisible)
                _window.Show();
            _window.BringToFront();
        }

        public void Hide() => _window?.Hide();

        public void Update(string state, TimeSpan remainingTime,
            string? nextActionName = null, TimeSpan? nextActionRemaining = null)
        {
            EnsureWindow();
            _window!.UpdateDisplay(state, remainingTime,
                nextActionName, nextActionRemaining);
        }

        public void Blink() => _window?.Blink();
        public void BringToFront() => _window?.BringToFront();

        private void EnsureWindow()
        {
            if (_window != null)
                return;

            _window = new OverlayWindow();
            _window.PauseRequested += Window_PauseRequested;
            _window.ResumeRequested += Window_ResumeRequested;
            _window.Closed += (_, _) =>
            {
                if (_window != null)
                {
                    _window.PauseRequested -= Window_PauseRequested;
                    _window.ResumeRequested -= Window_ResumeRequested;
                    _window = null;
                }
            };
        }

        private void Window_PauseRequested(object? sender, EventArgs e) =>
            PauseRequested?.Invoke(this, EventArgs.Empty);

        private void Window_ResumeRequested(object? sender, EventArgs e) =>
            ResumeRequested?.Invoke(this, EventArgs.Empty);
    }
}
