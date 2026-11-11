using System;
using System.Collections.Generic;
using System.Linq;
using Workflow_Timer.Models;

namespace Workflow_Timer.Services.Storage
{
    public class HistoryService
    {
        private readonly AppDataPathService _paths;
        private readonly JsonStorageService _storage;

        public HistoryService(AppDataPathService paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _storage = new JsonStorageService();
        }

        public List<HistoryEntry> LoadHistory() =>
            _storage.Load<List<HistoryEntry>>(_paths.HistoryPath)
            ?? new List<HistoryEntry>();

        public void AddSession(HistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (entry.WorkDuration <= TimeSpan.Zero)
                return;

            var entries = LoadHistory();
            entries.Add(entry);
            _storage.Save(_paths.HistoryPath, entries);
        }

        // All elapsed work is credited to the date the session started.
        // Splitting work across midnight is intentionally deferred.
        public TimeSpan GetDailyTotal(DateOnly date)
        {
            return TimeSpan.FromTicks(LoadHistory()
                .Where(entry => DateOnly.FromDateTime(entry.StartedAt) == date)
                .Sum(entry => entry.WorkDuration.Ticks));
        }

        public TimeSpan GetAllTimeTotal() =>
            TimeSpan.FromTicks(LoadHistory().Sum(entry => entry.WorkDuration.Ticks));

        // Historical entries remain intact. Clearing a personal best excludes
        // only that calendar date from future personal-best rankings.
        public int GetExcludedRecordDayCount() =>
            LoadRecordState().ExcludedDates.Distinct().Count();

        public bool ClearPersonalBest()
        {
            var best = GetBestDay();
            if (!best.HasValue)
                return false;

            var state = LoadRecordState();
            var dateKey = best.Value.Date.ToString("yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture);

            if (!state.ExcludedDates.Contains(dateKey))
                state.ExcludedDates.Add(dateKey);

            _storage.Save(_paths.RecordsPath, state);
            return true;
        }

        public (DateOnly Date, TimeSpan Total)? GetBestDay()
        {
            var excluded = LoadRecordState().ExcludedDates.ToHashSet(StringComparer.Ordinal);

            var best = LoadHistory()
                .GroupBy(entry => DateOnly.FromDateTime(entry.StartedAt))
                .Select(group => new
                {
                    Date = group.Key,
                    Ticks = group.Sum(entry => entry.WorkDuration.Ticks)
                })
                .Where(day => !excluded.Contains(day.Date.ToString("yyyy-MM-dd", global::System.Globalization.CultureInfo.InvariantCulture)))
                .OrderByDescending(day => day.Ticks)
                .ThenBy(day => day.Date)
                .FirstOrDefault();

            return best == null
                ? null
                : (best.Date, TimeSpan.FromTicks(best.Ticks));
        }

        private PersonalRecordState LoadRecordState()
        {
            var state = _storage.Load<PersonalRecordState>(_paths.RecordsPath)
                ?? new PersonalRecordState();

            state.ExcludedDates ??= new List<string>();
            return state;
        }
    }

    public class PersonalRecordState
    {
        public List<string> ExcludedDates { get; set; } = new();

        // Retained for compatibility with previously saved records.json.
        // The old reset timestamp no longer limits rankings.
        public DateTime? ResetAt { get; set; }
    }
}
