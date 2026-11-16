using global::System;
using global::System.Linq;
using global::System.Windows;
using AppThemeMode = Workflow_Timer.Enums.ThemeMode;

namespace Workflow_Timer.Services.System
{
    public static class ThemeService
    {
        public static void Apply(AppThemeMode mode)
        {
            var app = Application.Current;
            if (app == null)
                return;

            var file = mode == AppThemeMode.Dark ? "DarkTheme.xaml" : "LightTheme.xaml";
            var palette = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Resources/Themes/" + file, UriKind.Absolute)
            };

            // .NET 10 exposes application-level Fluent light/dark switching.
            // Limit suppression of the experimental API diagnostic to this call.
#pragma warning disable WPF0001
            app.ThemeMode = mode == AppThemeMode.Dark
                ? global::System.Windows.ThemeMode.Dark
                : global::System.Windows.ThemeMode.Light;
#pragma warning restore WPF0001

            var dictionaries = app.Resources.MergedDictionaries;
            var previous = dictionaries.FirstOrDefault(dictionary =>
                dictionary.Source?.OriginalString.EndsWith("/LightTheme.xaml", StringComparison.OrdinalIgnoreCase) == true ||
                dictionary.Source?.OriginalString.EndsWith("/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) == true);
            if (previous != null)
                dictionaries[dictionaries.IndexOf(previous)] = palette;
            else
                dictionaries.Add(palette);
        }
    }
}
