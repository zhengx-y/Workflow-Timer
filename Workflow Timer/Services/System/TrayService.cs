using global::System;
using global::System.Drawing;
using global::System.Windows;
using Forms = global::System.Windows.Forms;
using Workflow_Timer.Viewmodels;

namespace Workflow_Timer.Services.System
{
    public sealed class TrayService : IDisposable
    {
        private readonly Forms.NotifyIcon _icon;
        private readonly Forms.ContextMenuStrip _menu = new();
        private readonly Icon _image;
        private readonly Forms.ToolStripMenuItem _start, _pause, _stop;
        private readonly Window _window;
        private readonly MainViewModel _vm;
        private readonly Func<bool> _canInteract;
        private readonly Action _restore;
        private bool _disposed;

        public TrayService(Window window, MainViewModel vm, Func<bool> canInteract, Action restore)
        {
            _window = window;
            _vm = vm;
            _canInteract = canInteract;
            _restore = restore;
            var resource = Application.GetResourceStream(
                new Uri("pack://application:,,,/Resources/Icons/Workflow_Timer.ico"));
            if (resource == null) throw new InvalidOperationException("Tray icon resource is missing.");
            using (var stream = resource.Stream)
            using (var original = new Icon(stream))
                _image = (Icon)original.Clone();

            _icon = new Forms.NotifyIcon { Icon = _image, Text = "Workflow Timer", ContextMenuStrip = _menu };
            Add("Show Workflow Timer", _restore);
            _menu.Items.Add(new Forms.ToolStripSeparator());
            _start = Add("Start", () => Execute(_vm.TimerCommands.StartCommand));
            _pause = Add("Pause / Resume", () => Execute(_vm.CanResumeTimer
                ? _vm.TimerCommands.ResumeCommand : _vm.TimerCommands.PauseCommand));
            _stop = Add("Stop", () => Execute(_vm.TimerCommands.StopCommand));
            _menu.Items.Add(new Forms.ToolStripSeparator());
            Add("Exit", () => { _restore(); _window.Close(); });
            _menu.Opening += (_, _) =>
            {
                bool allowed = _canInteract();
                _start.Enabled = allowed && _vm.TimerCommands.StartCommand.CanExecute(null);
                _pause.Enabled = allowed && (_vm.CanPauseTimer || _vm.CanResumeTimer);
                _pause.Text = _vm.CanResumeTimer ? "Resume" : "Pause";
                _stop.Enabled = allowed && _vm.HasActiveTimer;
            };
            _icon.DoubleClick += (_, _) => Run(_restore);
        }

        private Forms.ToolStripMenuItem Add(string label, Action action)
        {
            var item = new Forms.ToolStripMenuItem(label);
            item.Click += (_, _) => Run(action);
            _menu.Items.Add(item);
            return item;
        }

        private void Run(Action action)
        {
            if (_disposed || !_canInteract()) return;
            try { action(); }
            catch (Exception ex)
            {
                _restore();
                MessageBox.Show(_window, ex.Message, "Workflow Timer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void Execute(global::System.Windows.Input.ICommand command)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }

        public void SetEnabled(bool enabled) => _icon.Visible = enabled;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
            _image.Dispose();
        }
    }
}
