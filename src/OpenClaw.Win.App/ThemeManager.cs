using System;
using System.Linq;
using System.Windows;

namespace OpenClaw.Win.App;

public static class ThemeManager
{
    private const string LightThemePath = "Themes/Light.xaml";
    private const string DarkThemePath = "Themes/Dark.xaml";

    public static void ApplyTheme(bool useDarkTheme)
    {
        if (Application.Current == null)
        {
            return;
        }

        var dictionaries = Application.Current.Resources.MergedDictionaries;
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
    }
}
