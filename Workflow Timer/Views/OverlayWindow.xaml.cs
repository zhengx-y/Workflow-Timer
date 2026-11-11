using System;
using System.Windows;
using System.Windows.Threading;

namespace Workflow_Timer.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _blinkTimer;
        private bool _isBlinking;
        private int _blinkCount;
        private string _currentState = string.Empty;

        public event EventHandler? PauseRequested;
        public event EventHandler? ResumeRequested;

        public OverlayWindow()
        {
            InitializeComponent();
            // WPF's work area is expressed in device-independent units and excludes
            // the taskbar; it adapts to primary-monitor resolution and DPI scaling.
            WindowStartupLocation = WindowStartupLocation.Manual;
            PositionTopLeft();
            _blinkTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _blinkTimer.Tick += BlinkTimer_Tick;
        }

        private void PositionTopLeft()
        {
            Rect area = SystemParameters.WorkArea;
            Left = area.Left + 12;
            Top = area.Top + 12;
        }

        public void UpdateDisplay(string state, TimeSpan remainingTime,
            string? nextActionName, TimeSpan? nextActionRemaining)
        {
            _currentState = state;
            StateText.Text = state.ToUpperInvariant();
            TimeText.Text = FormatTime(remainingTime);
            bool paused = string.Equals(state, "Paused", StringComparison.OrdinalIgnoreCase);
            bool active = paused || string.Equals(state, "Working", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "Break", StringComparison.OrdinalIgnoreCase);
            PauseResumeButton.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            PauseResumeButton.Content = paused ? "Resume" : "Pause";
            NextActionNameText.Text = string.IsNullOrWhiteSpace(nextActionName)
                ? "No upcoming actions" : nextActionName;
            NextActionNameText.ToolTip = NextActionNameText.Text;
            NextActionTimeText.Text = nextActionRemaining.HasValue
                ? FormatTime(nextActionRemaining.Value) : "--:--:--";
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        private void PauseResumeButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.Equals(_currentState, "Paused", StringComparison.OrdinalIgnoreCase))
                ResumeRequested?.Invoke(this, EventArgs.Empty);
            else if (string.Equals(_currentState, "Working", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_currentState, "Break", StringComparison.OrdinalIgnoreCase))
                PauseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Blink()
        {
            if (_isBlinking) return;
            _isBlinking = true;
            _blinkCount = 0;
            _blinkTimer.Start();
        }

        public void BringToFront()
        {
            if (!IsVisible) return;
            Topmost = true;
            Activate();
        }

        private void BlinkTimer_Tick(object? sender, EventArgs e)
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
