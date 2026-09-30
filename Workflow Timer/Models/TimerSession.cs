using Workflow_Timer.Models;

namespace Workflow_Timer.Models
{
    public class TimerSession
    {
        public string Label { get; set; }

        public TimeSpan WorkDuration { get; set; }

        public TimeSpan BreakDuration { get; set; }

        public List<ScheduledAction> Actions { get; set; } = new();
    }
}