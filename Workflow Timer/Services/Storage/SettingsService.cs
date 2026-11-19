using System;
using System.IO;
using System.Collections.Generic;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Helpers;

namespace Workflow_Timer.Services.Storage
{
    public class SettingsService
    {
        private readonly AppDataPathService _paths;
        private readonly JsonStorageService _storage = new();

        public SettingsService(AppDataPathService paths)
        {
            _paths = paths;
        }

        public Settings Load()
        {
            var path = _paths.SettingsPath;
            if (!File.Exists(path))
            {
                var defaults = new Settings { LeftPanelVisible = false, RightPanelVisible = false };
                Save(defaults);
                return defaults;
            }

            var settings = _storage.Load<Settings>(path)
                ?? throw new InvalidDataException("Settings file contains no settings.");
            Validate(settings);
            return settings;
        }

        public void Save(Settings settings)
        {
            Validate(settings);
            var path = _paths.SettingsPath;
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                _storage.Save(temporaryPath, settings);
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public static void Validate(Settings settings)
        {
            if (!Enum.IsDefined(settings.Theme))
                throw new InvalidDataException("Select a valid theme.");
            if (!Enum.IsDefined(settings.OverlayPosition))
                throw new InvalidDataException("Select a valid widget position.");
            if (!Enum.IsDefined(settings.OverlayFixedPosition) || settings.OverlayFixedPosition == OverlayPosition.FreeDrag)
                throw new InvalidDataException("Select a valid fixed widget position.");
            if (settings.FocusGrabberIntervalMinutes < 1 || settings.FocusGrabberIntervalMinutes > 1440)
                throw new InvalidDataException("Reminder interval must be between 1 and 1440 minutes.");
            if (settings.SnoozeMinutes < 1 || settings.SnoozeMinutes > 1440)
                throw new InvalidDataException("Snooze duration must be between 1 and 1440 minutes.");
            ValidateHotkeys(settings.Hotkeys);
        }

        private static void ValidateHotkeys(List<HotkeyBinding> hotkeys)
        {
            if (hotkeys == null)
                throw new InvalidDataException("Hotkey settings are missing.");

            var actions = new HashSet<HotkeyAction>();
            var gestures = new HashSet<(uint Modifiers, uint VirtualKey)>();
            foreach (var binding in hotkeys)
            {
                if (binding == null || !Enum.IsDefined(binding.Action) || !actions.Add(binding.Action))
                    throw new InvalidDataException("Invalid or duplicate hotkey action.");
                if (!Enum.IsDefined(binding.Modifier1) || binding.Modifier1 == HotkeyModifier.None ||
                    !Enum.IsDefined(binding.Modifier2) ||
                    binding.Modifier1 == binding.Modifier2)
                    throw new InvalidDataException($"{binding.ActionName}: choose two different modifiers or None for the second.");
                if (binding.Key == null)
                    throw new InvalidDataException($"{binding.ActionName}: select a supported key or leave it blank.");
                if (binding.IsAssigned)
                {
                    // Unknown keys are kept in JSON but skipped by the registration service.
                    if (HotkeyKeyHelper.TryGetVirtualKey(binding.Key, out uint virtualKey) &&
                        !gestures.Add((binding.ModifierMask, virtualKey)))
                        throw new InvalidDataException($"Duplicate hotkey: {binding.Gesture}.");
                }
            }

            if (actions.Count != Enum.GetValues<HotkeyAction>().Length)
                throw new InvalidDataException("One or more hotkey actions are missing.");
        }
    }
}
