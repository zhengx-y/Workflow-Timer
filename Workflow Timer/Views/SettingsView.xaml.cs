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
        private readonly Settings _savedSettings;
        private AppThemeMode _previewedTheme;
        private bool _committed;
        private bool _populating;
        private bool _applyingPreview;
        private bool _dirtyRefreshQueued;
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
            _savedSettings = currentSettings.Copy();
            if (_savedSettings.OverlayPosition != OverlayPosition.FreeDrag)
                _savedSettings.OverlayFixedPosition = _savedSettings.OverlayPosition;
            _previewedTheme = currentSettings.Theme;
            ThemeBox.ItemsSource = Enum.GetValues<AppThemeMode>();
            PositionBox.ItemsSource = new Dictionary<OverlayPosition, string>
            {
                [OverlayPosition.TopLeft] = "Top left",
                [OverlayPosition.TopCenter] = "Top center",
                [OverlayPosition.TopRight] = "Top right",
                [OverlayPosition.CenterLeft] = "Center left",
                [OverlayPosition.Center] = "Center",
                [OverlayPosition.CenterRight] = "Center right",
                [OverlayPosition.BottomLeft] = "Bottom left",
                [OverlayPosition.BottomCenter] = "Bottom center",
                [OverlayPosition.BottomRight] = "Bottom right",
                [OverlayPosition.LastFreeDrag] = "Last free drag position"
            };
            DataContext = new SettingsCommands(Save, () => DialogResult = false,
                () => Populate(new Settings
                {
                    FirstLaunchSetupCompleted = _savedSettings.FirstLaunchSetupCompleted,
                    NeverShowTutorial = _draft.NeverShowTutorial
                }));
            Populate(currentSettings);
            FreeDragCheck.Checked += (_, _) => PositionBox.IsEnabled = false;
            FreeDragCheck.Unchecked += (_, _) => PositionBox.IsEnabled = true;
            ThemeBox.SelectionChanged += (_, _) =>
            {
                if (!_populating) PreviewTheme();
            };
            AddHandler(global::System.Windows.Controls.Primitives.Selector.SelectionChangedEvent,
                new SelectionChangedEventHandler((_, _) => QueueDirtyRefresh()));
            AddHandler(global::System.Windows.Controls.Primitives.ToggleButton.CheckedEvent,
                new RoutedEventHandler((_, _) => QueueDirtyRefresh()));
            AddHandler(global::System.Windows.Controls.Primitives.ToggleButton.UncheckedEvent,
                new RoutedEventHandler((_, _) => QueueDirtyRefresh()));
            AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => QueueDirtyRefresh()));
            InputLanguageManager.Current.InputLanguageChanged += InputLanguageChanged;
            Activated += (_, _) => RefreshKeyLabels(HotkeysList);
            Closed += (_, _) =>
            {
                InputLanguageManager.Current.InputLanguageChanged -= InputLanguageChanged;
                if (!_committed && _previewedTheme != _savedSettings.Theme)
                    Workflow_Timer.Services.System.ThemeService.Apply(_savedSettings.Theme);
            };
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
            _populating = true;
            try
            {
                _draft = settings.Copy();
                HotkeysList.ItemsSource = _draft.Hotkeys.OrderBy(binding => binding.Action).ToList();
                StartupCheck.IsChecked = settings.StartWithWindows;
                TrayCheck.IsChecked = settings.MinimizeToTray;
                ThemeBox.SelectedItem = settings.Theme;
                VisualGuidanceCheck.IsChecked = settings.VisualGuidanceEnabled;
                HideWidgetCheck.IsChecked = settings.HideWidget;
                FreeDragCheck.IsChecked = settings.OverlayPosition == OverlayPosition.FreeDrag;
                PositionBox.SelectedValue = settings.OverlayPosition == OverlayPosition.FreeDrag
                    ? settings.OverlayFixedPosition : settings.OverlayPosition;
                PositionBox.IsEnabled = FreeDragCheck.IsChecked != true;
                FocusCheck.IsChecked = settings.FocusGrabberEnabled;
                IntervalBox.Text = settings.FocusGrabberIntervalMinutes.ToString();
                SnoozeBox.Text = settings.SnoozeMinutes.ToString();
                WorkSoundCheck.IsChecked = settings.WorkCompletionSoundEnabled;
                BreakSoundCheck.IsChecked = settings.BreakCompletionSoundEnabled;
                ReminderSoundCheck.IsChecked = settings.FocusReminderSoundEnabled;
                ErrorText.Text = string.Empty;
            }
            finally { _populating = false; }
            PreviewTheme();
            RefreshDirtyIndicator();
        }

        private void PreviewTheme()
        {
            if (_applyingPreview || ThemeBox.SelectedItem is not AppThemeMode theme || theme == _previewedTheme)
                return;
            _applyingPreview = true;
            try
            {
                Workflow_Timer.Services.System.ThemeService.Apply(theme);
                _previewedTheme = theme;
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Could not preview theme: " + ex.Message;
            }
            finally { _applyingPreview = false; }
        }

        private void QueueDirtyRefresh()
        {
            if (_populating || _dirtyRefreshQueued) return;
            _dirtyRefreshQueued = true;
            Dispatcher.BeginInvoke(global::System.Windows.Threading.DispatcherPriority.Background,
                new Action(() =>
                {
                    _dirtyRefreshQueued = false;
                    RefreshDirtyIndicator();
                }));
        }

        private void RefreshDirtyIndicator()
        {
            bool dirty = !TryReadCandidate(out var candidate) || !SameSettings(candidate, _savedSettings);
            UnsavedSettingsText.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        }

        private static bool SameSettings(Settings a, Settings b) =>
            a.NeverShowTutorial == b.NeverShowTutorial &&
            a.FirstLaunchSetupCompleted == b.FirstLaunchSetupCompleted &&
            a.StartWithWindows == b.StartWithWindows && a.MinimizeToTray == b.MinimizeToTray &&
            a.Theme == b.Theme && a.OverlayPosition == b.OverlayPosition &&
            a.VisualGuidanceEnabled == b.VisualGuidanceEnabled &&
            a.OverlayFixedPosition == b.OverlayFixedPosition && a.HideWidget == b.HideWidget &&
            a.OverlayCompact == b.OverlayCompact &&
            a.OverlayLeft == b.OverlayLeft && a.OverlayTop == b.OverlayTop &&
            a.FocusGrabberEnabled == b.FocusGrabberEnabled &&
            a.FocusGrabberIntervalMinutes == b.FocusGrabberIntervalMinutes && a.SnoozeMinutes == b.SnoozeMinutes &&
            a.WorkCompletionSoundEnabled == b.WorkCompletionSoundEnabled &&
            a.BreakCompletionSoundEnabled == b.BreakCompletionSoundEnabled &&
            a.FocusReminderSoundEnabled == b.FocusReminderSoundEnabled &&
            a.Hotkeys.OrderBy(h => h.Action).Select(h => (h.Action, h.Modifier1, h.Modifier2, h.Key))
                .SequenceEqual(b.Hotkeys.OrderBy(h => h.Action).Select(h => (h.Action, h.Modifier1, h.Modifier2, h.Key)));

        private bool TryReadCandidate(out Settings candidate)
        {
            candidate = _draft.Copy();
            if (PositionBox.SelectedValue is not OverlayPosition position ||
                ThemeBox.SelectedItem is not AppThemeMode theme ||
                !int.TryParse(IntervalBox.Text, out int interval) ||
                !int.TryParse(SnoozeBox.Text, out int snooze))
                return false;

            // Preserve saved drag coordinates when changing unrelated preferences.
            candidate.StartWithWindows = StartupCheck.IsChecked == true;
            candidate.MinimizeToTray = TrayCheck.IsChecked == true;
            candidate.Theme = theme;
            candidate.VisualGuidanceEnabled = VisualGuidanceCheck.IsChecked == true;
            candidate.HideWidget = HideWidgetCheck.IsChecked == true;
            candidate.OverlayFixedPosition = position;
            candidate.OverlayPosition = FreeDragCheck.IsChecked == true ? OverlayPosition.FreeDrag : position;
            candidate.FocusGrabberEnabled = FocusCheck.IsChecked == true;
            candidate.FocusGrabberIntervalMinutes = interval;
            candidate.SnoozeMinutes = snooze;
            candidate.WorkCompletionSoundEnabled = WorkSoundCheck.IsChecked == true;
            candidate.BreakCompletionSoundEnabled = BreakSoundCheck.IsChecked == true;
            candidate.FocusReminderSoundEnabled = ReminderSoundCheck.IsChecked == true;
            return true;
        }

        private void Save()
        {
            if (!TryReadCandidate(out var candidate))
            {
                ErrorText.Text = "Select a theme and widget position, and enter whole numbers for both durations.";
                return;
            }

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

            _committed = true;
            DialogResult = true;
        }

        private void OpenTutorial_Click(object sender, RoutedEventArgs e)
        {
            var tutorial = new TutorialView(_draft.NeverShowTutorial, neverShow =>
            {
                _draft.NeverShowTutorial = neverShow;
                RefreshDirtyIndicator();
            }) { Owner = this };
            tutorial.ShowDialog();
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
            QueueDirtyRefresh();
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
            QueueDirtyRefresh();
        }
    }
}
