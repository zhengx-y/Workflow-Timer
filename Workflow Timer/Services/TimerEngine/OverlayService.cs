using System;
using System.Windows;
using Workflow_Timer.Views;
using Workflow_Timer.Enums;

namespace Workflow_Timer.Services.TimerEngine
{
    public class OverlayService
    {
        private OverlayWindow? _window;
        private OverlayPosition _position = OverlayPosition.FreeDrag;
        private double? _left;
        private double? _top;
        private bool _hidden;
        private bool _compact;

        public event EventHandler? PauseRequested;
        public event EventHandler? ResumeRequested;
        public event EventHandler<Point>? PositionDragged;
        public event EventHandler<bool>? CompactChanged;

        public void ConfigurePosition(OverlayPosition position, double? left, double? top,
            bool hidden = false, bool compact = false)
        {
            _position = position;
            _left = left;
            _top = top;
            _hidden = hidden;
            _compact = compact;
            _window?.SetCompact(_compact);
            _window?.ApplyPosition(_position, _left, _top);
            if (_hidden) Hide();
        }

        public void Show()
        {
            if (_hidden) return;
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
            if (_hidden) return;
            EnsureWindow();
            _window!.UpdateDisplay(state, remainingTime,
                nextActionName, nextActionRemaining);
        }

        public void Blink() { if (!_hidden) _window?.Blink(); }
        public void BringToFront() { if (!_hidden) _window?.BringToFront(); }

        private void EnsureWindow()
        {
            if (_window != null)
                return;

            _window = new OverlayWindow();
            _window.SetCompact(_compact);
            _window.ApplyPosition(_position, _left, _top);
            _window.PositionDragged += Window_PositionDragged;
            _window.CompactChanged += Window_CompactChanged;
            _window.PauseRequested += Window_PauseRequested;
            _window.ResumeRequested += Window_ResumeRequested;
            _window.Closed += (_, _) =>
            {
                if (_window != null)
                {
                    _window.PauseRequested -= Window_PauseRequested;
                    _window.ResumeRequested -= Window_ResumeRequested;
                    _window.PositionDragged -= Window_PositionDragged;
                    _window.CompactChanged -= Window_CompactChanged;
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

        private void Window_CompactChanged(object? sender, bool compact)
        {
            _compact = compact;
            CompactChanged?.Invoke(this, compact);
        }
    }
}
