using System;
using Workflow_Timer.Views;

namespace Workflow_Timer.Services.TimerEngine
{
    public class OverlayService
    {
        private OverlayWindow? _window;

        public void Show()
        {
            EnsureWindow();

            if (!_window!.IsVisible)
                _window.Show();

            _window.BringToFront();
        }

        public void Hide()
        {
            if (_window == null)
                return;

            _window.Hide();
        }

        public void Update(
            string state,
            TimeSpan remainingTime)
        {
            EnsureWindow();

            _window!.UpdateDisplay(
                state,
                remainingTime);
        }

        public void Blink()
        {
            if (_window == null)
                return;

            _window.Blink();
        }

        public void BringToFront()
        {
            if (_window == null)
                return;

            _window.BringToFront();
        }

        private void EnsureWindow()
        {
            if (_window != null)
                return;

            _window = new OverlayWindow();

            _window.Closed += (_, _) =>
            {
                _window = null;
            };
        }
    }
}