using global::System;
using global::System.Collections.ObjectModel;
using global::System.ComponentModel;
using global::System.Runtime.CompilerServices;
using global::System.Windows;
using global::System.Windows.Data;
using Workflow_Timer.Commands;
using Workflow_Timer.Models;
using Workflow_Timer.Services.Storage;

namespace Workflow_Timer.Viewmodels
{
    public sealed class TodoViewModel : INotifyPropertyChanged
    {
        private readonly TodoService _service;
        private readonly ObservableCollection<TodoItem> _tasks = new();
        private string _searchText = string.Empty;
        private string _storageStatus = string.Empty;
        private bool _hasUnsavedChanges;

        public ListCollectionView ActiveTasks { get; }
        public ListCollectionView CompletedTasks { get; }
        public TodoCommands Commands { get; }
        public bool CanEditTasks { get; }
        public TodoItem? LastAddedTask { get; private set; }
        public bool HasUnsavedChanges => _hasUnsavedChanges;
        public string StorageStatus
        {
            get => _storageStatus;
            private set { _storageStatus = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                value ??= string.Empty;
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                ActiveTasks.Refresh();
                CompletedTasks.Refresh();
            }
        }

        public TodoViewModel(TodoService service)
        {
            _service = service;
            try
            {
                foreach (var task in _service.LoadTasks())
                {
                    _tasks.Add(task);
                    task.PropertyChanged += TaskChanged;
                }
                CanEditTasks = true;
                StorageStatus = "Changes are saved automatically.";
            }
            catch (Exception ex)
            {
                // Never overwrite a file that could not be read.
                CanEditTasks = false;
                StorageStatus = "Could not load tasks. Editing disabled; existing file preserved. " + ex.Message;
            }

            ActiveTasks = CreateView(false);
            CompletedTasks = CreateView(true);
            Commands = new TodoCommands(AddTask, DeleteTask, () => TrySave());
        }

        private ListCollectionView CreateView(bool completed)
        {
            var view = new ListCollectionView(_tasks)
            {
                Filter = item => item is TodoItem task &&
                    task.IsCompleted == completed &&
                    task.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
            };
            view.LiveFilteringProperties.Add(nameof(TodoItem.Title));
            view.LiveFilteringProperties.Add(nameof(TodoItem.IsCompleted));
            view.IsLiveFiltering = true;
            return view;
        }

        private void AddTask()
        {
            if (!CanEditTasks) return;
            // The new item must be visible regardless of the previous search.
            SearchText = string.Empty;
            var task = new TodoItem();
            task.PropertyChanged += TaskChanged;
            _tasks.Insert(0, task);
            LastAddedTask = task;
            MarkDirtyAndSave();
        }

        private void DeleteTask(object? parameter)
        {
            if (!CanEditTasks || parameter is not TodoItem task || !_tasks.Contains(task))
                return;
            var result = MessageBox.Show(Application.Current.MainWindow,
                $"Delete task \"{task.Title}\"?",
                "Delete task", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;
            task.PropertyChanged -= TaskChanged;
            _tasks.Remove(task);
            MarkDirtyAndSave();
        }

        private void TaskChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (CanEditTasks)
                MarkDirtyAndSave();
        }

        private void MarkDirtyAndSave()
        {
            _hasUnsavedChanges = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            TrySave();
        }

        // All edits run on the UI dispatcher. Save immediately; no pending debounce
        // can lose the last keystroke when the window closes.
        public bool TrySave()
        {
            if (!_hasUnsavedChanges) return true;
            if (!CanEditTasks) return false;
            try
            {
                _service.SaveTasks(_tasks);
                _hasUnsavedChanges = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StorageStatus = "Changes are saved automatically.";
                return true;
            }
            catch (Exception ex)
            {
                StorageStatus = "Tasks not saved — retry before closing. " + ex.Message;
                return false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
