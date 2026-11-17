using System.Windows;

namespace Workflow_Timer.Views
{
    public enum BrowseTargetKind
    {
        None,
        File,
        Folder
    }

    public partial class BrowseTargetView : Window
    {
        public BrowseTargetKind SelectedTarget { get; private set; }

        public BrowseTargetView()
        {
            InitializeComponent();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            SelectedTarget = BrowseTargetKind.File;
            DialogResult = true;
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            SelectedTarget = BrowseTargetKind.Folder;
            DialogResult = true;
        }
    }
}
