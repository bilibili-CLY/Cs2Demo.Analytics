namespace Cs2Demo.Analytics;

/// <summary>一次残局（clutch）记录：某玩家在某回合面对若干敌人。</summary>
public sealed class ClutchInfo
{
    /// <summary>回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>残局玩家 SteamId。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>残局开始时敌方存活人数（1 表示 1v1，5 表示 1v5）。</summary>
    public int OpponentCount { get; set; }
    /// <summary>残局玩家阵营（T/CT/空）。</summary>
    public string Side { get; set; } = Sides.Unknown;
    /// <summary>该残局是否拿下（csda 的 hasWon 原值）。</summary>
    public bool Won { get; set; }
    /// <summary>残局玩家是否存活到回合结束。</summary>
    public bool Survived { get; set; }
    /// <summary>残局期间的击杀数。</summary>
    public int KillCount { get; set; }
}
