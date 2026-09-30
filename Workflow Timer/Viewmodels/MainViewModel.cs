using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Workflow_Timer.Commands;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Services.TimerEngine;
using Workflow_Timer.Services.System;

namespace Workflow_Timer.Viewmodels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly WorkflowTimer _workflowTimer;

        private readonly SchedulerService _schedulerService;

        private Preset? _selectedPreset;

        public ObservableCollection<Preset> Presets { get; }

        public Preset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset == value)
                    return;

                _selectedPreset = value;
                OnPropertyChanged();
            }
        }

        public TimerCommands TimerCommands { get; }

        public PresetCommands PresetCommands { get; }

        public TimeSpan RemainingTime =>
            _workflowTimer.RemainingTime;

        public SessionState State =>
            _workflowTimer.State;

        public bool IsRunning =>
            _workflowTimer.IsRunning;

        public MainViewModel()
        {
            _workflowTimer = new WorkflowTimer(
                new WorkTimer(),
                new BreakTimer());

            _workflowTimer.Tick += WorkflowTimer_Tick;
            _workflowTimer.SessionCompleted += WorkflowTimer_SessionCompleted;

            _schedulerService = new SchedulerService(
                new LauncherService());

            Presets = new ObservableCollection<Preset>
            {
                new Preset
                {
                    Name = "Default",
                    Session = new TimerSession
                    {
                        Label = "Work",
                        WorkDuration = TimeSpan.FromMinutes(25),
                        BreakDuration = TimeSpan.FromMinutes(5)
                    }
                }
            };

            SelectedPreset = Presets[0];

            TimerCommands = new TimerCommands(
                StartTimer,
                PauseTimer,
                ResumeTimer,
                StopTimer,
                RepeatTimer,
                ExtendTimer);

            PresetCommands = new PresetCommands(
                LoadPreset,
                SavePreset,
                CreatePreset,
                DeletePreset);
        }

        private void StartTimer()
        {
            if (SelectedPreset == null)
                return;

            _schedulerService.LoadActions(
                SelectedPreset.Session.Actions);

            _workflowTimer.Start(SelectedPreset.Session);

            NotifyTimerProperties();
        }

        private void PauseTimer()
        {
            _workflowTimer.Pause();

            NotifyTimerProperties();
        }

        private void ResumeTimer()
        {
            _workflowTimer.Resume();

            NotifyTimerProperties();
        }

        private void StopTimer()
        {
            _workflowTimer.Stop();
            _schedulerService.Reset();

            NotifyTimerProperties();
        }

        private void RepeatTimer()
        {
            _workflowTimer.Repeat();

            NotifyTimerProperties();
        }

        private void ExtendTimer(object? value)
        {
            if (value == null)
                return;

            if (!int.TryParse(value.ToString(), out int minutes))
                return;

            if (minutes <= 0)
                return;

            _workflowTimer.Extend(
                TimeSpan.FromMinutes(minutes));

            NotifyTimerProperties();
        }

        private void WorkflowTimer_Tick(
    object? sender,
    EventArgs e)
        {
            _schedulerService.Check(
                _workflowTimer.WorkElapsedTime);

            NotifyTimerProperties();
        }

        private void WorkflowTimer_SessionCompleted(
            object? sender,
            EventArgs e)
        {
            NotifyTimerProperties();
        }

        private void LoadPreset()
        {
            if (SelectedPreset == null)
                return;

            StopTimer();
        }

        private void SavePreset()
        {
            // Persistence will be implemented by PresetService.
        }

        private void CreatePreset()
        {
            var preset = new Preset
            {
                Name = "New Preset",
                Session = new TimerSession
                {
                    Label = "Work",
                    WorkDuration = TimeSpan.FromMinutes(25),
                    BreakDuration = TimeSpan.FromMinutes(5)
                }
            };

            Presets.Add(preset);
            SelectedPreset = preset;
        }

        private void DeletePreset()
        {
            if (SelectedPreset == null)
                return;

            if (Presets.Count <= 1)
                return;

            int index = Presets.IndexOf(SelectedPreset);

            Presets.Remove(SelectedPreset);

            SelectedPreset =
                index < Presets.Count
                    ? Presets[index]
                    : Presets[^1];
        }

        private void NotifyTimerProperties()
        {
            OnPropertyChanged(nameof(RemainingTime));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsRunning));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}