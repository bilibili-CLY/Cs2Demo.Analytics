namespace Cs2Demo.Analytics;

/// <summary>某玩家在某回合结束时是否存活。</summary>
public sealed class SurvivalInfo
{
    /// <summary>回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>玩家 SteamId。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>是否存活到回合结束。</summary>
    public bool Survived { get; set; }
    /// <summary>回合结束时阵营（T/CT/空）。</summary>
    public string Side { get; set; } = Sides.Unknown;
}
