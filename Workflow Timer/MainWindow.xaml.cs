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
        private bool _settingsOpen;
        private bool _dispatchingHotkey;
        private string? _hotkeyWarning;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainViewModel();
            SourceInitialized += (_, _) => InitializeHotkeys();
            Loaded += (_, _) =>
            {
                if (DataContext is MainViewModel vm && vm.SettingsLoadWarning is string warning)
                    MessageBox.Show(this, warning, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                if (_hotkeyWarning != null)
                {
                    MessageBox.Show(this, _hotkeyWarning, "Keyboard shortcuts",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    _hotkeyWarning = null;
                }
            };
        }

        protected override void OnClosing(global::System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!e.Cancel)
                _hotkeyService?.Dispose();
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
                    if (!IsVisible) Show();
                    if (WindowState == WindowState.Minimized)
                        WindowState = WindowState.Normal;
                    Activate();
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
            }
        }
    }
}
