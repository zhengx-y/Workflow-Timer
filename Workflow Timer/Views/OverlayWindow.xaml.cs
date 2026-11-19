using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Threading;
using Workflow_Timer.Enums;

namespace Workflow_Timer.Views
{
    public partial class OverlayWindow : Window
    {
        private readonly DispatcherTimer _blinkTimer;
        private bool _isBlinking;
        private int _blinkCount;
        private string _currentState = string.Empty;
        private OverlayPosition _position = OverlayPosition.FreeDrag;
        private double? _savedLeft, _savedTop;
        private bool _compact;
        private bool _active;
        private const double EdgeMargin = 12;

        public event EventHandler? PauseRequested;
        public event EventHandler? ResumeRequested;
        public event EventHandler<Point>? PositionDragged;
        public event EventHandler<bool>? CompactChanged;

        public OverlayWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.Manual;
            ApplyPosition(OverlayPosition.FreeDrag, null, null);
            _blinkTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _blinkTimer.Tick += BlinkTimer_Tick;
        }

        public void ApplyPosition(OverlayPosition position, double? savedLeft, double? savedTop)
        {
            _position = position;
            _savedLeft = savedLeft;
            _savedTop = savedTop;
            StateText.Cursor = TimeText.Cursor = position == OverlayPosition.FreeDrag ? Cursors.SizeAll : Cursors.Arrow;
            StateText.ToolTip = TimeText.ToolTip = position == OverlayPosition.FreeDrag ? "Drag to move widget" : null;

            // Primary monitor work area, in WPF device-independent coordinates.
            Rect area = SystemParameters.WorkArea;
            double left = area.Left + EdgeMargin;
            double top = area.Top + EdgeMargin;
            if (position is OverlayPosition.TopRight or OverlayPosition.CenterRight or OverlayPosition.BottomRight)
                left = area.Right - Width - EdgeMargin;
            if (position is OverlayPosition.TopCenter or OverlayPosition.Center or OverlayPosition.BottomCenter)
                left = area.Left + (area.Width - Width) / 2;
            if (position is OverlayPosition.BottomLeft or OverlayPosition.BottomCenter or OverlayPosition.BottomRight)
                top = area.Bottom - Height - EdgeMargin;
            if (position is OverlayPosition.CenterLeft or OverlayPosition.Center or OverlayPosition.CenterRight)
                top = area.Top + (area.Height - Height) / 2;
            if ((position is OverlayPosition.FreeDrag or OverlayPosition.LastFreeDrag) &&
                savedLeft is double x && savedTop is double y &&
                double.IsFinite(x) && double.IsFinite(y))
            {
                left = x;
                top = y;
            }

            PlaceWithinWorkArea(left, top);
        }

        private void PlaceWithinWorkArea(double left, double top)
        {
            Rect area = SystemParameters.WorkArea;
            double maxLeft = Math.Max(area.Left, area.Right - Width);
            double maxTop = Math.Max(area.Top, area.Bottom - Height);
            Left = Math.Clamp(left, area.Left, maxLeft);
            Top = Math.Clamp(top, area.Top, maxTop);
        }

        private void StateText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_position != OverlayPosition.FreeDrag || e.ButtonState != MouseButtonState.Pressed)
                return;

            e.Handled = true;
            double oldLeft = Left;
            double oldTop = Top;
            DragMove();
            PlaceWithinWorkArea(Left, Top);
            if (Left != oldLeft || Top != oldTop)
            {
                _savedLeft = Left;
                _savedTop = Top;
                PositionDragged?.Invoke(this, new Point(Left, Top));
            }
        }

        public void SetCompact(bool compact)
        {
            _compact = compact;
            Width = compact ? 220 : 300;
            Height = compact ? 70 : 222;
            TimeText.FontSize = compact ? 24 : 32;
            StateText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            ActionPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            PauseResumeButton.Visibility = !compact && _active ? Visibility.Visible : Visibility.Collapsed;
            CompactButton.Content = compact ? "+" : "−";
            CompactButton.ToolTip = compact ? "Expand widget" : "Minimize widget";
            AutomationProperties.SetName(CompactButton, compact ? "Expand widget" : "Minimize widget");
            ApplyPosition(_position, _savedLeft, _savedTop);
        }

        private void CompactButton_Click(object sender, RoutedEventArgs e)
        {
            SetCompact(!_compact);
            CompactChanged?.Invoke(this, _compact);
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
            _active = active;
            // A new session can start immediately from the break reminder.
            // Do not let the previous session's blink hide the new countdown.
            if (active && _isBlinking)
            {
                _blinkTimer.Stop();
                _isBlinking = false;
                _blinkCount = 0;
                Opacity = 1.0;
            }
            PauseResumeButton.Visibility = active && !_compact ? Visibility.Visible : Visibility.Collapsed;
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
