using System;

namespace Workflow_Timer.Services.TimerEngine
{
    public class BreakTimer
    {
        private readonly CountdownTimer _timer;


        public TimeSpan Duration => _timer.Duration;

        public TimeSpan RemainingTime => _timer.RemainingTime;

        public TimeSpan ElapsedTime => _timer.ElapsedTime;

        public bool IsRunning => _timer.IsRunning;

        public bool IsPaused => _timer.IsPaused;


        public event EventHandler? Tick;

        public event EventHandler? Completed;



        public BreakTimer()
        {
            _timer = new CountdownTimer();

            _timer.Tick += (s, e) => Tick?.Invoke(this, e);
            _timer.Completed += (s, e) => Completed?.Invoke(this, e);
        }



        public void Start(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
                throw new ArgumentException(
                    "Break duration must be greater than zero.",
                    nameof(duration));

            _timer.Start(duration);
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