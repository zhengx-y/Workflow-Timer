using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using Workflow_Timer.Models;

namespace Workflow_Timer.Services.Storage
{
    public sealed class TodoService
    {
        private readonly AppDataPathService _paths;
        private readonly JsonStorageService _storage = new();

        public TodoService(AppDataPathService paths) => _paths = paths;

        public List<TodoItem> LoadTasks()
        {
            string path = _paths.TasksPath;
            if (!File.Exists(path))
                return new List<TodoItem>();
            var tasks = _storage.Load<List<TodoItem>>(path);
            if (tasks == null || tasks.Any(task => task == null))
                throw new InvalidDataException("The task file does not contain a valid task list.");
            return tasks;
        }

        public void SaveTasks(IEnumerable<TodoItem> tasks)
        {
            string path = _paths.TasksPath;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                _storage.Save(temporary, tasks.ToList());
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
