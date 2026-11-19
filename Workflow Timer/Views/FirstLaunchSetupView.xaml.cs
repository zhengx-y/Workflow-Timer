using global::System;
using global::System.Windows;

namespace Workflow_Timer.Views
{
    public partial class FirstLaunchSetupView : Window
    {
        private readonly Action<bool, bool> _apply;
        public FirstLaunchSetupView(bool startWithWindows, Action<bool, bool> apply)
        {
            InitializeComponent();
            _apply = apply;
            StartupCheck.IsChecked = startWithWindows;
        }
        private void Done_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _apply(ShortcutCheck.IsChecked == true, StartupCheck.IsChecked == true);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Could not complete setup: " + ex.Message;
            }
        }
    }
}
