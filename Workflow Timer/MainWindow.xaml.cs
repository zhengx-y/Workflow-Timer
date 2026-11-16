using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Workflow_Timer.Viewmodels;
using Workflow_Timer.Views;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Services.Storage;
using Workflow_Timer.Services.System;
using System;
using System.Windows.Interop;

namespace Workflow_Timer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HotkeyService? _hotkeyService;
        private TrayService? _trayService;
        private bool _restoringFromTray;
        private bool _shortcutPromptChecked;
        private bool _settingsOpen;
        private bool _dispatchingHotkey;
        private string? _hotkeyWarning;
        private bool _reminderFocusDeferred;
        private bool _closing;
        private const double LeftPanelMin = 220;
        private const double LeftPanelMax = 480;
        private const double RightPanelMin = 320;
        private const double RightPanelMax = 550;
        private const double CenterPanelMin = 500;
        private const double SplitterWidth = 8;
        private double _leftExpandedWidth = LeftPanelMin;
        private double _rightExpandedWidth = RightPanelMin;
        private bool _leftCollapsed;
        private bool _rightCollapsed;

        private void ToggleLeftPanel_Click(object sender, RoutedEventArgs e)
        {
            if (!_leftCollapsed)
            {
                _leftExpandedWidth = Math.Clamp(LeftPanelColumn.ActualWidth, LeftPanelMin, LeftPanelMax);
                _leftCollapsed = true;
                HidePanel(LeftPanelColumn, LeftSplitterColumn, LeftPanel, LeftSplitter);
                LeftPanelButton.Content = "Show tasks";
            }
            else
            {
                _leftCollapsed = false;
                ShowPanel(LeftPanelColumn, LeftSplitterColumn, LeftPanel, LeftSplitter,
                    LeftPanelMin, LeftPanelMax, _leftExpandedWidth);
                LeftPanelButton.Content = "Hide tasks";
                FitSidePanels(preferRight: false);
            }
        }

        private void ToggleRightPanel_Click(object sender, RoutedEventArgs e)
        {
            if (!_rightCollapsed)
            {
                _rightExpandedWidth = Math.Clamp(RightPanelColumn.ActualWidth, RightPanelMin, RightPanelMax);
                _rightCollapsed = true;
                HidePanel(RightPanelColumn, RightSplitterColumn, RightPanel, RightSplitter);
                RightPanelButton.Content = "Show history / motivation";
            }
            else
            {
                _rightCollapsed = false;
                ShowPanel(RightPanelColumn, RightSplitterColumn, RightPanel, RightSplitter,
                    RightPanelMin, RightPanelMax, _rightExpandedWidth);
                RightPanelButton.Content = "Hide history / motivation";
                FitSidePanels(preferRight: true);
            }
        }

        private static void HidePanel(ColumnDefinition column, ColumnDefinition divider,
            FrameworkElement panel, GridSplitter splitter)
        {
            panel.Visibility = Visibility.Collapsed;
            splitter.Visibility = Visibility.Collapsed;
            column.MinWidth = 0;
            column.MaxWidth = 0;
            column.Width = new GridLength(0);
            divider.Width = new GridLength(0);
        }

        private static void ShowPanel(ColumnDefinition column, ColumnDefinition divider,
            FrameworkElement panel, GridSplitter splitter, double minimum, double maximum, double width)
        {
            column.MaxWidth = maximum;
            column.MinWidth = minimum;
            column.Width = new GridLength(Math.Clamp(width, minimum, maximum));
            divider.Width = new GridLength(SplitterWidth);
            panel.Visibility = Visibility.Visible;
            splitter.Visibility = Visibility.Visible;
        }

        private void PanelGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged && LeftPanelColumn != null && RightPanelColumn != null)
                FitSidePanels(preferRight: true);
        }

        // Keep the central column reachable when the window shrinks or a panel reopens.
        // GridSplitter itself respects the center and side-column minima while dragging.
        private void FitSidePanels(bool preferRight)
        {
            if (PanelGrid.ActualWidth <= 0) return;
            double left = _leftCollapsed ? 0 : LeftPanelColumn.Width.Value;
            double right = _rightCollapsed ? 0 : RightPanelColumn.Width.Value;
            double available = PanelGrid.ActualWidth - CenterPanelMin -
                (_leftCollapsed ? 0 : SplitterWidth) - (_rightCollapsed ? 0 : SplitterWidth);
            double excess = Math.Max(0, left + right - available);
            if (excess <= 0) return;

            void Shrink(ColumnDefinition column, double minimum, ref double width)
            {
                double amount = Math.Min(excess, Math.Max(0, width - minimum));
                if (amount <= 0) return;
                width -= amount;
                excess -= amount;
                column.Width = new GridLength(width);
            }
            if (preferRight)
            {
                if (!_rightCollapsed) Shrink(RightPanelColumn, RightPanelMin, ref right);
                if (!_leftCollapsed) Shrink(LeftPanelColumn, LeftPanelMin, ref left);
            }
            else
            {
                if (!_leftCollapsed) Shrink(LeftPanelColumn, LeftPanelMin, ref left);
                if (!_rightCollapsed) Shrink(RightPanelColumn, RightPanelMin, ref right);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            var viewModel = new MainViewModel();
            DataContext = viewModel;
            viewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.CurrentSettings))
                    UpdateTray(viewModel);
            };
            viewModel.FocusReminderRequested += (_, _) => FocusReminderRequested();
            SourceInitialized += (_, _) => InitializeHotkeys();
            Loaded += (_, _) =>
            {
                UpdateTray(viewModel);
                if (DataContext is MainViewModel vm && vm.SettingsLoadWarning is string warning)
                    MessageBox.Show(this, warning, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (_hotkeyWarning != null)
                {
                    MessageBox.Show(this, _hotkeyWarning, "Keyboard shortcuts",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    _hotkeyWarning = null;
                }
                PromptForDesktopShortcut();
            };
        }

        private void PromptForDesktopShortcut()
        {
            if (_shortcutPromptChecked || _closing) return;
            _shortcutPromptChecked = true;
            try
            {
                if (ShortcutService.HasPrompted) return;
                var answer = MessageBox.Show(this,
                    "Welcome to Workflow Timer!\n\nWould you like to create a desktop shortcut?\n\n" +
                    "You can also create one later in Settings.",
                    "Workflow Timer", MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.No);
                // Remember both Yes and No, even if shortcut creation fails.
                ShortcutService.RememberPrompt();
                if (answer == MessageBoxResult.Yes)
                    ShortcutService.CreateDesktopShortcut();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Desktop shortcut setup could not be completed. You can try again in Settings.\n\n" +
                    ex.Message, "Workflow Timer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnClosing(global::System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataManagementService.ResetInProgress) e.Cancel = false;
            if (!e.Cancel && !DataManagementService.ResetInProgress && DataContext is MainViewModel pending &&
                !pending.ResolvePendingEdits())
                e.Cancel = true;
            if (!e.Cancel && !DataManagementService.ResetInProgress && DataContext is MainViewModel tasks && !tasks.Todo.TrySave())
            {
                MessageBox.Show(this, "Task changes could not be saved. Retry saving before closing.",
                    "Unsaved tasks", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
            }
            if (!e.Cancel)
            {
                _closing = true;
                if (DataContext is MainViewModel vm)
                    vm.ShutdownFocusReminders();
                _hotkeyService?.Dispose();
                _trayService?.Dispose();
            }
        }

        private void FocusReminderRequested()
        {
            if (_closing)
                return;
            if (_settingsOpen || !IsEnabled)
            {
                _reminderFocusDeferred = true;
                return;
            }
            _reminderFocusDeferred = false;
            RestoreMainWindow();
        }

        private void RestoreMainWindow()
        {
            if (_closing || _restoringFromTray) return;
            _restoringFromTray = true;
            try
            {
                WindowFocusService.BringIntoFocus(this);
            }
            finally
            {
                _restoringFromTray = false;
            }
        }

        private void UpdateTray(MainViewModel vm)
        {
            if (_closing) return;
            try
            {
                if (vm.CurrentSettings.MinimizeToTray && _trayService == null)
                    _trayService = new TrayService(this, vm,
                        () => !_closing && !_settingsOpen && IsEnabled, RestoreMainWindow);
                _trayService?.SetEnabled(vm.CurrentSettings.MinimizeToTray);
                if (!vm.CurrentSettings.MinimizeToTray && !IsVisible && IsLoaded)
                    RestoreMainWindow();
            }
            catch (Exception ex)
            {
                _trayService?.Dispose();
                _trayService = null;
                MessageBox.Show(this, "System tray is unavailable. Normal minimizing remains active.\n\n" + ex.Message,
                    "Workflow Timer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (!_closing && !_restoringFromTray && WindowState == WindowState.Minimized && _trayService != null &&
                DataContext is MainViewModel vm && vm.CurrentSettings.MinimizeToTray)
                Hide();
        }

        private void InitializeHotkeys()
        {
            if (DataContext is not MainViewModel vm)
                return;
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if (source == null)
            {
                _hotkeyWarning = "Could not initialize global keyboard shortcuts.";
                return;
            }
            _hotkeyService = new HotkeyService(source);
            _hotkeyService.HotkeyPressed += (_, action) => DispatchHotkey(action);
            var errors = _hotkeyService.RegisterInitial(vm.CurrentSettings.Hotkeys);
            if (errors.Count > 0)
                _hotkeyWarning = "Some shortcuts could not be registered. Other shortcuts remain available.\n\n"
                    + string.Join("\n", errors);
        }

        private void DispatchHotkey(HotkeyAction action)
        {
            if (_settingsOpen || _dispatchingHotkey || !IsEnabled ||
                DataContext is not MainViewModel vm)
                return;
            _dispatchingHotkey = true;
            try
            {
                if (action == HotkeyAction.ShowMainWindow)
                {
                    RestoreMainWindow();
                }
                else
                    vm.ExecuteHotkey(action);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Keyboard shortcut",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _dispatchingHotkey = false;
            }
        }

        private void SaveSettingsWithHotkeys(MainViewModel vm, Settings settings)
        {
            SettingsService.Validate(settings);
            if (_hotkeyService == null)
            {
                if (global::System.Linq.Enumerable.Any(settings.Hotkeys, binding => binding.IsAssigned))
                    throw new InvalidOperationException("Global keyboard shortcuts could not be initialized.");
                vm.SaveSettings(settings);
                return;
            }
            var service = _hotkeyService;
            var previous = service.Snapshot();
            service.Apply(settings.Hotkeys);
            try
            {
                vm.SaveSettings(settings);
            }
            catch (Exception saveError)
            {
                try
                {
                    service.Apply(previous);
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException(saveError.Message +
                        " Could not restore keyboard shortcuts: " + restoreError.Message, restoreError);
                }
                throw;
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm || !vm.CanEditSettings)
                return;

            var settings = new SettingsView(vm.CurrentSettings.Copy(),
                candidate => SaveSettingsWithHotkeys(vm, candidate))
            {
                Owner = this
            };
            _settingsOpen = true;
            try
            {
                settings.ShowDialog();
            }
            finally
            {
                _settingsOpen = false;
                if (_reminderFocusDeferred)
                {
                    _reminderFocusDeferred = false;
                    if (vm.IsFocusReminderAttentionActive)
                        FocusReminderRequested();
                }
            }
            if (settings.ResetDataRequested && vm.CanEditSettings)
            {
                try
                {
                    DataManagementService.PrepareReset();
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not reset data: " + ex.Message,
                        "Workflow Timer", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
