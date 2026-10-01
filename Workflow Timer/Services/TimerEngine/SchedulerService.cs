using System;
using System.Collections.Generic;
using System.Linq;
using Workflow_Timer.Models;
using Workflow_Timer.Services.System;

namespace Workflow_Timer.Services.TimerEngine
{
    public class SchedulerService
    {
        private readonly LauncherService _launcherService;

        private List<ScheduledAction> _actions = new();

        private readonly HashSet<ScheduledAction> _executedActions = new();

        public SchedulerService(LauncherService launcherService)
        {
            _launcherService = launcherService;
        }

        public void LoadActions(IEnumerable<ScheduledAction> actions)
        {
            _actions = actions
                .Where(a => a.Enabled)
                .ToList();

            _executedActions.Clear();
        }

        public void Check(TimeSpan elapsedTime)
        {
            foreach (var action in _actions)
            {
                if (_executedActions.Contains(action))
                    continue;

                if (elapsedTime >= action.OffsetFromStart)
                {
                    _launcherService.Launch(action);

                    _executedActions.Add(action);

                    ActionTriggered?.Invoke(
                        this,
                        action);
                }
            }
        }

        public event EventHandler<ScheduledAction>? ActionTriggered;

        public void Reset()
        {
            _executedActions.Clear();
        }
    }
}