namespace OpenClaw.Win.Core;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public static class ThemePreferenceResolver
{
    public static bool Resolve(ThemePreference preference, bool? systemIsLight)
    {
        return preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            ThemePreference.System => systemIsLight == false,
            _ => false
        };
    }
}
