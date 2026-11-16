using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using Workflow_Timer.Commands;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Services.Storage;
using Workflow_Timer.Helpers;
using AppThemeMode = Workflow_Timer.Enums.ThemeMode;

namespace Workflow_Timer.Views
{
    public partial class SettingsView : Window
    {
        private readonly Action<Settings> _saveSettings;
        private Settings _draft = new();
        public bool ResetDataRequested { get; private set; }

        public HotkeyModifier[] FirstModifiers { get; } =
            new[] { HotkeyModifier.Ctrl, HotkeyModifier.Alt, HotkeyModifier.Shift, HotkeyModifier.Win };

        public HotkeyModifier[] SecondModifiers { get; } =
            new[] { HotkeyModifier.None, HotkeyModifier.Ctrl, HotkeyModifier.Alt, HotkeyModifier.Shift, HotkeyModifier.Win };

        public SettingsView(Settings currentSettings, Action<Settings> saveSettings)
        {
            InitializeComponent();
            _saveSettings = saveSettings;
            ThemeBox.ItemsSource = Enum.GetValues<AppThemeMode>();
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
            InputLanguageManager.Current.InputLanguageChanged += InputLanguageChanged;
            Activated += (_, _) => RefreshKeyLabels(HotkeysList);
            Closed += (_, _) =>
                InputLanguageManager.Current.InputLanguageChanged -= InputLanguageChanged;
        }

        private void InputLanguageChanged(object sender, InputLanguageEventArgs e)
        {
            // Refresh after Windows has switched the active input layout.
            Dispatcher.BeginInvoke(new Action(() => RefreshKeyLabels(HotkeysList)));
        }

        private static void RefreshKeyLabels(DependencyObject parent)
        {
            if (parent is TextBox box && box.Name == "HotkeyKeyBox")
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                RefreshKeyLabels(VisualTreeHelper.GetChild(parent, i));
        }

        private void Populate(Settings settings)
        {
            _draft = settings.Copy();
            HotkeysList.ItemsSource = _draft.Hotkeys.OrderBy(binding => binding.Action).ToList();
            StartupCheck.IsChecked = settings.StartWithWindows;
            TrayCheck.IsChecked = settings.MinimizeToTray;
            ThemeBox.SelectedItem = settings.Theme;
            PositionBox.SelectedValue = settings.OverlayPosition;
            FocusCheck.IsChecked = settings.FocusGrabberEnabled;
            IntervalBox.Text = settings.FocusGrabberIntervalMinutes.ToString();
            SnoozeBox.Text = settings.SnoozeMinutes.ToString();
            WorkSoundCheck.IsChecked = settings.WorkCompletionSoundEnabled;
            BreakSoundCheck.IsChecked = settings.BreakCompletionSoundEnabled;
            ReminderSoundCheck.IsChecked = settings.FocusReminderSoundEnabled;
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
            candidate.MinimizeToTray = TrayCheck.IsChecked == true;
            candidate.Theme = ThemeBox.SelectedItem is AppThemeMode theme ? theme : AppThemeMode.Light;
            candidate.OverlayPosition = position;
            candidate.FocusGrabberEnabled = FocusCheck.IsChecked == true;
            candidate.FocusGrabberIntervalMinutes = interval;
            candidate.SnoozeMinutes = snooze;
            candidate.WorkCompletionSoundEnabled = WorkSoundCheck.IsChecked == true;
            candidate.BreakCompletionSoundEnabled = BreakSoundCheck.IsChecked == true;
            candidate.FocusReminderSoundEnabled = ReminderSoundCheck.IsChecked == true;

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

        private void CreateDesktopShortcut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Workflow_Timer.Services.System.ShortcutService.CreateDesktopShortcut();
                MessageBox.Show(this, "Desktop shortcut created or updated.", "Workflow Timer",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Could not create desktop shortcut: " + ex.Message;
            }
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try { DataManagementService.OpenFolder(); }
            catch (Exception ex) { ErrorText.Text = "Could not open data folder: " + ex.Message; }
        }

        private void DeleteData_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(this,
                "Permanently delete all saved presets, tasks, history, records and settings?\n\n" +
                "Unsaved edits will also be discarded. Workflow Timer will restart with factory defaults. " +
                "Back up your data first if needed.",
                "Delete all application data", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            ResetDataRequested = true;
            DialogResult = false;
        }

        private void HotkeyKey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox box || box.DataContext is not HotkeyBinding binding ||
                Keyboard.Modifiers != ModifierKeys.None)
                return;

            Key key = e.Key == Key.DeadCharProcessed ? e.DeadCharProcessedKey : e.Key;

            // Leave normal dialog navigation (Tab, Enter, Escape) untouched.
            if (key == Key.Tab || key == Key.Enter || key == Key.Escape)
                return;

            string name;
            if (key == Key.Back)
                name = string.Empty;
            else if (!HotkeyKeyHelper.TryGetName(key, out name))
            {
                e.Handled = true;
                return;
            }

            e.Handled = true;
            binding.Key = name;
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
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
