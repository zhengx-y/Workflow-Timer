using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//Timer complete
//Break over
//Blink animation
//Windows notification
//Sound

namespace Workflow_Timer.Services.System
{
    public sealed class NotificationService
    {
        private readonly SoundService _sound = new();

        public void Notify(Workflow_Timer.Enums.NotificationType type, Workflow_Timer.Models.Settings settings)
        {
            bool enabled = type switch
            {
                Workflow_Timer.Enums.NotificationType.WorkCompleted => settings.WorkCompletionSoundEnabled,
                Workflow_Timer.Enums.NotificationType.BreakCompleted => settings.BreakCompletionSoundEnabled,
                Workflow_Timer.Enums.NotificationType.FocusReminder => settings.FocusReminderSoundEnabled,
                _ => false
            };

            if (!enabled)
                return;

            if (type == Workflow_Timer.Enums.NotificationType.FocusReminder)
                _sound.PlaySnoozeAlert();
            else
                _sound.PlayAlarm();
        }
    }
}
