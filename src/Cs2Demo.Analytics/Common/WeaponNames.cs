namespace Cs2Demo.Analytics;

/// <summary>
/// 武器名归一化（纯函数，可单测）。V1 计算用不到武器，但解析时一并记录，
/// 避免以后加字段造成 breaking change。未知非空名字归为 UNKNOWN。
/// </summary>
internal static class WeaponNames
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        var name = raw.StartsWith("weapon_", StringComparison.Ordinal)
            ? raw["weapon_".Length..]
            : raw;
        var key = name.ToLowerInvariant();

        if (key.Contains("knife") || key.Contains("bayonet"))
            return "Knife";

        if (ShortNames.TryGetValue(key, out var display))
            return display;

        foreach (var pair in PrefixNames)
        {
            if (key.StartsWith(pair.Key, StringComparison.Ordinal) || key.EndsWith(pair.Key, StringComparison.Ordinal))
                return pair.Value;
        }

        return "UNKNOWN";
    }

    private static readonly Dictionary<string, string> ShortNames = new(StringComparer.Ordinal)
    {
        ["ak47"] = "AK-47",
        ["m4a1"] = "M4A4",
        ["m4a1_silencer"] = "M4A1",
        ["m4a1_silencer_off"] = "M4A1",
        ["awp"] = "AWP",
        ["ssg08"] = "SSG 08",
        ["g3sg1"] = "G3SG1",
        ["scar20"] = "SCAR-20",
        ["galilar"] = "Galil AR",
        ["famas"] = "FAMAS",
        ["aug"] = "AUG",
        ["sg556"] = "SG 553",
        ["mac10"] = "MAC-10",
        ["ump45"] = "UMP-45",
        ["p90"] = "P90",
        ["mp7"] = "MP7",
        ["mp5sd"] = "MP5-SD",
        ["mp9"] = "MP9",
        ["bizon"] = "PP-Bizon",
        ["nova"] = "Nova",
        ["xm1014"] = "XM1014",
        ["mag7"] = "MAG-7",
        ["sawedoff"] = "Sawed-Off",
        ["m249"] = "M249",
        ["negev"] = "Negev",
        ["glock"] = "Glock-18",
        ["hkp2000"] = "P2000",
        ["usp"] = "USP-S",
        ["usp_silencer"] = "USP-S",
        ["usp_silencer_off"] = "USP-S",
        ["p250"] = "P250",
        ["tec9"] = "Tec-9",
        ["fiveseven"] = "Five-SeveN",
        ["cz75a"] = "CZ75 Auto",
        ["deagle"] = "Desert Eagle",
        ["elite"] = "Dual Berettas",
        ["revolver"] = "R8 Revolver",
        ["taser"] = "Zeus x27",
        ["hegrenade"] = "HE Grenade",
        ["flashbang"] = "Flashbang",
        ["smokegrenade"] = "Smoke Grenade",
        ["molotov"] = "Molotov",
        ["incgrenade"] = "Incendiary Grenade",
        ["decoy"] = "Decoy Grenade",
        ["c4"] = "C4",
        ["planted_c4"] = "C4",
    };

    private static readonly KeyValuePair<string, string>[] PrefixNames =
    {
        new("galil", "Galil AR"),
        new("ak47", "AK-47"),
        new("deagle", "Desert Eagle"),
        new("fiveseven", "Five-SeveN"),
        new("tec9", "Tec-9"),
        new("cz75", "CZ75 Auto"),
        new("usp", "USP-S"),
        new("hkp2000", "P2000"),
        new("scar", "SCAR-20"),
        new("ssg08", "SSG 08"),
        new("sg556", "SG 553"),
        new("mac10", "MAC-10"),
        new("ump45", "UMP-45"),
        new("mp5sd", "MP5-SD"),
        new("mag7", "MAG-7"),
        new("sawedoff", "Sawed-Off"),
        new("smoke", "Smoke Grenade"),
        new("molotov", "Molotov"),
        new("incendiary", "Incendiary Grenade"),
        new("inferno", "Incendiary Grenade"),
        new("flashbang", "Flashbang"),
        new("hegrenade", "HE Grenade"),
        new("decoy", "Decoy Grenade"),
    };
}
