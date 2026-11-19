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
using Workflow_Timer.Services.Motivation;
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
        private readonly NotificationService _notifications = new();
        private bool _suppressInitialReminderSound;
        public TodoViewModel Todo { get; }
        private readonly MotivationService _motivationService = new();
        public string MotivationQuoteText { get; private set; } = string.Empty;
        public ICommand NextMotivationQuoteCommand { get; }

        private void NextMotivationQuote()
        {
            MotivationQuoteText = _motivationService.GetRandomQuote();
            OnPropertyChanged(nameof(MotivationQuoteText));
        }
        public Settings CurrentSettings { get; private set; } = new();
        public ICommand ToggleThemeCommand { get; }
        public string ThemeToggleText => CurrentSettings.Theme == Workflow_Timer.Enums.ThemeMode.Dark
            ? "Light mode" : "Dark mode";
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
        private readonly global::System.Windows.Threading.DispatcherTimer _presetSaveTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        private string? _presetSaveError;

        public string PresetNameDraft
        {
            get => _presetNameDraft;
            set { if (_presetNameDraft == value) return; _presetNameDraft = value; QueuePresetSave(); }
        }

        public string PresetWorkDraft
        {
            get => _presetWorkDraft;
            set { if (_presetWorkDraft == value) return; _presetWorkDraft = value; QueuePresetSave(); }
        }

        public string PresetBreakDraft
        {
            get => _presetBreakDraft;
            set { if (_presetBreakDraft == value) return; _presetBreakDraft = value; QueuePresetSave(); }
        }

        public bool HasUnsavedPresetChanges => SelectedPreset is Preset preset &&
            (PresetNameDraft.Trim() != preset.Name ||
             !TryParsePresetDuration(PresetWorkDraft, out var work) || work != preset.Session.WorkDuration ||
             !TryParsePresetDuration(PresetBreakDraft, out var pause) || pause != preset.Session.BreakDuration);

        public string? PresetEditError => GetPresetValidationError() ?? _presetSaveError;
        public bool HasPresetEditError => PresetEditError != null;

        private bool CanRunSelectedPreset => SelectedPreset != null &&
            !HasPresetEditError && !_resolvingPresetEdits &&
            !HasUnsavedActionChanges && !_resolvingActionEdits;
        private bool _resolvingActionEdits;
        public bool HasUnsavedActionChanges => ScheduledActions.Any(row => row.HasPendingChanges);

        private void NotifyActionDraft()
        {
            OnPropertyChanged(nameof(HasUnsavedActionChanges));
            TimerCommands?.RefreshAvailability();
        }

        public ICommand AddScheduledActionCommand { get; }
        public ICommand RemoveScheduledActionCommand { get; }
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
        public ICommand ClearOldRecentSessionsCommand { get; }
        private string _recentSessionAgeDays = "30";
        public string RecentSessionAgeDays
        {
            get => _recentSessionAgeDays;
            set { _recentSessionAgeDays = value; OnPropertyChanged(); }
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

                if (value == null || !CanEditSettings || !ResolvePendingEdits())
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

            OnPropertyChanged(nameof(SelectedPreset));
            RememberSelectedPreset();
        }

        private void RememberSelectedPreset()
        {
            if (_selectedPreset == null) return;
            try { _presetService.SaveSelectedPresetId(_selectedPreset.Id); }
            catch (Exception ex)
            {
                MessageBox.Show("Could not remember the selected preset.\n\n" + ex.Message,
                    "Workflow Timer", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
            _presetSaveTimer.Tick += (_, _) =>
            {
                _presetSaveTimer.Stop();
                if (CanEditSettings && !_resolvingPresetEdits && HasUnsavedPresetChanges)
                    TrySavePresetDraft();
            };
            Todo = new TodoViewModel(new TodoService(new AppDataPathService()));
            NextMotivationQuoteCommand = new RelayCommand(NextMotivationQuote);
            NextMotivationQuote();
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

            ThemeService.Apply(CurrentSettings.Theme);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);

            _workflowTimer = new WorkflowTimer(
                new WorkTimer(),
                new BreakTimer());

            _workflowTimer.Tick += WorkflowTimer_Tick;
            _workflowTimer.SessionCompleted += WorkflowTimer_SessionCompleted;
            _workflowTimer.WorkCompleted += WorkflowTimer_WorkCompleted;
            _workflowTimer.BreakCompleted += (_, _) =>
                _notifications.Notify(NotificationType.BreakCompleted, CurrentSettings);

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
            {
                if (!_suppressInitialReminderSound)
                    _notifications.Notify(NotificationType.FocusReminder, CurrentSettings);
                FocusReminderRequested?.Invoke(this, EventArgs.Empty);
            };
            ConfigureFocusReminder();

            // Persist unfinished work if Windows/the user closes the app normally.
            if (Application.Current != null)
                Application.Current.Exit += (_, _) =>
                {
                    if (!DataManagementService.ResetInProgress) RecordWorkIfNeeded(false);
                };

            _schedulerService = new SchedulerService(
                new LauncherService());

            _overlayService = new OverlayService();
            _overlayService.ConfigurePosition(CurrentSettings.OverlayPosition,
                CurrentSettings.OverlayLeft, CurrentSettings.OverlayTop,
                CurrentSettings.HideWidget, CurrentSettings.OverlayCompact);
            _overlayService.PositionDragged += (_, point) => SaveOverlayPosition(point);
            _overlayService.CompactChanged += (_, compact) => SaveOverlayCompact(compact);
            _overlayService.PauseRequested += (_, _) => PauseTimer();
            _overlayService.ResumeRequested += (_, _) => ResumeTimer();

            _schedulerService.ActionTriggered += SchedulerService_ActionTriggered;

            _presetService = new PresetService(
    new AppDataPathService());

            _historyService = new HistoryService(new AppDataPathService());
            ClearPersonalBestCommand = new RelayCommand(ClearPersonalBest);
            ClearOldRecentSessionsCommand = new RelayCommand(ClearOldRecentSessions);
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
            var ids = new HashSet<Guid>();
            foreach (var preset in savedPresets)
            {
                if (preset.Id == Guid.Empty || !ids.Add(preset.Id))
                {
                    do { preset.Id = Guid.NewGuid(); } while (!ids.Add(preset.Id));
                    reorderedOnLoad = true;
                }
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

            var rememberedId = _presetService.LoadSelectedPresetId();
            SelectedPreset = Presets.FirstOrDefault(p => p.Id == rememberedId) ?? Presets[0];

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
                CreatePreset,
                DeletePreset);

            BrowseActionTargetCommand = new RelayCommand(BrowseActionTarget);
            AddScheduledActionCommand = new RelayCommand(AddScheduledAction);
            RemoveScheduledActionCommand = new RelayCommand(RemoveScheduledAction);
        }

        public void SaveMainWindowState(bool maximized)
        {
            // Do not overwrite unreadable settings or recreate data during a reset.
            if (SettingsLoadWarning != null || DataManagementService.ResetInProgress ||
                CurrentSettings.MainWindowMaximized == maximized) return;

            var candidate = CurrentSettings.Copy();
            candidate.MainWindowMaximized = maximized;
            _settingsService.Save(candidate);
            CurrentSettings = candidate;
        }

        public void SaveTutorialPreference(bool neverShow)
        {
            if (SettingsLoadWarning != null)
                throw new InvalidOperationException("Resolve the settings load warning before saving preferences.");
            if (CurrentSettings.NeverShowTutorial == neverShow) return;
            var candidate = CurrentSettings.Copy();
            candidate.NeverShowTutorial = neverShow;
            _settingsService.Save(candidate);
            CurrentSettings = candidate;
            OnPropertyChanged(nameof(CurrentSettings));
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
            ThemeService.Apply(CurrentSettings.Theme);
            SettingsLoadWarning = null;
            _overlayService.ConfigurePosition(CurrentSettings.OverlayPosition,
                CurrentSettings.OverlayLeft, CurrentSettings.OverlayTop,
                CurrentSettings.HideWidget, CurrentSettings.OverlayCompact);
            OnPropertyChanged(nameof(CurrentSettings));
            OnPropertyChanged(nameof(ThemeToggleText));
            ConfigureFocusReminder();
        }

        private void ToggleTheme()
        {
            var previous = CurrentSettings.Theme;
            var candidate = CurrentSettings.Copy();
            candidate.Theme = previous == Workflow_Timer.Enums.ThemeMode.Dark
                ? Workflow_Timer.Enums.ThemeMode.Light : Workflow_Timer.Enums.ThemeMode.Dark;
            if (SettingsLoadWarning != null && MessageBox.Show(
                "The existing settings could not be loaded. Saving the theme will replace the settings file. Continue?",
                "Workflow Timer", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            try
            {
                ThemeService.Apply(candidate.Theme);
                _settingsService.Save(candidate);
            }
            catch (Exception ex)
            {
                ThemeService.Apply(previous);
                MessageBox.Show("Could not save the theme: " + ex.Message, "Workflow Timer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            CurrentSettings = candidate;
            SettingsLoadWarning = null;
            OnPropertyChanged(nameof(CurrentSettings));
            OnPropertyChanged(nameof(ThemeToggleText));
        }

        private void ConfigureFocusReminder()
        {
            _focusReminder.Configure(CurrentSettings.FocusGrabberEnabled,
                CurrentSettings.FocusGrabberIntervalMinutes, CurrentSettings.SnoozeMinutes);
            OnPropertyChanged(nameof(SnoozeReminderText));
            OnPropertyChanged(nameof(FocusReminderIntervalText));
        }

        public void ShutdownFocusReminders() => _focusReminder.Dispose();

        private void SaveOverlayCompact(bool compact)
        {
            var candidate = CurrentSettings.Copy();
            candidate.OverlayCompact = compact;
            try
            {
                _settingsService.Save(candidate);
                CurrentSettings = candidate;
                OnPropertyChanged(nameof(CurrentSettings));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save widget size: " + ex.Message, "Workflow Timer",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

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

            if (!FlushPresetSave()) return;

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
            NextMotivationQuote();

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

            if (!FlushPresetSave()) return;

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
                NextMotivationQuote();
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
            _notifications.Notify(NotificationType.WorkCompleted, CurrentSettings);
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
            NextMotivationQuote();
            UpdateOverlay();

            _overlayService.Blink();

            NotifyTimerProperties();
            // Avoid a duplicate beep when the first reminder coincides with completion.
            _suppressInitialReminderSound = _workflowTimer.CurrentSession?.BreakDuration == TimeSpan.Zero
                ? CurrentSettings.WorkCompletionSoundEnabled
                : CurrentSettings.BreakCompletionSoundEnabled;
            try
            {
                _focusReminder.Begin();
            }
            finally
            {
                _suppressInitialReminderSound = false;
            }
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
            if (SelectedPreset != null)
                foreach (var action in SelectedPreset.Session.Actions)
                    ScheduledActions.Add(new ScheduledActionViewModel(action, AutoSaveAction));
            OnPropertyChanged(nameof(ScheduledActions));
            NotifyActionDraft();
        }

        public void SortActionRows()
        {
            var ordered = ScheduledActions.OrderBy(row => row.Action.OffsetFromStart).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                int previous = ScheduledActions.IndexOf(ordered[i]);
                if (previous != i) ScheduledActions.Move(previous, i);
            }
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
            if (!CanEditSettings || SelectedPreset == null || !ResolvePendingEdits())
                return;

            StopTimer();
            RefreshScheduledActions();
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
            OnPropertyChanged(nameof(PresetEditError));
            OnPropertyChanged(nameof(HasPresetEditError));
            TimerCommands?.RefreshAvailability();
        }

        private void QueuePresetSave()
        {
            _presetSaveTimer.Stop();
            _presetSaveError = null;
            if (CanEditSettings && !_resolvingPresetEdits && HasUnsavedPresetChanges && !HasPresetEditError)
                _presetSaveTimer.Start();
            NotifyPresetDraft();
        }

        private bool FlushPresetSave()
        {
            _presetSaveTimer.Stop();
            return !HasUnsavedPresetChanges || (CanEditSettings && TrySavePresetDraft());
        }

        private void ResetPresetDraft()
        {
            _presetSaveTimer.Stop();
            _presetSaveError = null;
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

        private string? GetPresetValidationError()
        {
            if (SelectedPreset is not Preset preset)
                return null;

            if (string.IsNullOrWhiteSpace(PresetNameDraft) ||
                !TryParsePresetDuration(PresetWorkDraft, out var work) || work <= TimeSpan.Zero ||
                !TryParsePresetDuration(PresetBreakDraft, out _))
                return "Enter a preset name, a positive work duration and a valid break duration.";
            if (preset.Session.Actions.Any(action => action.OffsetFromStart > work))
                return "A scheduled action exceeds the work duration. Adjust or remove it first.";
            return null;
        }

        private bool TrySavePresetDraft()
        {
            if (SelectedPreset is not Preset preset || GetPresetValidationError() != null ||
                !TryParsePresetDuration(PresetWorkDraft, out var work) ||
                !TryParsePresetDuration(PresetBreakDraft, out var pause))
                return false;

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
                _presetSaveError = "Could not save preset: " + ex.Message;
                NotifyPresetDraft();
                return false;
            }
            _presetSaveError = null;
            NotifyPresetDraft();
            return true;
        }

        // Used by preset selection, creation, deletion and window closing.
        public bool ResolvePendingPresetEdits()
        {
            if (_resolvingPresetEdits)
                return false;
            if (FlushPresetSave())
                return true;

            _resolvingPresetEdits = true;
            NotifyPresetDraft();
            try
            {
                var result = MessageBox.Show(Application.Current.MainWindow,
                    (PresetEditError ?? "Preset changes could not be saved.") + "\n\nDiscard these edits?",
                    "Incomplete preset", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
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

        public bool ResolvePendingEdits() =>
            ResolvePendingActionEdits() && ResolvePendingPresetEdits();

        private bool ResolvePendingActionEdits()
        {
            if (_resolvingActionEdits || _resolvingPresetEdits)
                return false;
            if (!HasUnsavedActionChanges)
                return true;
            _resolvingActionEdits = true;
            NotifyActionDraft();
            try
            {
                var result = MessageBox.Show(Application.Current.MainWindow,
                    "Some scheduled action edits could not be saved. Discard those edits?",
                    "Incomplete scheduled actions", MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.Yes)
                {
                    RefreshScheduledActions();
                    return true;
                }
                return false;
            }
            finally
            {
                _resolvingActionEdits = false;
                NotifyActionDraft();
            }
        }

        private void CreatePreset()
        {
            if (!CanEditSettings || !ResolvePendingEdits())
                return;

            var preset = new Preset
            {
                Id = Guid.NewGuid(),
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

            if (!ResolvePendingEdits())
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

            return CanEditSettings && !_resolvingActionEdits && !_resolvingPresetEdits;
        }

        private void BrowseActionTarget(object? value)
        {
            if (!CanEditActions() || value is not ScheduledActionViewModel row || !ScheduledActions.Contains(row))
                return;

            var owner = Application.Current.MainWindow;
            var choice = new Workflow_Timer.Views.BrowseTargetView { Owner = owner };
            if (choice.ShowDialog() != true || !CanEditActions() || !ScheduledActions.Contains(row))
                return;

            if (choice.SelectedTarget == Workflow_Timer.Views.BrowseTargetKind.File)
            {
                var dialog = new OpenFileDialog { Title = "Select a file", Filter = "All files (*.*)|*.*", CheckFileExists = true };
                if (dialog.ShowDialog(owner) == true && CanEditActions() && ScheduledActions.Contains(row))
                    row.Target = dialog.FileName;
            }
            else if (choice.SelectedTarget == Workflow_Timer.Views.BrowseTargetKind.Folder)
            {
                var dialog = new OpenFolderDialog { Title = "Select a folder" };
                if (dialog.ShowDialog(owner) == true && CanEditActions() && ScheduledActions.Contains(row))
                    row.Target = dialog.FolderName;
            }
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

        private static bool TryInferTarget(string input, out ActionType type, out string target)
        {
            target = input.Trim();
            type = ActionType.File;
            if (Directory.Exists(target))
            {
                type = ActionType.Folder;
                target = Path.GetFullPath(target);
                return true;
            }
            if (File.Exists(target))
            {
                type = Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    ? ActionType.Executable : ActionType.File;
                target = Path.GetFullPath(target);
                return true;
            }
            if (target.Contains('\\') || target.StartsWith("/") ||
                (target.Length >= 2 && target[1] == ':'))
                return false;
            bool explicitUrl = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
            if (!TryNormalizeWebsite(target, out var normalized)) return false;
            var host = new Uri(normalized).Host;
            var suffix = host.Split('.').Last();
            string[] common = { "com", "org", "net", "edu", "gov", "io", "dev", "app", "info", "biz", "me", "co", "at", "de", "uk", "eu", "jp", "fr", "ch", "tv", "ai", "gg", "xyz" };
            if (!explicitUrl && !common.Contains(suffix, StringComparer.OrdinalIgnoreCase) &&
                !host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !global::System.Net.IPAddress.TryParse(host, out _))
                return false;
            type = ActionType.Website;
            target = normalized;
            return true;
        }

        private void AddScheduledAction()
        {
            if (!CanEditActions()) return;
            var row = new ScheduledActionViewModel(new ScheduledAction
            {
                Name = "New Action", Target = string.Empty, Enabled = true,
                OffsetFromStart = TimeSpan.Zero
            }, AutoSaveAction);
            row.SetPending("Enter an existing path or website URL.");
            ScheduledActions.Add(row);
            NotifyActionDraft();
        }

        private void AutoSaveAction(ScheduledActionViewModel row)
        {
            if (!CanEditActions() || !ScheduledActions.Contains(row)) return;
            if (HasUnsavedPresetChanges && !HasPresetEditError) FlushPresetSave();
            var session = SelectedPreset!.Session;
            string? error = null;
            TimeSpan offset = TimeSpan.Zero;
            ActionType type = ActionType.File;
            string target = string.Empty;
            if (string.IsNullOrWhiteSpace(row.Name)) error = "Enter a name.";
            else if (!TimeSpan.TryParseExact(row.OffsetText, "c", CultureInfo.InvariantCulture, out offset)
                || offset < TimeSpan.Zero || offset > session.WorkDuration)
                error = "Enter an offset within the work duration (HH:MM:SS).";
            else if (!TryInferTarget(row.Target, out type, out target)) error = "Enter an existing path or valid website URL.";
            if (error != null)
            {
                row.SetPending(error);
                NotifyActionDraft();
                return;
            }

            var action = row.Action;
            var old = (action.Name, action.ActionType, action.Target, action.OffsetFromStart, action.Enabled);
            var originalOrder = session.Actions.ToList();
            action.Name = row.Name.Trim();
            action.ActionType = type;
            action.Target = target;
            action.OffsetFromStart = offset;
            action.Enabled = row.Enabled;
            if (!session.Actions.Contains(action)) session.Actions.Add(action);
            SortSelectedPresetActions();
            try
            {
                PersistPresets();
                row.SetPending(null);
            }
            catch (Exception ex)
            {
                session.Actions = originalOrder;
                (action.Name, action.ActionType, action.Target, action.OffsetFromStart, action.Enabled) = old;
                row.SetPending("Save failed: " + ex.Message);
            }
            NotifyActionDraft();
            if (HasUnsavedPresetChanges) QueuePresetSave();
        }

        private void RemoveScheduledAction(object? value)
        {
            if (!CanEditActions() || value is not ScheduledActionViewModel row || !ScheduledActions.Contains(row))
                return;
            if (MessageBox.Show($"Remove scheduled action \"{row.Name}\"?", "Confirm Action Removal",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            var session = SelectedPreset!.Session;
            var originalOrder = session.Actions.ToList();
            if (session.Actions.Remove(row.Action))
            {
                try { PersistPresets(); }
                catch (Exception ex)
                {
                    session.Actions = originalOrder;
                    MessageBox.Show("Could not remove action: " + ex.Message, "Save failed");
                    return;
                }
            }
            ScheduledActions.Remove(row);
            NotifyActionDraft();
            if (HasUnsavedPresetChanges) QueuePresetSave();
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
            foreach (var entry in entries.Where(e => !e.HiddenFromRecentSessions)
                .OrderByDescending(e => e.StartedAt))
            {
                RecentHistory.Add(new HistoryDisplayRow
                {
                    Title = string.IsNullOrWhiteSpace(entry.PresetName) ? "Work" : entry.PresetName,
                    Detail = $"{entry.StartedAt:yyyy-MM-dd HH:mm} · {(entry.Completed ? "Completed" : "Stopped")}",
                    Duration = FormatWork(entry.WorkDuration)
                });
            }
        }

        private void ClearOldRecentSessions()
        {
            if (!int.TryParse(RecentSessionAgeDays.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int days) || days <= 0)
            {
                MessageBox.Show("Enter a positive whole number of days.",
                    "Invalid duration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var today = DateOnly.FromDateTime(DateTime.Now);
            try
            {
                int count = _historyService.CountRecentSessionsOlderThan(today, days);
                if (count == 0)
                {
                    MessageBox.Show("No visible sessions are older than that many days.",
                        "Recent sessions", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var confirmation = MessageBox.Show(
                    $"Clear {count} session(s) older than {days} calendar day(s) from Recent Sessions?\n\n" +
                    "Daily totals, personal best and all recorded work will remain unchanged.",
                    "Clear recent sessions", MessageBoxButton.YesNo,
                    MessageBoxImage.Question, MessageBoxResult.No);
                if (confirmation != MessageBoxResult.Yes)
                    return;

                int cleared = _historyService.ClearRecentSessionsOlderThan(today, days);
                RefreshHistory();
                MessageBox.Show($"Cleared {cleared} session(s) from Recent Sessions.",
                    "Recent sessions", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not clear recent sessions: " + ex.Message,
                    "History cleanup failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
