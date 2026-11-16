using Workflow_Timer.Enums;
using System.Collections.Generic;
using System.Linq;

namespace Workflow_Timer.Models
{
    public class Settings
    {
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        public ThemeMode Theme { get; set; } = ThemeMode.Light;
        public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopLeft;
        // WPF device-independent coordinates; null means no saved drag position.
        public double? OverlayLeft { get; set; }
        public double? OverlayTop { get; set; }
        public bool FocusGrabberEnabled { get; set; } = false;
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
