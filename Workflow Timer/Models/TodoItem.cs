using global::System.ComponentModel;
using global::System.Runtime.CompilerServices;

namespace Workflow_Timer.Models
{
    public sealed class TodoItem : INotifyPropertyChanged
    {
        private string _title = "New task";
        private string _description = string.Empty;
        private bool _isCompleted;

        public string Title
        {
            get => _title;
            set
            {
                value ??= string.Empty;
                if (_title == value) return;
                _title = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                value ??= string.Empty;
                if (_description == value) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted == value) return;
                _isCompleted = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
