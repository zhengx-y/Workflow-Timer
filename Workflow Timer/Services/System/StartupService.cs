using global::System;
using global::System.IO;
using Microsoft.Win32;

namespace Workflow_Timer.Services.System
{
    public class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Workflow Timer";

        // Returns an action that restores the previous entry if saving JSON fails.
        public Action Apply(bool enabled)
        {
            string? command = enabled ? GetStartupCommand() : null;

            using var key = enabled
                ? Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
                : Registry.CurrentUser.OpenSubKey(RunKey, writable: true);

            if (key == null)
            {
                if (enabled)
                    throw new InvalidOperationException("Could not open the Windows startup registry key.");
                return () => { };
            }

            var previousValue = key.GetValue(ValueName, null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            var previousKind = previousValue != null
                ? key.GetValueKind(ValueName)
                : RegistryValueKind.None;

            if (enabled)
            {
                if (previousKind == RegistryValueKind.String &&
                    string.Equals(previousValue as string, command, StringComparison.Ordinal))
                    return () => { };

                key.SetValue(ValueName, command!, RegistryValueKind.String);
            }
            else
            {
                if (previousValue == null)
                    return () => { };

                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return () =>
            {
                if (previousValue == null)
                {
                    using var restoreKey = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                    restoreKey?.DeleteValue(ValueName, throwOnMissingValue: false);
                }
                else
                {
                    using var restoreKey = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
                        ?? throw new InvalidOperationException("Could not restore the Windows startup entry.");
                    restoreKey.SetValue(ValueName, previousValue, previousKind);
                }
            };
        }

        private static string GetStartupCommand()
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable) ||
                !File.Exists(executable) ||
                string.Equals(Path.GetFileName(executable), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Start Workflow Timer from its own executable before enabling Windows startup.");

            // Quotes are required because the executable path can contain spaces.
            return "\"" + executable + "\"";
        }
    }
}
