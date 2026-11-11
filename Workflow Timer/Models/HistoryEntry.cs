using System;

namespace Workflow_Timer.Models
{
    public class HistoryEntry
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public string PresetName { get; set; } = string.Empty;
        public TimeSpan WorkDuration { get; set; }
        public bool Completed { get; set; }
    }
}
