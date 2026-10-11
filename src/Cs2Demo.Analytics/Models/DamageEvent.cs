namespace Cs2Demo.Analytics;

/// <summary>一次有效伤害（已折成真实扣血，超出的溢出伤害不计）。自伤、队伤不会进入此表。</summary>
public sealed class DamageEvent
{
    /// <summary>所在回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>攻击者 SteamId。</summary>
    public string AttackerSteamId { get; set; } = "";
    /// <summary>受害者 SteamId。</summary>
    public string VictimSteamId { get; set; } = "";
    /// <summary>真实扣血（大于 0）。</summary>
    public int Damage { get; set; }
    /// <summary>攻击者阵营（T/CT/空）。</summary>
    public string AttackerSide { get; set; } = Sides.Unknown;
    /// <summary>受害者阵营（T/CT/空）。</summary>
    public string VictimSide { get; set; } = Sides.Unknown;
    /// <summary>归一化后的武器名。</summary>
    public string Weapon { get; set; } = "";
}
