using global::System;
using global::System.Globalization;
using global::System.Windows;
using global::System.Windows.Controls;
using global::System.Windows.Controls.Primitives;
using global::System.Windows.Input;

namespace Workflow_Timer.Views.Controls
{
    public partial class TimeSpinnerControl : UserControl
    {
        // Presets use total hours; scheduler offsets use TimeSpan's invariant constant format.
        public static readonly DependencyProperty DurationTextProperty = DependencyProperty.Register(
            nameof(DurationText), typeof(string), typeof(TimeSpinnerControl),
            new FrameworkPropertyMetadata("00:00:00", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, DurationChanged));

        public static readonly DependencyProperty UseConstantFormatProperty = DependencyProperty.Register(
            nameof(UseConstantFormat), typeof(bool), typeof(TimeSpinnerControl),
            new PropertyMetadata(false, DurationChanged));

        public static readonly DependencyProperty ShowArrowsProperty = DependencyProperty.Register(
            nameof(ShowArrows), typeof(bool), typeof(TimeSpinnerControl), new PropertyMetadata(true));

        public bool ShowArrows
        {
            get => (bool)GetValue(ShowArrowsProperty);
            set => SetValue(ShowArrowsProperty, value);
        }

        public string DurationText
        {
            get => (string)GetValue(DurationTextProperty);
            set => SetValue(DurationTextProperty, value);
        }

        public bool UseConstantFormat
        {
            get => (bool)GetValue(UseConstantFormatProperty);
            set => SetValue(UseConstantFormatProperty, value);
        }

        private const long MaxSeconds = long.MaxValue / TimeSpan.TicksPerSecond;
        private const int MaxHours = (int)(MaxSeconds / 3600);
        private readonly int[] _parts = new int[3];
        private TextBox[] _fields = Array.Empty<TextBox>();
        private bool _updating;

        public TimeSpinnerControl()
        {
            InitializeComponent();
            _fields = new[] { HoursBox, MinutesBox, SecondsBox };
            foreach (var field in _fields)
                DataObject.AddPastingHandler(field, Field_Pasting);
            LoadDuration();
        }

        private static void DurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TimeSpinnerControl)d;
            if (!control._updating && control._fields.Length == 3)
                control.LoadDuration();
        }

        private void LoadDuration()
        {
            string text = DurationText ?? string.Empty;
            if (UseConstantFormat && TimeSpan.TryParseExact(text, "c", CultureInfo.InvariantCulture, out var span) && span >= TimeSpan.Zero)
            {
                _parts[0] = (int)span.TotalHours;
                _parts[1] = span.Minutes;
                _parts[2] = span.Seconds;
            }
            else
            {
                var parts = text.Split(':');
                if (parts.Length != 3 ||
                    !TryNumber(parts[0], 0, out int hours) ||
                    !TryNumber(parts[1], 1, out int minutes) ||
                    !TryNumber(parts[2], 2, out int seconds) ||
                    (long)hours * 3600 + minutes * 60L + seconds > MaxSeconds)
                    return;
                _parts[0] = hours;
                _parts[1] = minutes;
                _parts[2] = seconds;
            }
            _updating = true;
            try
            {
                for (int i = 0; i < 3; i++) _fields[i].Text = FormatPart(i);
            }
            finally { _updating = false; }
        }

        private string FormatPart(int i) => _parts[i].ToString("00", CultureInfo.InvariantCulture);
        private static int Index(TextBox field) => int.Parse((string)field.Tag, CultureInfo.InvariantCulture);
        private static int Limit(int i) => i == 0 ? MaxHours : 59;

        private static bool TryNumber(string text, int i, out int value)
            => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
               && value >= 0 && value <= Limit(i);

        private bool IsAllowed(string text, int index)
        {
            if (text.Length == 0) return true;
            if (!TryNumber(text, index, out int value)) return false;
            long total = (long)(index == 0 ? value : _parts[0]) * 3600
                       + (index == 1 ? value : _parts[1]) * 60L
                       + (index == 2 ? value : _parts[2]);
            return total <= MaxSeconds;
        }

        private static string ProposedText(TextBox field, string input)
            => field.Text.Remove(field.SelectionStart, field.SelectionLength).Insert(field.SelectionStart, input);

        private void Field_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var field = (TextBox)sender;
            e.Handled = !IsAllowed(ProposedText(field, e.Text), Index(field));
        }

        private void Field_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            var field = (TextBox)sender;
            if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText) ||
                e.DataObject.GetData(DataFormats.UnicodeText) is not string input ||
                !IsAllowed(ProposedText(field, input), Index(field)))
                e.CancelCommand();
        }

        private void Field_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updating || _fields.Length != 3) return;
            var field = (TextBox)sender;
            int i = Index(field);
            // An empty field is a temporary edit; restore its last valid value on leaving it.
            if (field.Text.Length == 0) return;
            if (!IsAllowed(field.Text, i) || !TryNumber(field.Text, i, out int value))
            {
                NormalizeField(i);
                return;
            }
            if (_parts[i] == value) return;
            _parts[i] = value;
            PublishDuration();
        }

        private void PublishDuration()
        {
            long seconds = (long)_parts[0] * 3600 + _parts[1] * 60L + _parts[2];
            var span = TimeSpan.FromTicks(seconds * TimeSpan.TicksPerSecond);
            string text = UseConstantFormat
                ? span.ToString("c", CultureInfo.InvariantCulture)
                : $"{FormatPart(0)}:{FormatPart(1)}:{FormatPart(2)}";
            _updating = true;
            try
            {
                // Preserve the binding rather than replacing it with a local value.
                SetCurrentValue(DurationTextProperty, text);
            }
            finally { _updating = false; }
        }

        private void NormalizeField(int i)
        {
            _updating = true;
            try { _fields[i].Text = FormatPart(i); }
            finally { _updating = false; }
        }

        private void Adjust(int i, int delta)
        {
            if (!IsEnabled) return;
            _fields[i].Focus();
            int value = i == 0
                ? Math.Clamp(_parts[i] + delta, 0, Limit(i))
                : (_parts[i] + delta + 60) % 60;
            if (IsAllowed(value.ToString(CultureInfo.InvariantCulture), i) && value != _parts[i])
            {
                _parts[i] = value;
                PublishDuration();
            }
            NormalizeField(i);
            _fields[i].SelectAll();
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            var tag = ((RepeatButton)sender).Tag.ToString()!.Split(':');
            Adjust(int.Parse(tag[0], CultureInfo.InvariantCulture), int.Parse(tag[1], CultureInfo.InvariantCulture));
        }

        private void Field_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!IsEnabled || e.Delta == 0) return;
            Adjust(Index((TextBox)sender), Math.Sign(e.Delta));
            e.Handled = true;
        }

        private void Field_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Up && e.Key != Key.Down) return;
            Adjust(Index((TextBox)sender), e.Key == Key.Up ? 1 : -1);
            e.Handled = true;
        }

        private void Field_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
            => ((TextBox)sender).SelectAll();

        private void Field_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var field = (TextBox)sender;
            if (field.IsKeyboardFocused) return;
            e.Handled = true;
            field.Focus();
            field.SelectAll();
        }

        private void Field_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_fields.Length == 3) NormalizeField(Index((TextBox)sender));
        }
    }
}
