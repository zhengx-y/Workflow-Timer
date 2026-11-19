using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Runtime.InteropServices;
using global::System.Windows.Interop;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;
using Workflow_Timer.Helpers;

namespace Workflow_Timer.Services.System
{
    public sealed class HotkeyService : IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;
        private readonly HwndSource _source;
        private readonly IntPtr _handle;
        private readonly Dictionary<int, HotkeyBinding> _registered = new();
        private readonly HashSet<int> _pendingRelease = new();
        private bool _disposed;

        public event EventHandler<HotkeyAction>? HotkeyPressed;

        public HotkeyService(HwndSource source)
        {
            _source = source;
            _handle = source.Handle;
            _source.AddHook(WindowHook);
        }

        // The modifier mask and Windows virtual-key code uniquely identify the
        // combination, regardless of the order of the two modifiers.
        private static bool TryGetId(HotkeyBinding binding, out int id)
        {
            id = 0;
            if (!HotkeyKeyHelper.TryGetVirtualKey(binding.Key, out uint key)) return false;
            id = (int)((binding.ModifierMask << 8) | key);
            return true;
        }

        public List<HotkeyBinding> Snapshot() =>
            _registered.Values.Select(binding => binding.Copy()).ToList();

        // At launch, keep each successful registration and list unavailable keys.
        public List<string> RegisterInitial(IEnumerable<HotkeyBinding> bindings)
        {
            return ApplyBestEffort(bindings);
        }

        // Each gesture is independent: unavailable combinations never block settings.
        public void Apply(IEnumerable<HotkeyBinding> bindings) => ApplyBestEffort(bindings);

        private List<string> ApplyBestEffort(IEnumerable<HotkeyBinding> bindings)
        {
            var errors = new List<string>();
            var desired = new Dictionary<int, HotkeyBinding>();
            foreach (var binding in bindings.Where(b => b.IsAssigned))
            {
                if (TryGetId(binding, out int id)) desired.TryAdd(id, binding.Copy());
                else errors.Add($"{binding.ActionName}: unsupported key {binding.Key}.");
            }

            foreach (int id in _pendingRelease.ToArray())
                if (UnregisterHotKey(_handle, id)) _pendingRelease.Remove(id);

            foreach (int id in _registered.Keys.ToArray())
            {
                if (desired.ContainsKey(id)) continue;
                if (!UnregisterHotKey(_handle, id))
                {
                    _pendingRelease.Add(id);
                    errors.Add($"Could not release {_registered[id].Gesture}.");
                }
                // Never dispatch a gesture the user has removed, even if native release failed.
                _registered.Remove(id);
            }

            foreach (var pair in desired)
            {
                if (_registered.ContainsKey(pair.Key))
                {
                    _registered[pair.Key] = pair.Value;
                    continue;
                }
                if (_pendingRelease.Contains(pair.Key))
                {
                    errors.Add($"{pair.Value.ActionName}: previous registration could not be released.");
                    continue;
                }
                if (TryRegister(pair.Key, pair.Value, out string error))
                    _registered.Add(pair.Key, pair.Value);
                else errors.Add(error);
            }
            foreach (string error in errors)
                global::System.Diagnostics.Trace.TraceWarning(error);
            return errors;
        }

        private bool TryRegister(int id, HotkeyBinding binding, out string error)
        {
            if (RegisterHotKey(_handle, id, binding.ModifierMask | ModNoRepeat,
                HotkeyKeyHelper.GetVirtualKey(binding.Key)))
            {
                error = string.Empty;
                return true;
            }
            int code = Marshal.GetLastWin32Error();
            error = $"{binding.ActionName}: {binding.Gesture} is unavailable (Windows error {code}). Choose another shortcut or clear the key.";
            return false;
        }

        private IntPtr WindowHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (!_disposed && message == WmHotkey &&
                _registered.TryGetValue(wParam.ToInt32(), out var binding))
            {
                handled = true;
                HotkeyPressed?.Invoke(this, binding.Action);
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (int id in _registered.Keys)
                UnregisterHotKey(_handle, id);
            foreach (int id in _pendingRelease)
                UnregisterHotKey(_handle, id);
            _pendingRelease.Clear();
            _registered.Clear();
            if (!_source.IsDisposed)
                _source.RemoveHook(WindowHook);
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
