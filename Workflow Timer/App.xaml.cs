using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Workflow_Timer.Services.System;

namespace Workflow_Timer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private SingleInstanceService? _singleInstance;
        private bool _isPrimary;
        private bool _activationPending;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Acquire ownership before constructing the window or loading user data.
            _singleInstance = new SingleInstanceService();
            _isPrimary = _singleInstance.TryAcquire();
            if (!_isPrimary)
            {
                if (_singleInstance.RequestActivation())
                {
                    Shutdown();
                    return;
                }

                // The original instance may have exited while we were connecting.
                _isPrimary = _singleInstance.TryAcquire();
                if (!_isPrimary)
                {
                    MessageBox.Show("Workflow Timer is already running, but could not be contacted. Try again in a moment.",
                        "Workflow Timer", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
                }
            }

            _singleInstance.StartListening(() =>
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(ActivateExistingWindow)));

            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (_activationPending)
                ActivateExistingWindow();
        }

        private void ActivateExistingWindow()
        {
            if (Dispatcher.HasShutdownStarted || Services.Storage.DataManagementService.ResetInProgress)
                return;
            if (MainWindow is not MainWindow window)
            {
                _activationPending = true;
                return;
            }
            _activationPending = false;
            window.ActivateFromAnotherLaunch();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { base.OnExit(e); }
            finally
            {
                // Release ownership before the existing reset-and-restart flow launches the replacement.
                _singleInstance?.Dispose();
                if (_isPrimary)
                    Services.Storage.DataManagementService.FinishResetAndRestart();
            }
        }
    }

}
