using global::System;
using global::System.IO;
using global::System.Linq;
using global::System.Text.Json;
using Workflow_Timer.Helpers;
using Workflow_Timer.Models;

namespace Workflow_Timer.Services.Motivation
{
    public sealed class MotivationService
    {
        private static readonly string[] FallbackQuotes =
        {
            "The first push is the hardest. Keep the ball rolling.",
            "You don't need to finish everything today. You just need to begin.",
            "A small step forward is still a step forward.",
            "Give the next few minutes your attention, not your entire future."
        };

        private readonly string[] _quotes;
        private int _previousIndex = -1;

        public MotivationService()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", "Quotes", "Motivation.json");
            _quotes = LoadQuotes(path);
        }

        private static string[] LoadQuotes(string path)
        {
            try
            {
                var entries = JsonSerializer.Deserialize<MotivationQuote?[]>(
                    File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var quotes = entries?.Select(entry => entry?.Text?.Trim() ?? string.Empty)
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (quotes is { Length: >= 2 })
                    return quotes;
                if (quotes is { Length: 1 })
                    return quotes.Concat(FallbackQuotes)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                // Quotes must never prevent the timer from starting.
            }
            return FallbackQuotes;
        }

        public string GetRandomQuote()
        {
            _previousIndex = RandomHelper.NextIndex(_quotes.Length, _previousIndex);
            return _quotes[_previousIndex];
        }
    }
}
