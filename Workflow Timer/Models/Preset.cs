namespace Workflow_Timer.Models
{
    public class Preset : global::System.ComponentModel.INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public global::System.Guid Id { get; set; }

        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                    return;
                _name = value;
                PropertyChanged?.Invoke(this,
                    new global::System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event global::System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public TimerSession Session { get; set; } = new();
    }
}
