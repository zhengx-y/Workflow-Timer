using global::System.Windows;
using global::System.Windows.Controls;
using global::System.Windows.Media;
using global::System.Windows.Threading;
using Workflow_Timer.Viewmodels;

namespace Workflow_Timer.Views.MainWindowControls
{
    public partial class TodoControl : UserControl
    {
        public TodoControl() => InitializeComponent();

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not TodoViewModel vm || !vm.CanEditTasks) return;
            vm.Commands.AddTaskCommand.Execute(null);
            if (vm.LastAddedTask is not { } task) return;
            ActiveList.ScrollIntoView(task);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new global::System.Action(() =>
            {
                ActiveList.UpdateLayout();
                if (ActiveList.ItemContainerGenerator.ContainerFromItem(task) is DependencyObject container &&
                    FindTitle(container) is TextBox title)
                {
                    title.Focus();
                    title.SelectAll();
                }
            }));
        }

        private static TextBox? FindTitle(DependencyObject parent)
        {
            if (parent is TextBox box && box.Name == "TaskTitle") return box;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var found = FindTitle(VisualTreeHelper.GetChild(parent, i));
                if (found != null) return found;
            }
            return null;
        }
    }
}
