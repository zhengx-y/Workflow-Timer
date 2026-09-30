using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using global::System.Windows.Threading;

namespace Workflow_Timer.Services.TimerEngine
{
    public class CountdownTimer
    {
        private readonly DispatcherTimer _timer;

        public TimeSpan Duration { get; private set; }

        public TimeSpan RemainingTime { get; private set; }

        public TimeSpan ElapsedTime
        {
            get
            {
                return Duration - RemainingTime;
            }
        }

        public bool IsRunning { get; private set; }

        public bool IsPaused { get; private set; }


        public event EventHandler? Tick;

        public event EventHandler? Completed;


        public CountdownTimer()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _timer.Tick += TimerTick;
        }


        public void Start(TimeSpan duration)
        {
            Stop();

            Duration = duration;
            RemainingTime = duration;

            IsRunning = true;
            IsPaused = false;

            _timer.Start();

            Tick?.Invoke(this, EventArgs.Empty);
        }


        public void Pause()
        {
            if (!IsRunning || IsPaused)
                return;

            IsPaused = true;
            _timer.Stop();
        }


        public void Resume()
        {
            if (!IsRunning || !IsPaused)
                return;

            IsPaused = false;
            _timer.Start();
        }


        public void Stop()
        {
            _timer.Stop();

            IsRunning = false;
            IsPaused = false;

            Duration = TimeSpan.Zero;
            RemainingTime = TimeSpan.Zero;
        }


        public void Extend(TimeSpan extension)
        {
            if (extension <= TimeSpan.Zero)
                return;

            Duration += extension;
            RemainingTime += extension;
        }


        private void TimerTick(object? sender, EventArgs e)
        {
            if (!IsRunning || IsPaused)
                return;


            RemainingTime -= TimeSpan.FromSeconds(1);


            if (RemainingTime <= TimeSpan.Zero)
            {
                RemainingTime = TimeSpan.Zero;

                _timer.Stop();

                IsRunning = false;

                Completed?.Invoke(this, EventArgs.Empty);
            }


            Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}