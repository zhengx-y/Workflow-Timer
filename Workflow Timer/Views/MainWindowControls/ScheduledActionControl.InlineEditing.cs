using System.Windows.Input;
using System.Windows.Threading;
using Workflow_Timer.Viewmodels;

namespace Workflow_Timer.Views.MainWindowControls
{
    public partial class ScheduledActionControl
    {
        private void ActionOffsetLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Defer reordering until the focus transition has finished.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new global::System.Action(() =>
            {
                if (DataContext is MainViewModel vm)
                    vm.SortActionRows();
            }));
        }
    }
}
