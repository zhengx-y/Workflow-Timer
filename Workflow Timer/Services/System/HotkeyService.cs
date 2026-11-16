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
        private static int Id(HotkeyBinding binding) =>
            (int)((binding.ModifierMask << 8) | HotkeyKeyHelper.GetVirtualKey(binding.Key));

        public List<HotkeyBinding> Snapshot() =>
            _registered.Values.Select(binding => binding.Copy()).ToList();

        // At launch, keep each successful registration and list unavailable keys.
        public List<string> RegisterInitial(IEnumerable<HotkeyBinding> bindings)
        {
            var errors = new List<string>();
            foreach (var binding in bindings.Where(b => b.IsAssigned))
            {
                int id = Id(binding);
                if (TryRegister(id, binding, out string error))
                    _registered.Add(id, binding.Copy());
                else
                    errors.Add(error);
            }
            return errors;
        }

        // Register additions before releasing previous combinations. This means
        // an unavailable new shortcut leaves the existing registrations intact.
        public void Apply(IEnumerable<HotkeyBinding> bindings)
        {
            var desired = bindings.Where(b => b.IsAssigned)
                .ToDictionary(Id, b => b.Copy());
            var additions = new List<int>();
            var removals = new List<int>();
            try
            {
                foreach (var pair in desired)
                {
                    if (_registered.ContainsKey(pair.Key))
                        continue;
                    if (!TryRegister(pair.Key, pair.Value, out string error))
                        throw new InvalidOperationException(error);
                    additions.Add(pair.Key);
                }

                foreach (var pair in _registered)
                {
                    if (desired.ContainsKey(pair.Key))
                        continue;
                    if (!UnregisterHotKey(_handle, pair.Key))
                        throw new InvalidOperationException($"Could not release {pair.Value.Gesture}.");
                    removals.Add(pair.Key);
                }
            }
            catch (Exception error)
            {
                var rollbackErrors = new List<string>();
                foreach (int id in additions)
                {
                    if (!UnregisterHotKey(_handle, id))
                    {
                        _registered[id] = desired[id];
                        rollbackErrors.Add($"Could not release {desired[id].Gesture}.");
                    }
                }
                foreach (int id in removals)
                {
                    if (!TryRegister(id, _registered[id], out string restoreError))
                    {
                        _registered.Remove(id);
                        rollbackErrors.Add(restoreError);
                    }
                }
                if (rollbackErrors.Count > 0)
                    throw new InvalidOperationException(error.Message + " Restore failed: " +
                        string.Join(" ", rollbackErrors), error);
                throw;
            }

            _registered.Clear();
            foreach (var pair in desired)
                _registered.Add(pair.Key, pair.Value);
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
