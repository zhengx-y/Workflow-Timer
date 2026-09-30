using Workflow_Timer.Enums;

namespace Workflow_Timer.Models
{
    public class ScheduledAction
    {
        public string Name { get; set; }

        public ActionType ActionType { get; set; }

        public string Target { get; set; }

        public TimeSpan OffsetFromStart { get; set; }

        public bool Enabled { get; set; }
    }
}