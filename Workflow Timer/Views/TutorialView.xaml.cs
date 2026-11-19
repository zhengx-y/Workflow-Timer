using global::System;
using global::System.Windows;

namespace Workflow_Timer.Views
{
    public partial class TutorialView : Window
    {
        private readonly Action<bool> _savePreference;
        public TutorialView(bool neverShow, Action<bool> savePreference)
        {
            InitializeComponent();
            _savePreference = savePreference;
            NeverShowCheck.IsChecked = neverShow;
        }
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _savePreference(NeverShowCheck.IsChecked == true);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Could not save tutorial preference: " + ex.Message;
            }
        }
    }
}
