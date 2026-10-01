using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Workflow_Timer.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _blinkTimer;
        private bool _isBlinking;
        private int _blinkCount;

        public OverlayWindow()
        {
            InitializeComponent();

            _blinkTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };

            _blinkTimer.Tick += BlinkTimer_Tick;
        }

        public void UpdateDisplay(string state, TimeSpan remainingTime)
        {
            StateText.Text = state.ToUpperInvariant();
            TimeText.Text = remainingTime.ToString(@"hh\:mm\:ss");
        }

        public void Blink()
        {
            if (_isBlinking)
                return;

            _isBlinking = true;
            _blinkCount = 0;
            _blinkTimer.Start();
        }

        public void BringToFront()
        {
            if (!IsVisible)
                return;

            Topmost = true;
            Activate();
        }

        private void BlinkTimer_Tick(
            object? sender,
            EventArgs e)
        {
            _blinkCount++;

            Opacity = Opacity < 1.0 ? 1.0 : 0.25;

            if (_blinkCount >= 6)
            {
                _blinkTimer.Stop();
                Opacity = 1.0;
                _isBlinking = false;

                Hide();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _blinkTimer.Stop();
            base.OnClosed(e);
        }
    }
}