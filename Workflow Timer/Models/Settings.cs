using Workflow_Timer.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Workflow_Timer.Models
{
    public class Settings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool NeverShowTutorial { get; set; } = false;
        public bool FirstLaunchSetupCompleted { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        // Only normal/maximized are persisted; minimized is never a startup state.
        public bool MainWindowMaximized { get; set; } = false;
        public ThemeMode Theme { get; set; } = ThemeMode.Light;
        public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.FreeDrag;
        public OverlayPosition OverlayFixedPosition { get; set; } = OverlayPosition.TopLeft;
        public bool HideWidget { get; set; } = false;
        public bool OverlayCompact { get; set; } = false;
        // WPF device-independent coordinates; null means no saved drag position.
        public double? OverlayLeft { get; set; }
        public double? OverlayTop { get; set; }
        public bool FocusGrabberEnabled { get; set; } = true;
        public int FocusGrabberIntervalMinutes { get; set; } = 5;
        public int SnoozeMinutes { get; set; } = 10;
        public bool WorkCompletionSoundEnabled { get; set; } = true;
        public bool BreakCompletionSoundEnabled { get; set; } = true;
        public bool FocusReminderSoundEnabled { get; set; } = true;

        public List<HotkeyBinding> Hotkeys { get; set; } = HotkeyBinding.CreateDefaults();

        public Settings Copy()
        {
            var copy = (Settings)MemberwiseClone();
            copy.Hotkeys = Hotkeys.Select(binding => binding.Copy()).ToList();
            return copy;
        }
    }
}
