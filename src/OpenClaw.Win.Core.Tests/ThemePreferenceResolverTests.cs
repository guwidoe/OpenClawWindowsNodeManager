using OpenClaw.Win.Core;
using Xunit;

namespace OpenClaw.Win.Core.Tests;

public class ThemePreferenceResolverTests
{
    [Theory]
    [InlineData(ThemePreference.Dark, true)]
    [InlineData(ThemePreference.Light, false)]
    public void Resolve_ExplicitPreference_Wins(ThemePreference preference, bool expectedDark)
    {
        var result = ThemePreferenceResolver.Resolve(preference, systemIsLight: null);
        Assert.Equal(expectedDark, result);
    }

    [Fact]
    public void Resolve_SystemPreference_UsesSystemLightFlag()
    {
        Assert.False(ThemePreferenceResolver.Resolve(ThemePreference.System, systemIsLight: true));
        Assert.True(ThemePreferenceResolver.Resolve(ThemePreference.System, systemIsLight: false));
    }

    [Fact]
    public void Resolve_SystemPreference_DefaultsToLightWhenUnknown()
    {
        Assert.False(ThemePreferenceResolver.Resolve(ThemePreference.System, systemIsLight: null));
    }
}
