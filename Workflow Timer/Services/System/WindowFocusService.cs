using global::System;
using global::System.Runtime.InteropServices;
using global::System.Windows;
using global::System.Windows.Interop;

namespace Workflow_Timer.Services.System
{
    public static class WindowFocusService
    {
        public static void BringIntoFocus(Window window)
        {
            bool wasHidden = !window.IsVisible;
            bool wasMinimized = window.WindowState == WindowState.Minimized;
            if (!window.IsVisible)
                window.Show();

            // Restoring the WPF property alone can leave the native HWND iconic
            // after Hide() was called from the minimized state.
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero && (wasHidden || wasMinimized || IsIconic(handle)))
                ShowWindow(handle, 9); // SW_RESTORE

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            // Windows may refuse foreground activation while another app is in use.
            // In that case, request taskbar attention instead of forcing focus.
            if (!window.Activate())
                FlashTaskbar(window);
        }

        private static void FlashTaskbar(Window window)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;
            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf<FlashInfo>(),
                Window = handle,
                Flags = 0x00000002, // FLASHW_TRAY
                Count = 3,
                Timeout = 0
            };
            FlashWindowEx(ref info);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashInfo
        {
            public uint Size;
            public IntPtr Window;
            public uint Flags;
            public uint Count;
            public uint Timeout;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FlashInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);
    }
}
