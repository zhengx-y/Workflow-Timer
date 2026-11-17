using System.Windows.Input;
using System.Windows.Threading;
using Workflow_Timer.Viewmodels;
using Workflow_Timer.Views.Controls;

namespace Workflow_Timer.Views.MainWindowControls
{
    public partial class ScheduledActionControl
    {
        private void ActionOffsetLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Defer reordering until the focus transition has finished.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new global::System.Action(() =>
            {
                // Moving between hours, minutes and seconds is still editing the same offset.
                if (sender is TimeSpinnerControl spinner && spinner.IsKeyboardFocusWithin)
                    return;
                if (DataContext is MainViewModel vm)
                    vm.SortActionRows();
            }));
        }
    }
}
