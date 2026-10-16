namespace Cs2Demo.Analytics;

/// <summary>一次有效伤害。取 csda 的原始 <c>healthDamage</c>（**不折叠溢出**，与 csda 口径一致）；自伤、队伤、世界伤害不会进入此表。</summary>
public sealed class DamageEvent
{
    /// <summary>所在回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>攻击者 SteamId。</summary>
    public string AttackerSteamId { get; set; } = "";
    /// <summary>受害者 SteamId。</summary>
    public string VictimSteamId { get; set; } = "";
    /// <summary>本次伤害量（csda 的 healthDamage，负数已归 0）。</summary>
    public int Damage { get; set; }
    /// <summary>攻击者阵营（T/CT/空）。</summary>
    public string AttackerSide { get; set; } = Sides.Unknown;
    /// <summary>受害者阵营（T/CT/空）。</summary>
    public string VictimSide { get; set; } = Sides.Unknown;
    /// <summary>归一化后的武器名。</summary>
    public string Weapon { get; set; } = "";
    /// <summary>受击瞬间受害者剩余护甲（csda 的 victimArmor 原值），未知为 0。</summary>
    public int VictimArmor { get; set; }
}
