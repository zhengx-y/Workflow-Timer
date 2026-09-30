namespace Workflow_Timer.Models
{
    public class Preset
    {
        public string Name { get; set; } = string.Empty;

        public TimerSession Session { get; set; } = new();
    }
}