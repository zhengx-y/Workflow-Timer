using global::System;
using global::System.Diagnostics;
using global::System.IO;
using Workflow_Timer.Services.System;

namespace Workflow_Timer.Services.Storage
{
    public static class DataManagementService
    {
        public static bool ResetInProgress { get; private set; }
        private static string? _stagedDirectory;

        public static void OpenFolder()
        {
            var path = new AppDataPathService().RootPath;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        // Move the dedicated application directory atomically before shutting down.
        // If preparation fails, the original data and startup preference remain intact.
        public static void PrepareReset()
        {
            if (ResetInProgress) return;
            using var self = Process.GetCurrentProcess();
            var others = Process.GetProcessesByName(self.ProcessName);
            try
            {
                if (global::System.Linq.Enumerable.Any(others, p => p.Id != self.Id))
                    throw new InvalidOperationException("Close other Workflow Timer instances before resetting data.");
            }
            finally
            {
                foreach (var process in others) process.Dispose();
            }
            var root = new AppDataPathService().RootPath;
            var staged = root + ".reset." + Guid.NewGuid().ToString("N");
            var restoreStartup = new StartupService().Apply(false);
            try { Directory.Move(root, staged); }
            catch { restoreStartup(); throw; }
            _stagedDirectory = staged;
            ResetInProgress = true;
        }

        // Called after the WPF Exit event and ordinary window disposal.
        public static void FinishResetAndRestart()
        {
            if (!ResetInProgress || _stagedDirectory == null) return;
            try
            {
                Directory.Delete(_stagedDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                global::System.Windows.MessageBox.Show(
                    "Some old data could not be deleted. You may remove this folder manually:\n" +
                    _stagedDirectory + "\n\n" + ex.Message, "Workflow Timer reset");
            }
            try
            {
                var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path is unavailable.");
                var start = new ProcessStartInfo(exe) { UseShellExecute = false };
                if (string.Equals(Path.GetFileName(exe), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
                    start.ArgumentList.Add(global::System.Reflection.Assembly.GetEntryAssembly()!.Location);
                Process.Start(start);
            }
            catch (Exception ex)
            {
                global::System.Windows.MessageBox.Show(
                    "Data was reset. Please open Workflow Timer manually.\n\n" + ex.Message,
                    "Workflow Timer reset");
            }
        }
    }
}
