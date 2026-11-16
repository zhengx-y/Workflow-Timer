using global::System;
using global::System.IO;
using global::System.Runtime.InteropServices;
using Workflow_Timer.Services.Storage;

namespace Workflow_Timer.Services.System
{
    public static class ShortcutService
    {
        private static string PromptPath =>
            Path.Combine(new AppDataPathService().RootPath, "desktop-shortcut-prompted.txt");

        public static bool HasPrompted => File.Exists(PromptPath);

        public static void RememberPrompt()
        {
            File.WriteAllText(PromptPath, "The desktop shortcut prompt has been answered.");
        }

        public static string CreateDesktopShortcut()
        {
            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("The application executable path is unavailable.");
            if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
                string.Equals(Path.GetFileName(executable), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Run Workflow Timer from its own executable to create a shortcut.");

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop) || !Directory.Exists(desktop))
                throw new DirectoryNotFoundException("The Windows desktop folder could not be located.");

            string shortcutPath = Path.Combine(desktop, "Workflow Timer.lnk");
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "Workflow_Timer.ico");
            if (!File.Exists(iconPath)) iconPath = executable;

            Type shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("Windows shortcut creation is unavailable.");
            object shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Could not initialize Windows shortcut creation.");
            object? link = null;
            try
            {
                // Opening the same .lnk path updates it rather than creating duplicates.
                link = ((dynamic)shell).CreateShortcut(shortcutPath);
                dynamic shortcut = link;
                shortcut.TargetPath = executable;
                shortcut.Arguments = string.Empty;
                shortcut.WorkingDirectory = Path.GetDirectoryName(executable)!;
                shortcut.IconLocation = iconPath + ",0";
                shortcut.Description = "Workflow Timer";
                shortcut.WindowStyle = 1;
                shortcut.Save();
                if (!File.Exists(shortcutPath))
                    throw new IOException("Windows did not save the desktop shortcut.");
                return shortcutPath;
            }
            finally
            {
                if (link != null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
                if (Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
