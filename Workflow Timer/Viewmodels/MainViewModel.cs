using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Workflow_Timer.Commands;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Services.TimerEngine;
using Workflow_Timer.Services.System;
using System.Linq;
using Workflow_Timer.Services.Storage;
using System.Windows;
using System.Windows.Input;
using System.Globalization;
using System.IO;
using Workflow_Timer.Helpers;
using Microsoft.Win32;

namespace Workflow_Timer.Viewmodels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly WorkflowTimer _workflowTimer;
        private readonly SchedulerService _schedulerService;

        private readonly OverlayService _overlayService;

        private readonly PresetService _presetService;
        private readonly HistoryService _historyService;
        private readonly SettingsService _settingsService;
        private readonly StartupService _startupService = new();
        private readonly FocusReminderService _focusReminder = new();
        public Settings CurrentSettings { get; private set; } = new();
        public string? SettingsLoadWarning { get; private set; }
        public bool IsFocusReminderVisible => _focusReminder.IsVisible;
        public bool IsFocusReminderAttentionActive => _focusReminder.IsAttentionActive;
        public bool IsFocusReminderBlinkOn => _focusReminder.IsBlinkOn;
        public string SnoozeReminderText => $"Snooze {CurrentSettings.SnoozeMinutes} min";
        public string FocusReminderIntervalText
        {
            get
            {
                var remaining = _focusReminder.Remaining;
                var countdown = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
                var status = _focusReminder.IsSnoozed ? "Snoozed" :
                    _focusReminder.IsAttentionActive ? "Reminder active" : "Dismissed";
                return $"{status} — Next reminder in {countdown}";
            }
        }
        public ICommand SnoozeReminderCommand { get; }
        public ICommand DismissReminderCommand { get; }
        public ICommand CancelReminderCommand { get; }
        public event EventHandler? FocusReminderRequested;
        private DateTime _workStartedAt;
        private bool _workRecorded;

        private Preset? _selectedPreset;
        private string _presetNameDraft = string.Empty;
        private string _presetWorkDraft = string.Empty;
        private string _presetBreakDraft = string.Empty;
        private bool _resolvingPresetEdits;

        public string PresetNameDraft
        {
            get => _presetNameDraft;
            set { _presetNameDraft = value; NotifyPresetDraft(); }
        }

        public string PresetWorkDraft
        {
            get => _presetWorkDraft;
            set { _presetWorkDraft = value; NotifyPresetDraft(); }
        }

        public string PresetBreakDraft
        {
            get => _presetBreakDraft;
            set { _presetBreakDraft = value; NotifyPresetDraft(); }
        }

        public bool HasUnsavedPresetChanges => SelectedPreset is Preset preset &&
            (PresetNameDraft != preset.Name ||
             !TryParsePresetDuration(PresetWorkDraft, out var work) || work != preset.Session.WorkDuration ||
             !TryParsePresetDuration(PresetBreakDraft, out var pause) || pause != preset.Session.BreakDuration);

        private bool CanRunSelectedPreset => SelectedPreset != null &&
            !HasUnsavedPresetChanges && !_resolvingPresetEdits &&
            SelectedPreset.Session.WorkDuration > TimeSpan.Zero &&
            SelectedPreset.Session.BreakDuration >= TimeSpan.Zero;
        private ScheduledActionViewModel? _selectedScheduledAction;
        private string _actionName = string.Empty;
        private string _actionTarget = string.Empty;
        private string _actionOffsetText = "00:00:00";
        private ActionType _selectedActionType = ActionType.Website;
        private bool _actionEnabled = true;

        public Array ActionTypes { get; } = Enum.GetValues<ActionType>();

        public ICommand AddScheduledActionCommand { get; }
        public ICommand UpdateScheduledActionCommand { get; }
        public ICommand RemoveScheduledActionCommand { get; }
        public ICommand ClearActionEditorCommand { get; }
        public ICommand ToggleScheduledActionEnabledCommand { get; }
        public ICommand BrowseActionTargetCommand { get; }

        public ObservableCollection<HistoryDisplayRow> RecentHistory { get; } = new();
        public ObservableCollection<HistoryDisplayRow> DailyHistory { get; } = new();

        private string _todayWorkText = "0m";
        private string _allTimeWorkText = "0m";
        private string _bestDayText = "—";
        private string _bestDayDateText = "No record yet";
        private string _recordResetText = string.Empty;

        public string TodayWorkText { get => _todayWorkText; private set { _todayWorkText = value; OnPropertyChanged(); } }
        public string AllTimeWorkText { get => _allTimeWorkText; private set { _allTimeWorkText = value; OnPropertyChanged(); } }
        public string BestDayText { get => _bestDayText; private set { _bestDayText = value; OnPropertyChanged(); } }
        public string BestDayDateText { get => _bestDayDateText; private set { _bestDayDateText = value; OnPropertyChanged(); } }
        public string RecordResetText { get => _recordResetText; private set { _recordResetText = value; OnPropertyChanged(); } }
        public ICommand ClearPersonalBestCommand { get; }

        public bool CanBrowseActionTarget =>
            SelectedActionType != ActionType.Website;

        public ScheduledActionViewModel? SelectedScheduledAction
        {
            get => _selectedScheduledAction;
            set
            {
                if (ReferenceEquals(_selectedScheduledAction, value))
                    return;

                _selectedScheduledAction = value;
                OnPropertyChanged();

                if (value == null)
                {
                    ClearActionFields();
                    return;
                }

                ActionName = value.Action.Name;
                SelectedActionType = value.Action.ActionType;
                ActionTarget = value.Action.Target;
                ActionOffsetText = value.Action.OffsetFromStart.ToString("c", CultureInfo.InvariantCulture);
                ActionEnabled = value.Action.Enabled;
            }
        }

        public string ActionName
        {
            get => _actionName;
            set { _actionName = value; OnPropertyChanged(); }
        }

        public ActionType SelectedActionType
        {
            get => _selectedActionType;
            set
            {
                if (_selectedActionType == value)
                    return;

                _selectedActionType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanBrowseActionTarget));
            }
        }

        public string ActionTarget
        {
            get => _actionTarget;
            set { _actionTarget = value; OnPropertyChanged(); }
        }

        public string ActionOffsetText
        {
            get => _actionOffsetText;
            set { _actionOffsetText = value; OnPropertyChanged(); }
        }

        public bool ActionEnabled
        {
            get => _actionEnabled;
            set { _actionEnabled = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Preset> Presets { get; }

        public ObservableCollection<ScheduledActionViewModel> ScheduledActions { get; }

        public Preset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset == value)
                    return;

                if (value == null || !CanEditSettings || !ResolvePendingPresetEdits())
                {
                    // Restore the ComboBox selection after its TwoWay update finishes.
                    Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                        OnPropertyChanged(nameof(SelectedPreset))));
                    return;
                }

                SelectPreset(value);
            }
        }

        private void SelectPreset(Preset value)
        {
            _selectedPreset = value;
            ResetPresetDraft();

            RefreshScheduledActions();
            SelectedScheduledAction = null;
            ClearActionFields();

            OnPropertyChanged(nameof(SelectedPreset));
        }

        public TimerCommands TimerCommands { get; }

        public PresetCommands PresetCommands { get; }

        public TimeSpan RemainingTime =>
            _workflowTimer.RemainingTime;

        public string RemainingTimeText =>
    RemainingTime.ToString(@"hh\:mm\:ss");

        public SessionState State =>
            _workflowTimer.State;

        public bool IsRunning =>
            _workflowTimer.IsRunning;

        // Button visibility is derived from the actual session state.
        public bool CanStartTimer =>
            State == SessionState.Idle || State == SessionState.Completed;

        public bool HasActiveTimer =>
            State == SessionState.Working ||
            State == SessionState.Break ||
            State == SessionState.Paused;

        public bool CanPauseTimer =>
            State == SessionState.Working || State == SessionState.Break;

        public bool CanResumeTimer => State == SessionState.Paused;

        public bool CanRepeatTimer => HasActiveTimer || State == SessionState.Completed;

        public bool CanExtendTimer =>
            State == SessionState.Working || State == SessionState.Break;

        // Keep settings visible, but read-only during work, break, or pause.
        public bool CanEditSettings =>
            _workflowTimer.State != SessionState.Working &&
            _workflowTimer.State != SessionState.Break &&
            _workflowTimer.State != SessionState.Paused;

        public MainViewModel()
        {
            _settingsService = new SettingsService(new AppDataPathService());
            try
            {
                CurrentSettings = _settingsService.Load();
            }
            catch (Exception ex)
            {
                SettingsLoadWarning = "Could not load settings. Defaults are active for this run. "
                    + "Saving settings will replace the existing settings file.\n\n" + ex.Message;
            }

            _workflowTimer = new WorkflowTimer(
                new WorkTimer(),
                new BreakTimer());

            _workflowTimer.Tick += WorkflowTimer_Tick;
            _workflowTimer.SessionCompleted += WorkflowTimer_SessionCompleted;
            _workflowTimer.WorkCompleted += WorkflowTimer_WorkCompleted;

            SnoozeReminderCommand = new RelayCommand(_focusReminder.Snooze);
            DismissReminderCommand = new RelayCommand(_focusReminder.Dismiss);
            CancelReminderCommand = new RelayCommand(_focusReminder.Cancel);
            _focusReminder.StateChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsFocusReminderVisible));
                OnPropertyChanged(nameof(IsFocusReminderAttentionActive));
                OnPropertyChanged(nameof(IsFocusReminderBlinkOn));
                OnPropertyChanged(nameof(FocusReminderIntervalText));
            };
            _focusReminder.ReminderDue += (_, _) =>
                FocusReminderRequested?.Invoke(this, EventArgs.Empty);
            ConfigureFocusReminder();

            // Persist unfinished work if Windows/the user closes the app normally.
            if (Application.Current != null)
                Application.Current.Exit += (_, _) => RecordWorkIfNeeded(false);

            _schedulerService = new SchedulerService(
                new LauncherService());

            _overlayService = new OverlayService();
            _overlayService.ConfigurePosition(CurrentSettings.OverlayPosition,
                CurrentSettings.OverlayLeft, CurrentSettings.OverlayTop);
            _overlayService.PositionDragged += (_, point) => SaveOverlayPosition(point);
            _overlayService.PauseRequested += (_, _) => PauseTimer();
            _overlayService.ResumeRequested += (_, _) => ResumeTimer();

            _schedulerService.ActionTriggered += SchedulerService_ActionTriggered;

            _presetService = new PresetService(
    new AppDataPathService());

            _historyService = new HistoryService(new AppDataPathService());
            ClearPersonalBestCommand = new RelayCommand(ClearPersonalBest);
            RefreshHistory();

            var savedPresets = _presetService.LoadPresets();

            if (savedPresets.Count == 0)
            {
                savedPresets.Add(new Preset
                {
                    Name = "Default",
                    Session = new TimerSession
                    {
                        Label = "Work",
                        WorkDuration = TimeSpan.FromMinutes(25),
                        BreakDuration = TimeSpan.FromMinutes(5),
                        Actions = new List<ScheduledAction>()
                    }
                });

                _presetService.SavePresets(savedPresets);
            }

            // Normalize existing presets to chronological action order once on load.
            bool reorderedOnLoad = false;
            foreach (var preset in savedPresets)
            {
                var original = preset.Session.Actions;
                var sorted = SortHelper.ByOffset(original);

                if (!original.SequenceEqual(sorted))
                {
                    preset.Session.Actions = sorted;
                    reorderedOnLoad = true;
                }
            }

            if (reorderedOnLoad)
                _presetService.SavePresets(savedPresets);

            Presets = new ObservableCollection<Preset>(savedPresets);

            ScheduledActions =
                new ObservableCollection<ScheduledActionViewModel>();

            SelectedPreset = Presets[0];

            TimerCommands = new TimerCommands(
                StartTimer,
                PauseTimer,
                ResumeTimer,
                StopTimer,
                RepeatTimer,
                ExtendTimer,
                () => CanStartTimer && CanRunSelectedPreset,
                () => CanRepeatTimer && CanRunSelectedPreset);

            PresetCommands = new PresetCommands(
                LoadPreset,
                SavePreset,
                CreatePreset,
                DeletePreset);

            ToggleScheduledActionEnabledCommand = new RelayCommand(ToggleScheduledActionEnabled);
            BrowseActionTargetCommand = new RelayCommand(BrowseActionTarget);
            AddScheduledActionCommand = new RelayCommand(AddScheduledAction);
            UpdateScheduledActionCommand = new RelayCommand(UpdateScheduledAction);
            RemoveScheduledActionCommand = new RelayCommand(RemoveScheduledAction);
            ClearActionEditorCommand = new RelayCommand(() =>
            {
                SelectedScheduledAction = null;
                ClearActionFields();
            });
        }

        public void SaveSettings(Settings settings)
        {
            if (!CanEditSettings)
                throw new InvalidOperationException("Stop the timer before changing settings.");

            SettingsService.Validate(settings);
            // Apply the explicit user choice only when Save is clicked.
            var restoreStartup = _startupService.Apply(settings.StartWithWindows);
            try
            {
                _settingsService.Save(settings);
            }
            catch (Exception saveError)
            {
                try
                {
                    restoreStartup();
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException(
                        "Could not save settings or restore the previous Windows startup entry. "
                        + saveError.Message + " Restore error: " + restoreError.Message,
                        restoreError);
                }
                throw;
            }
            CurrentSettings = settings.Copy();
            SettingsLoadWarning = null;
            _overlayService.ConfigurePosition(CurrentSettings.OverlayPosition,
                CurrentSettings.OverlayLeft, CurrentSettings.OverlayTop);
            OnPropertyChanged(nameof(CurrentSettings));
            ConfigureFocusReminder();
        }

        private void ConfigureFocusReminder()
        {
            _focusReminder.Configure(CurrentSettings.FocusGrabberEnabled,
                CurrentSettings.FocusGrabberIntervalMinutes, CurrentSettings.SnoozeMinutes);
            OnPropertyChanged(nameof(SnoozeReminderText));
            OnPropertyChanged(nameof(FocusReminderIntervalText));
        }

        public void ShutdownFocusReminders() => _focusReminder.Dispose();

        private void SaveOverlayPosition(Point point)
        {
            if (CurrentSettings.OverlayPosition != OverlayPosition.FreeDrag)
                return;

            // Dragging is allowed during a session, unlike editing other settings.
            // Write only the position, without touching Windows startup registration.
            var candidate = CurrentSettings.Copy();
            candidate.OverlayLeft = point.X;
            candidate.OverlayTop = point.Y;
            try
            {
                _settingsService.Save(candidate);
                CurrentSettings = candidate;
                OnPropertyChanged(nameof(CurrentSettings));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the widget position. It will remain moved for this run.\n\n"
                    + ex.Message, "Workflow Timer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void ExecuteHotkey(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.StartTimer:
                    TimerCommands.StartCommand.Execute(null);
                    break;
                case HotkeyAction.StopTimer:
                    TimerCommands.StopCommand.Execute(null);
                    break;
                case HotkeyAction.PauseResume:
                    if (CanResumeTimer)
                        TimerCommands.ResumeCommand.Execute(null);
                    else if (CanPauseTimer)
                        TimerCommands.PauseCommand.Execute(null);
                    break;
                case HotkeyAction.RepeatTimer:
                    TimerCommands.RepeatCommand.Execute(null);
                    break;
                case HotkeyAction.Extend5:
                    TimerCommands.ExtendCommand.Execute(5);
                    break;
                case HotkeyAction.Extend10:
                    TimerCommands.ExtendCommand.Execute(10);
                    break;
                case HotkeyAction.Extend30:
                    TimerCommands.ExtendCommand.Execute(30);
                    break;
                case HotkeyAction.Extend60:
                    TimerCommands.ExtendCommand.Execute(60);
                    break;
                case HotkeyAction.NextPreset:
                    SelectAdjacentPreset(1);
                    break;
                case HotkeyAction.PreviousPreset:
                    SelectAdjacentPreset(-1);
                    break;
                case HotkeyAction.CreatePreset:
                    if (CanEditSettings)
                        PresetCommands.CreatePresetCommand.Execute(null);
                    break;
            }
        }

        private void SelectAdjacentPreset(int direction)
        {
            if (!CanEditSettings || Presets.Count == 0)
                return;
            int index = SelectedPreset == null ? -1 : Presets.IndexOf(SelectedPreset);
            SelectedPreset = index < 0 ? Presets[0]
                : Presets[(index + direction + Presets.Count) % Presets.Count];
        }

        private void StartTimer()
        {
            if (!CanStartTimer || !CanRunSelectedPreset)
                return;

            if (SelectedPreset == null)
                return;

            if (_workflowTimer.IsRunning)
                return;

            _focusReminder.Cancel();
            ResetScheduledActions();

            _workStartedAt = DateTime.Now;
            _workRecorded = false;

            _schedulerService.LoadActions(
                SelectedPreset.Session.Actions);

            _workflowTimer.Start(SelectedPreset.Session);

            UpdateOverlay();

            _overlayService.Show();

            NotifyTimerProperties();
        }

        private void PauseTimer()
        {
            if (!CanPauseTimer)
                return;

            _workflowTimer.Pause();
            UpdateOverlay();

            NotifyTimerProperties();
        }

        private void ResumeTimer()
        {
            if (!CanResumeTimer)
                return;

            _workflowTimer.Resume();
            UpdateOverlay();

            NotifyTimerProperties();
        }

        private void StopTimer()
        {
            if (!HasActiveTimer)
                return;

            _focusReminder.Cancel();
            RecordWorkIfNeeded(false);
            _workflowTimer.Stop();
            _schedulerService.Reset();
            _overlayService.Hide();

            ResetScheduledActions();

            NotifyTimerProperties();
        }

        private void RepeatTimer()
        {
            if (!CanRepeatTimer || !CanRunSelectedPreset)
                return;

            // After completion the selected preset may have changed.
            if (State == SessionState.Completed)
            {
                StartTimer();
                return;
            }

            _focusReminder.Cancel();
            // Repeat discards the old countdown, so persist its work first.
            RecordWorkIfNeeded(false);
            ResetScheduledActions();

            if (SelectedPreset != null)
            {
                _schedulerService.LoadActions(
                    SelectedPreset.Session.Actions);
            }

            _workflowTimer.Repeat();

            if (_workflowTimer.State == SessionState.Working)
            {
                _workStartedAt = DateTime.Now;
                _workRecorded = false;
            }

            UpdateOverlay();

            _overlayService.Show();

            NotifyTimerProperties();
        }

        private void ExtendTimer(object? value)
        {
            if (!CanExtendTimer)
                return;

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

        private void UpdateOverlay()
        {
            TimeSpan remaining = _workflowTimer.RemainingTime;
            string? nextName = null;
            TimeSpan? nextRemaining = null;
            if (_workflowTimer.State == SessionState.Working ||
                (_workflowTimer.State == SessionState.Paused &&
                 _workflowTimer.CurrentSession != null &&
                 !_workRecorded))
            {
                var next = _schedulerService.GetNextPendingAction();
                if (next != null)
                {
                    nextName = next.Name;
                    nextRemaining = next.OffsetFromStart - _workflowTimer.WorkElapsedTime;
                    if (nextRemaining < TimeSpan.Zero)
                        nextRemaining = TimeSpan.Zero;
                }
            }
            _overlayService.Update(_workflowTimer.State.ToString(),
                remaining, nextName, nextRemaining);
        }

        private void WorkflowTimer_Tick(
    object? sender,
    EventArgs e)
        {
            _schedulerService.Check(
                _workflowTimer.WorkElapsedTime);

            UpdateOverlay();

            NotifyTimerProperties();
        }

        private void WorkflowTimer_WorkCompleted(object? sender, EventArgs e)
        {
            RecordWorkIfNeeded(true);
        }

        private void RecordWorkIfNeeded(bool completed)
        {
            if (_workRecorded || _workStartedAt == default)
                return;

            // During breaks, WorkElapsedTime remains at the completed value.
            // Mark attempts as recorded even when they contain no work.
            TimeSpan elapsed = _workflowTimer.WorkElapsedTime;
            if (elapsed <= TimeSpan.Zero)
            {
                _workRecorded = true;
                return;
            }

            var entry = new HistoryEntry
            {
                StartedAt = _workStartedAt,
                EndedAt = DateTime.Now,
                PresetName = SelectedPreset?.Name ??
                    _workflowTimer.CurrentSession?.Label ?? "Work",
                WorkDuration = elapsed,
                Completed = completed
            };

            // Only mark as saved after persistence succeeds.
            _historyService.AddSession(entry);
            _workRecorded = true;
            RefreshHistory();
        }

        private void WorkflowTimer_SessionCompleted(
    object? sender,
    EventArgs e)
        {
            UpdateOverlay();

            _overlayService.Blink();

            NotifyTimerProperties();
            _focusReminder.Begin();
        }

        private void SchedulerService_ActionTriggered(
            object? sender,
            ScheduledAction action)
        {
            var actionViewModel =
                FindScheduledActionViewModel(action);

            if (actionViewModel == null)
                return;

            actionViewModel.MarkTriggered();

            OnPropertyChanged(nameof(ScheduledActions));
        }

        private void SortSelectedPresetActions()
        {
            if (SelectedPreset == null)
                return;

            SelectedPreset.Session.Actions = SortHelper.ByOffset(
                SelectedPreset.Session.Actions);
        }

        private void RefreshScheduledActions()
        {
            ScheduledActions.Clear();

            if (SelectedPreset == null)
                return;

            foreach (var action in SelectedPreset.Session.Actions)
            {
                ScheduledActions.Add(
                    new ScheduledActionViewModel(action));
            }

            OnPropertyChanged(nameof(ScheduledActions));
        }

        private void ResetScheduledActions()
        {
            foreach (var action in ScheduledActions)
            {
                action.Reset();
            }

            OnPropertyChanged(nameof(ScheduledActions));
        }

        private ScheduledActionViewModel? FindScheduledActionViewModel(
            ScheduledAction action)
        {
            foreach (var item in ScheduledActions)
            {
                if (ReferenceEquals(item.Action, action))
                    return item;
            }

            return null;
        }

        private void LoadPreset()
        {
            if (!CanEditSettings || SelectedPreset == null)
                return;

            StopTimer();
            RefreshScheduledActions();
        }

        private void SavePreset()
        {
            if (!CanEditSettings)
                return;

            TrySavePresetDraft();
        }

        private void PersistPresets() => _presetService.SavePresets(Presets.ToList());

        private static string FormatPresetDuration(TimeSpan duration) =>
            $"{(long)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";

        private static bool TryParsePresetDuration(string text, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;
            var parts = text.Trim().Split(':');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int hours) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) ||
                minutes > 59 || seconds > 59)
                return false;
            try
            {
                duration = new TimeSpan(hours, minutes, seconds);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private void NotifyPresetDraft()
        {
            OnPropertyChanged(nameof(HasUnsavedPresetChanges));
            TimerCommands?.RefreshAvailability();
        }

        private void ResetPresetDraft()
        {
            if (SelectedPreset is not Preset preset)
                return;
            _presetNameDraft = preset.Name;
            _presetWorkDraft = FormatPresetDuration(preset.Session.WorkDuration);
            _presetBreakDraft = FormatPresetDuration(preset.Session.BreakDuration);
            OnPropertyChanged(nameof(PresetNameDraft));
            OnPropertyChanged(nameof(PresetWorkDraft));
            OnPropertyChanged(nameof(PresetBreakDraft));
            NotifyPresetDraft();
        }

        private bool TrySavePresetDraft()
        {
            if (SelectedPreset is not Preset preset)
                return false;

            if (string.IsNullOrWhiteSpace(PresetNameDraft) ||
                !TryParsePresetDuration(PresetWorkDraft, out var work) || work <= TimeSpan.Zero ||
                !TryParsePresetDuration(PresetBreakDraft, out var pause))
            {
                MessageBox.Show("Enter a preset name, a positive work duration and a non-negative break duration (HH:MM:SS).",
                    "Invalid preset", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (preset.Session.Actions.Any(action => action.OffsetFromStart > work))
            {
                MessageBox.Show("A scheduled action has an offset beyond the new work duration. Adjust or remove that action first.",
                    "Invalid preset", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var oldName = preset.Name;
            var oldWork = preset.Session.WorkDuration;
            var oldBreak = preset.Session.BreakDuration;
            preset.Name = PresetNameDraft.Trim();
            preset.Session.WorkDuration = work;
            preset.Session.BreakDuration = pause;
            try
            {
                PersistPresets();
            }
            catch (Exception ex)
            {
                preset.Name = oldName;
                preset.Session.WorkDuration = oldWork;
                preset.Session.BreakDuration = oldBreak;
                NotifyPresetDraft();
                MessageBox.Show("Could not save the preset: " + ex.Message,
                    "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            ResetPresetDraft();
            return true;
        }

        // Used by preset selection, creation, deletion and window closing.
        public bool ResolvePendingPresetEdits()
        {
            if (_resolvingPresetEdits)
                return false;
            if (!HasUnsavedPresetChanges)
                return true;

            _resolvingPresetEdits = true;
            NotifyPresetDraft();
            try
            {
                var result = MessageBox.Show(Application.Current.MainWindow,
                    "Save changes to the current preset?\n\nYes: Save\nNo: Discard\nCancel: Keep editing",
                    "Unsaved preset changes", MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question, MessageBoxResult.Cancel);
                if (result == MessageBoxResult.Yes)
                    return TrySavePresetDraft();
                if (result == MessageBoxResult.No)
                {
                    ResetPresetDraft();
                    return true;
                }
                return false;
            }
            finally
            {
                _resolvingPresetEdits = false;
                NotifyPresetDraft();
            }
        }

        private void CreatePreset()
        {
            if (!CanEditSettings || !ResolvePendingPresetEdits())
                return;

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
            SelectPreset(preset);
            PersistPresets();
        }

        private void DeletePreset()
        {
            if (!CanEditSettings || SelectedPreset == null)
                return;

            if (Presets.Count <= 1)
                return;

            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to delete the preset \"{SelectedPreset.Name}\"?",
                "Confirm Preset Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            if (!ResolvePendingPresetEdits())
                return;

            int index = Presets.IndexOf(SelectedPreset);

            var deleted = SelectedPreset;
            SelectPreset(Presets[index == Presets.Count - 1 ? index - 1 : index + 1]);
            Presets.Remove(deleted);

            PersistPresets();
        }

        private bool CanEditActions()
        {
            if (SelectedPreset == null)
                return false;

            return CanEditSettings;
        }

        private void ClearActionFields()
        {
            ActionName = string.Empty;
            SelectedActionType = ActionType.Website;
            ActionTarget = string.Empty;
            ActionOffsetText = "00:00:00";
            ActionEnabled = true;
        }

        private void BrowseActionTarget()
        {
            if (!CanEditActions() || !CanBrowseActionTarget)
                return;

            if (SelectedActionType == ActionType.Folder)
            {
                var folderDialog = new OpenFolderDialog
                {
                    Title = "Select a folder",
                    Multiselect = false
                };

                if (Directory.Exists(ActionTarget))
                    folderDialog.InitialDirectory = ActionTarget;

                if (folderDialog.ShowDialog() == true)
                    ActionTarget = folderDialog.FolderName;

                return;
            }

            var fileDialog = new OpenFileDialog
            {
                Title = SelectedActionType == ActionType.Executable
                    ? "Select an executable"
                    : "Select a file",
                Filter = SelectedActionType == ActionType.Executable
                    ? "Executable files (*.exe)|*.exe|All files (*.*)|*.*"
                    : "All files (*.*)|*.*",
                Multiselect = false,
                CheckFileExists = true
            };

            if (File.Exists(ActionTarget))
                fileDialog.InitialDirectory = Path.GetDirectoryName(ActionTarget);

            if (fileDialog.ShowDialog() == true)
                ActionTarget = fileDialog.FileName;
        }

        private static bool TryNormalizeWebsite(string input, out string normalized)
        {
            normalized = string.Empty;
            string candidate = input.Trim();

            if (string.IsNullOrWhiteSpace(candidate) ||
                candidate.Any(char.IsWhiteSpace))
                return false;

            if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Do not transform another explicit scheme into an HTTPS address.
                if (candidate.Contains("://") || candidate.Contains('@'))
                    return false;

                candidate = "https://" + candidate;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrWhiteSpace(uri.Host) ||
                !string.IsNullOrEmpty(uri.UserInfo))
                return false;

            // Allow local development hosts, IPv4/IPv6 addresses and normal DNS domains.
            bool validHost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                             uri.Host.Contains('.') ||
                             System.Net.IPAddress.TryParse(uri.Host, out _);

            if (!validHost)
                return false;

            normalized = candidate;
            return true;
        }

        private bool TryReadActionFields(out TimeSpan offset, out string normalizedTarget)
        {
            offset = TimeSpan.Zero;
            normalizedTarget = string.Empty;

            if (string.IsNullOrWhiteSpace(ActionName) ||
                string.IsNullOrWhiteSpace(ActionTarget))
            {
                MessageBox.Show("Enter both a name and a target.", "Invalid Action");
                return false;
            }

            if (!TimeSpan.TryParse(ActionOffsetText, CultureInfo.InvariantCulture, out offset) ||
                offset < TimeSpan.Zero ||
                offset > SelectedPreset!.Session.WorkDuration)
            {
                MessageBox.Show(
                    "Enter a valid, non-negative offset within the work duration (for example 00:00:05).",
                    "Invalid Offset");
                return false;
            }

            string target = ActionTarget.Trim();
            bool validTarget;

            if (SelectedActionType == ActionType.Website)
            {
                validTarget = TryNormalizeWebsite(target, out normalizedTarget);
            }
            else
            {
                normalizedTarget = target;
                validTarget = SelectedActionType switch
                {
                    ActionType.Executable or ActionType.File => File.Exists(target),
                    ActionType.Folder => Directory.Exists(target),
                    _ => false
                };
            }

            if (!validTarget)
            {
                MessageBox.Show(
                    "Enter a valid website address or select an existing file/folder path.",
                    "Invalid Target");
                return false;
            }

            return true;
        }

        private void AddScheduledAction()
        {
            if (!CanEditActions() || !TryReadActionFields(out var offset, out var target))
                return;

            var action = new ScheduledAction
            {
                Name = ActionName.Trim(),
                ActionType = SelectedActionType,
                Target = target,
                OffsetFromStart = offset,
                Enabled = ActionEnabled
            };

            SelectedPreset!.Session.Actions.Add(action);
            SortSelectedPresetActions();
            PersistPresets();
            RefreshScheduledActions();
            SelectedScheduledAction = ScheduledActions.First(x => ReferenceEquals(x.Action, action));
        }

        private void ToggleScheduledActionEnabled(object? value)
        {
            if (value is not ScheduledActionViewModel item)
                return;

            if (!CanEditSettings)
                return;

            if (SelectedPreset == null ||
                !SelectedPreset.Session.Actions.Contains(item.Action))
                return;

            var action = item.Action;
            action.Enabled = !action.Enabled;

            PersistPresets();
            RefreshScheduledActions();

            // Restore selection and sync the editor checkbox to the saved value.
            SelectedScheduledAction = ScheduledActions.First(
                x => ReferenceEquals(x.Action, action));
        }

        private void UpdateScheduledAction()
        {
            if (!CanEditActions() || SelectedScheduledAction == null ||
                !TryReadActionFields(out var offset, out var target))
                return;

            var action = SelectedScheduledAction.Action;
            action.Name = ActionName.Trim();
            action.ActionType = SelectedActionType;
            action.Target = target;
            action.OffsetFromStart = offset;
            action.Enabled = ActionEnabled;

            SortSelectedPresetActions();
            PersistPresets();
            RefreshScheduledActions();
            SelectedScheduledAction = ScheduledActions.First(x => ReferenceEquals(x.Action, action));
        }

        private void RemoveScheduledAction()
        {
            if (!CanEditActions() || SelectedScheduledAction == null)
                return;

            var action = SelectedScheduledAction.Action;
            var confirmation = MessageBox.Show(
                $"Remove scheduled action \"{action.Name}\"?",
                "Confirm Action Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirmation != MessageBoxResult.Yes)
                return;

            SelectedPreset!.Session.Actions.Remove(action);
            SortSelectedPresetActions();
            PersistPresets();
            RefreshScheduledActions();
            SelectedScheduledAction = null;
        }

        private static string FormatWork(TimeSpan duration)
        {
            // Show seconds for short test sessions; show total hours rather than wrapping at 24h.
            long hours = (long)duration.TotalHours;
            if (hours > 0)
                return $"{hours}h {duration.Minutes}m {duration.Seconds}s";
            if (duration.Minutes > 0)
                return $"{duration.Minutes}m {duration.Seconds}s";
            return $"{duration.Seconds}s";
        }

        private void RefreshHistory()
        {
            var entries = _historyService.LoadHistory();
            var today = DateOnly.FromDateTime(DateTime.Now);

            TodayWorkText = FormatWork(TimeSpan.FromTicks(entries
                .Where(e => DateOnly.FromDateTime(e.StartedAt) == today)
                .Sum(e => e.WorkDuration.Ticks)));
            AllTimeWorkText = FormatWork(TimeSpan.FromTicks(entries.Sum(e => e.WorkDuration.Ticks)));

            var best = _historyService.GetBestDay();
            BestDayText = best.HasValue ? FormatWork(best.Value.Total) : "—";
            BestDayDateText = best.HasValue
                ? best.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "No record yet";

            int excludedCount = _historyService.GetExcludedRecordDayCount();
            RecordResetText = excludedCount > 0
                ? $"{excludedCount} day(s) excluded from personal-best rankings"
                : "All-time best day";

            DailyHistory.Clear();
            foreach (var day in entries
                .GroupBy(e => DateOnly.FromDateTime(e.StartedAt))
                .OrderByDescending(g => g.Key))
            {
                DailyHistory.Add(new HistoryDisplayRow
                {
                    Title = day.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Detail = $"{day.Count()} session(s)",
                    Duration = FormatWork(TimeSpan.FromTicks(day.Sum(e => e.WorkDuration.Ticks)))
                });
            }

            RecentHistory.Clear();
            foreach (var entry in entries.OrderByDescending(e => e.StartedAt))
            {
                RecentHistory.Add(new HistoryDisplayRow
                {
                    Title = string.IsNullOrWhiteSpace(entry.PresetName) ? "Work" : entry.PresetName,
                    Detail = $"{entry.StartedAt:yyyy-MM-dd HH:mm} · {(entry.Completed ? "Completed" : "Stopped")}",
                    Duration = FormatWork(entry.WorkDuration)
                });
            }
        }

        private void ClearPersonalBest()
        {
            var currentBest = _historyService.GetBestDay();
            if (!currentBest.HasValue)
            {
                MessageBox.Show(
                    "There is no remaining personal-best day to clear.",
                    "No Personal Best",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Exclude {currentBest.Value.Date:yyyy-MM-dd} " +
                $"({FormatWork(currentBest.Value.Total)}) from personal-best rankings?\n\n" +
                "The next-highest eligible day will become your record. " +
                "Daily totals, session history, and all recorded work remain unchanged.",
                "Clear Personal Best",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            if (_historyService.ClearPersonalBest())
                RefreshHistory();
        }

        private void NotifyTimerProperties()
        {
            OnPropertyChanged(nameof(RemainingTime));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(CanEditSettings));
            OnPropertyChanged(nameof(CanStartTimer));
            OnPropertyChanged(nameof(HasActiveTimer));
            OnPropertyChanged(nameof(CanPauseTimer));
            OnPropertyChanged(nameof(CanResumeTimer));
            OnPropertyChanged(nameof(CanRepeatTimer));
            OnPropertyChanged(nameof(CanExtendTimer));
            OnPropertyChanged(nameof(RemainingTimeText));
            TimerCommands?.RefreshAvailability();
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
