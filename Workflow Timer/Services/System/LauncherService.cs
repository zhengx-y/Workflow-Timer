using System;
using System.Diagnostics;
using System.IO;
using Workflow_Timer.Enums;
using Workflow_Timer.Models;

namespace Workflow_Timer.Services.System
{
    public class LauncherService
    {
        public void Launch(ScheduledAction action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (!action.Enabled)
                return;

            if (string.IsNullOrWhiteSpace(action.Target))
                throw new ArgumentException(
                    "Scheduled action target cannot be empty.",
                    nameof(action.Target));


            switch (action.ActionType)
            {
                case ActionType.Website:
                    OpenWebsite(action.Target);
                    break;


                case ActionType.Executable:
                    OpenExecutable(action.Target);
                    break;


                case ActionType.Folder:
                    OpenFolder(action.Target);
                    break;


                case ActionType.File:
                    OpenFile(action.Target);
                    break;


                default:
                    throw new NotSupportedException(
                        $"Action type {action.ActionType} is not supported.");
            }
        }


        private void OpenWebsite(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }


        private void OpenExecutable(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Executable was not found.",
                    path);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }


        private void OpenFolder(string path)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException(path);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }


        private void OpenFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "File was not found.",
                    path);

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
    }
}