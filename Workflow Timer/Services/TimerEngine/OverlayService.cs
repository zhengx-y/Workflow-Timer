using System;
using System.Windows;
using Workflow_Timer.Views;
using Workflow_Timer.Enums;

namespace Workflow_Timer.Services.TimerEngine
{
    public class OverlayService
    {
        private OverlayWindow? _window;
        private OverlayPosition _position = OverlayPosition.TopLeft;
        private double? _left;
        private double? _top;

        public event EventHandler? PauseRequested;
        public event EventHandler? ResumeRequested;
        public event EventHandler<Point>? PositionDragged;

        public void ConfigurePosition(OverlayPosition position, double? left, double? top)
        {
            _position = position;
            _left = left;
            _top = top;
            _window?.ApplyPosition(_position, _left, _top);
        }

        public void Show()
        {
            EnsureWindow();
            _window!.ApplyPosition(_position, _left, _top);
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
            _window.ApplyPosition(_position, _left, _top);
            _window.PositionDragged += Window_PositionDragged;
            _window.PauseRequested += Window_PauseRequested;
            _window.ResumeRequested += Window_ResumeRequested;
            _window.Closed += (_, _) =>
            {
                if (_window != null)
                {
                    _window.PauseRequested -= Window_PauseRequested;
                    _window.ResumeRequested -= Window_ResumeRequested;
                    _window.PositionDragged -= Window_PositionDragged;
                    _window = null;
                }
            };
        }

        private void Window_PauseRequested(object? sender, EventArgs e) =>
            PauseRequested?.Invoke(this, EventArgs.Empty);

        private void Window_ResumeRequested(object? sender, EventArgs e) =>
            ResumeRequested?.Invoke(this, EventArgs.Empty);

        private void Window_PositionDragged(object? sender, Point point)
        {
            _left = point.X;
            _top = point.Y;
            PositionDragged?.Invoke(this, point);
        }
    }
}
