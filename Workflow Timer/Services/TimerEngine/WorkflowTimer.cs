using System;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;

namespace Workflow_Timer.Services.TimerEngine
{
    public class WorkflowTimer
    {
        private readonly WorkTimer _workTimer;
        private readonly BreakTimer _breakTimer;

        private TimerSession? _currentSession;


        public TimerSession? CurrentSession => _currentSession;


        public bool IsRunning =>
            _workTimer.IsRunning || _breakTimer.IsRunning;


        public SessionState State { get; private set; }
            = SessionState.Idle;


        public TimeSpan RemainingTime =>
            State switch
            {
                SessionState.Working => _workTimer.RemainingTime,
                SessionState.Break => _breakTimer.RemainingTime,
                _ => TimeSpan.Zero
            };


        // Used by SchedulerService
        public TimeSpan WorkElapsedTime =>
            _workTimer.ElapsedTime;


        public TimeSpan BreakElapsedTime =>
            _breakTimer.ElapsedTime;



        public event EventHandler? Tick;

        public event EventHandler? WorkCompleted;

        public event EventHandler? BreakStarted;

        public event EventHandler? BreakCompleted;

        public event EventHandler? SessionCompleted;



        public WorkflowTimer(
            WorkTimer workTimer,
            BreakTimer breakTimer)
        {
            _workTimer = workTimer;
            _breakTimer = breakTimer;


            _workTimer.Tick += TimerTick;
            _breakTimer.Tick += TimerTick;


            _workTimer.Completed += WorkFinished;
            _breakTimer.Completed += BreakFinished;
        }



        private void TimerTick(object? sender, EventArgs e)
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }



        public void Start(TimerSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));


            if (IsRunning)
                throw new InvalidOperationException(
                    "A timer session is already running.");


            _currentSession = session;

            State = SessionState.Working;

            _workTimer.Start(session);
        }



        public void Pause()
        {
            switch (State)
            {
                case SessionState.Working:
                    _workTimer.Pause();
                    break;

                case SessionState.Break:
                    _breakTimer.Pause();
                    break;

                default:
                    return;
            }


            State = SessionState.Paused;
        }



        public void Resume()
        {
            if (State != SessionState.Paused)
                return;


            if (_workTimer.IsPaused)
            {
                _workTimer.Resume();
                State = SessionState.Working;
            }
            else if (_breakTimer.IsPaused)
            {
                _breakTimer.Resume();
                State = SessionState.Break;
            }
        }



        public void Stop()
        {
            _workTimer.Stop();
            _breakTimer.Stop();

            _currentSession = null;

            State = SessionState.Idle;
        }



        public void Extend(TimeSpan amount)
        {
            if (amount <= TimeSpan.Zero)
                return;


            switch (State)
            {
                case SessionState.Working:
                    _workTimer.Extend(amount);
                    break;

                case SessionState.Break:
                    _breakTimer.Extend(amount);
                    break;
            }
        }



        public void Repeat()
        {
            if (_currentSession == null)
                return;


            TimerSession session = _currentSession;


            Stop();

            Start(session);
        }



        private void WorkFinished(object? sender, EventArgs e)
        {
            WorkCompleted?.Invoke(this, EventArgs.Empty);


            if (_currentSession == null)
                return;


            State = SessionState.Break;


            BreakStarted?.Invoke(this, EventArgs.Empty);


            _breakTimer.Start(
                _currentSession.BreakDuration);
        }



        private void BreakFinished(object? sender, EventArgs e)
        {
            State = SessionState.Completed;


            BreakCompleted?.Invoke(this, EventArgs.Empty);

            SessionCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}