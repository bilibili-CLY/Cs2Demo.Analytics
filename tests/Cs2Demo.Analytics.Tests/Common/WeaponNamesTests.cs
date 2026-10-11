using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

public sealed class WeaponNamesTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("weapon_ak47", "AK-47")]
    [InlineData("weapon_awp", "AWP")]
    [InlineData("weapon_m4a1_silencer", "M4A1")]
    [InlineData("weapon_knife_t", "Knife")]
    [InlineData("weapon_bayonet", "Knife")]
    [InlineData("weapon_deagle", "Desert Eagle")]
    [InlineData("weapon_hegrenade", "HE Grenade")]
    [InlineData("weapon_c4", "C4")]
    public void Normalize_MapsKnownWeapons(string? raw, string expected)
    {
        Assert.Equal(expected, WeaponNames.Normalize(raw));
    }

    [Theory]
    [InlineData("weapon_futuristick3000")]
    [InlineData("something_new")]
    public void Normalize_UnknownName_FallsBackToUnknown(string raw)
    {
        Assert.Equal("UNKNOWN", WeaponNames.Normalize(raw));
    }
}
