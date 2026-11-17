using global::System.Windows;
using Workflow_Timer.Services.System;

namespace Workflow_Timer
{
    public partial class MainWindow
    {
        internal void ActivateFromAnotherLaunch()
        {
            if (_closing) return;

            // Keep an open modal settings/browse window in front of its owner.
            if (!IsEnabled || _settingsOpen)
            {
                foreach (Window candidate in Application.Current.Windows)
                {
                    if (candidate == this || !candidate.IsVisible || !candidate.IsEnabled)
                        continue;
                    Window? owner = candidate.Owner;
                    while (owner != null && owner != this) owner = owner.Owner;
                    if (owner == this)
                    {
                        WindowFocusService.BringIntoFocus(candidate);
                        return;
                    }
                }
                // Native file dialogs are not in Application.Windows.
                if (!IsEnabled) return;
            }

            RestoreMainWindow();
        }
    }
}
