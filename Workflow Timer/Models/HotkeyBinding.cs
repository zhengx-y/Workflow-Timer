using System.Collections.Generic;
using System.Text.Json.Serialization;
using Workflow_Timer.Enums;

namespace Workflow_Timer.Models
{
    public class HotkeyBinding
    {
        public HotkeyAction Action { get; set; }
        public HotkeyModifier Modifier1 { get; set; } = HotkeyModifier.Ctrl;
        public HotkeyModifier Modifier2 { get; set; } = HotkeyModifier.None;
        public string Key { get; set; } = string.Empty;

        [JsonIgnore]
        public string ActionName => Action switch
        {
            HotkeyAction.StartTimer => "Start timer",
            HotkeyAction.StopTimer => "Stop timer",
            HotkeyAction.PauseResume => "Pause / Resume",
            HotkeyAction.RepeatTimer => "Repeat timer",
            HotkeyAction.Extend5 => "Extend by 5 minutes",
            HotkeyAction.Extend10 => "Extend by 10 minutes",
            HotkeyAction.Extend30 => "Extend by 30 minutes",
            HotkeyAction.Extend60 => "Extend by 1 hour",
            HotkeyAction.ShowMainWindow => "Show main window",
            HotkeyAction.NextPreset => "Select next preset",
            HotkeyAction.PreviousPreset => "Select previous preset",
            HotkeyAction.CreatePreset => "Make new preset",
            _ => Action.ToString()
        };

        [JsonIgnore]
        public bool IsAssigned => !string.IsNullOrEmpty(Key);

        [JsonIgnore]
        public uint ModifierMask => (uint)Modifier1 | (uint)Modifier2;

        [JsonIgnore]
        public string Gesture => Modifier2 == HotkeyModifier.None
            ? $"{Modifier1} + {Key}" : $"{Modifier1} + {Modifier2} + {Key}";

        public HotkeyBinding Copy() => (HotkeyBinding)MemberwiseClone();

        public static List<HotkeyBinding> CreateDefaults() => new()
        {
            Assigned(HotkeyAction.StartTimer, "S"),
            Assigned(HotkeyAction.StopTimer, "X"),
            Assigned(HotkeyAction.PauseResume, "P"),
            Assigned(HotkeyAction.RepeatTimer, "R"),
            new() { Action = HotkeyAction.Extend5 },
            new() { Action = HotkeyAction.Extend10 },
            new() { Action = HotkeyAction.Extend30 },
            new() { Action = HotkeyAction.Extend60 },
            Assigned(HotkeyAction.ShowMainWindow, "W"),
            new() { Action = HotkeyAction.NextPreset },
            new() { Action = HotkeyAction.PreviousPreset },
            new() { Action = HotkeyAction.CreatePreset }
        };

        private static HotkeyBinding Assigned(HotkeyAction action, string key) => new()
        {
            Action = action,
            Modifier1 = HotkeyModifier.Ctrl,
            Modifier2 = HotkeyModifier.Alt,
            Key = key
        };
    }
}
