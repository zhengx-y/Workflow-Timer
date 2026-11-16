using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using Workflow_Timer.Commands;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Services.Storage;

namespace Workflow_Timer.Views
{
    public partial class SettingsView : Window
    {
        private readonly Action<Settings> _saveSettings;
        private Settings _draft = new();

        public HotkeyModifier[] FirstModifiers { get; } =
            new[] { HotkeyModifier.Ctrl, HotkeyModifier.Alt, HotkeyModifier.Shift, HotkeyModifier.Win };

        public HotkeyModifier[] SecondModifiers { get; } =
            new[] { HotkeyModifier.None, HotkeyModifier.Ctrl, HotkeyModifier.Alt, HotkeyModifier.Shift, HotkeyModifier.Win };

        public SettingsView(Settings currentSettings, Action<Settings> saveSettings)
        {
            InitializeComponent();
            _saveSettings = saveSettings;
            PositionBox.ItemsSource = new Dictionary<OverlayPosition, string>
            {
                [OverlayPosition.TopLeft] = "Top left",
                [OverlayPosition.TopRight] = "Top right",
                [OverlayPosition.BottomLeft] = "Bottom left",
                [OverlayPosition.BottomRight] = "Bottom right",
                [OverlayPosition.FreeDrag] = "Free drag"
            };
            DataContext = new SettingsCommands(Save, () => DialogResult = false,
                () => Populate(new Settings()));
            Populate(currentSettings);
        }

        private void Populate(Settings settings)
        {
            _draft = settings.Copy();
            HotkeysList.ItemsSource = _draft.Hotkeys.OrderBy(binding => binding.Action).ToList();
            StartupCheck.IsChecked = settings.StartWithWindows;
            PositionBox.SelectedValue = settings.OverlayPosition;
            FocusCheck.IsChecked = settings.FocusGrabberEnabled;
            IntervalBox.Text = settings.FocusGrabberIntervalMinutes.ToString();
            SnoozeBox.Text = settings.SnoozeMinutes.ToString();
            ErrorText.Text = string.Empty;
        }

        private void Save()
        {
            if (PositionBox.SelectedValue is not OverlayPosition position ||
                !int.TryParse(IntervalBox.Text, out int interval) ||
                !int.TryParse(SnoozeBox.Text, out int snooze))
            {
                ErrorText.Text = "Enter whole numbers for both durations.";
                return;
            }

            // Preserve saved drag coordinates when changing unrelated preferences.
            var candidate = _draft.Copy();
            candidate.StartWithWindows = StartupCheck.IsChecked == true;
            candidate.OverlayPosition = position;
            candidate.FocusGrabberEnabled = FocusCheck.IsChecked == true;
            candidate.FocusGrabberIntervalMinutes = interval;
            candidate.SnoozeMinutes = snooze;

            try
            {
                SettingsService.Validate(candidate);
                _saveSettings(candidate);
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Could not save settings: " + ex.Message;
                return;
            }

            DialogResult = true;
        }

        private static bool IsLetter(string text) =>
            text.Length == 1 && ((text[0] >= 'A' && text[0] <= 'Z') ||
                (text[0] >= 'a' && text[0] <= 'z'));

        private void HotkeyKey_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsLetter(e.Text);
        }

        private void HotkeyKey_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !IsLetter(text))
                e.CancelCommand();
        }

        private void ClearHotkeys_Click(object sender, RoutedEventArgs e)
        {
            foreach (var binding in _draft.Hotkeys)
            {
                binding.Key = string.Empty;
                binding.Modifier1 = HotkeyModifier.Ctrl;
                binding.Modifier2 = HotkeyModifier.None;
            }
            HotkeysList.ItemsSource = null;
            HotkeysList.ItemsSource = _draft.Hotkeys.OrderBy(binding => binding.Action).ToList();
        }
    }
}
