using System.Windows.Controls;

using System.Windows.Input;

namespace Workflow_Timer.Views.MainWindowControls
{
    public partial class PresetControl : UserControl
    {
        public PresetControl()
        {
            InitializeComponent();
        }

        private void PresetName_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox box) box.SelectAll();
        }

        private void PresetName_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox box && !box.IsKeyboardFocusWithin)
            {
                e.Handled = true;
                box.Focus();
                box.SelectAll();
            }
        }
    }
}
