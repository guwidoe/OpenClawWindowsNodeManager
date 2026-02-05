using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using OpenClaw.Win.Core;

namespace OpenClaw.Win.App;

public static class ThemeManager
{
    private const string LightThemePath = "Themes/Light.xaml";
    private const string DarkThemePath = "Themes/Dark.xaml";

    public static void ApplyTheme(ThemePreference preference)
    {
        if (System.Windows.Application.Current == null)
        {
            return;
        }

        var systemIsLight = GetSystemAppsUseLightTheme();
        var useDarkTheme = ThemePreferenceResolver.Resolve(preference, systemIsLight);

        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries
            .Where(dict => dict.Source != null &&
                           (dict.Source.OriginalString.Contains(LightThemePath, StringComparison.OrdinalIgnoreCase) ||
                            dict.Source.OriginalString.Contains(DarkThemePath, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var dict in existing)
        {
            dictionaries.Remove(dict);
        }

        var source = useDarkTheme ? DarkThemePath : LightThemePath;
        dictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
        ApplyTitleBarTheme(useDarkTheme);
    }

    public static void ApplyTitleBar(Window window, ThemePreference preference)
    {
        var systemIsLight = GetSystemAppsUseLightTheme();
        var useDarkTheme = ThemePreferenceResolver.Resolve(preference, systemIsLight);
        WindowThemeHelper.ApplyTitleBar(window, useDarkTheme);
    }

    private static bool? GetSystemAppsUseLightTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                null);

            return value switch
            {
                int intValue => intValue != 0,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyTitleBarTheme(bool useDarkTheme)
    {
        if (System.Windows.Application.Current == null)
        {
            return;
        }

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            WindowThemeHelper.ApplyTitleBar(window, useDarkTheme);
        }
    }
}
