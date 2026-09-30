using Workflow_Timer.Models;
using Workflow_Timer.Services.TimerEngine;

namespace Workflow_Timer.Services.TimerEngine
{
    public class WorkTimer
    {
        private readonly CountdownTimer _timer;

        public string Label { get; private set; } = string.Empty;

        public TimeSpan Duration => _timer.Duration;

        public TimeSpan RemainingTime => _timer.RemainingTime;

        public TimeSpan ElapsedTime => _timer.ElapsedTime;

        public bool IsRunning => _timer.IsRunning;

        public bool IsPaused => _timer.IsPaused;


        public event EventHandler? Tick;
        public event EventHandler? Completed;


        public WorkTimer()
        {
            _timer = new CountdownTimer();

            _timer.Tick += (s, e) => Tick?.Invoke(this, e);
            _timer.Completed += (s, e) => Completed?.Invoke(this, e);
        }


        public void Start(TimerSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            Label = session.Label;

            _timer.Start(session.WorkDuration);
        }


        public void Pause()
        {
            _timer.Pause();
        }


        public void Resume()
        {
            _timer.Resume();
        }


        public void Stop()
        {
            _timer.Stop();
        }


        public void Extend(TimeSpan amount)
        {
            _timer.Extend(amount);
        }
    }
}