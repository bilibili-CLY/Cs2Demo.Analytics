namespace Cs2Demo.Analytics;

/// <summary>
/// 一名玩家单场统计结果。V1 共 6 项：KPR / ADR / KAST / Surviving / Impact / Rating2.0。
/// 算不出来的指标为 null（回合数为 0 时全为 null；缺伤害数据时只有 Adr 为 null，Rating2.0 会按 ADR=0 继续算）。
/// </summary>
public sealed class PlayerMatchStats
{
    /// <summary>玩家 SteamId。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>玩家名。</summary>
    public string Name { get; set; } = "";
    /// <summary>总回合数。</summary>
    public int Rounds { get; set; }
    /// <summary>击杀数（不含自杀/队伤）。</summary>
    public int Kills { get; set; }
    /// <summary>死亡数。</summary>
    public int Deaths { get; set; }
    /// <summary>助攻数。</summary>
    public int Assists { get; set; }
    /// <summary>总真实伤害。缺伤害数据时为 null。</summary>
    public int? TotalDamage { get; set; }
    /// <summary>存活回合数。</summary>
    public int SurvivedRounds { get; set; }
    /// <summary>KAST 回合数（有击杀/助攻/补枪/存活的回合，去重）。</summary>
    public int KastRounds { get; set; }
    /// <summary>平均每回合击杀。</summary>
    public double? Kpr { get; set; }
    /// <summary>平均每回合真实伤害。缺伤害数据时为 null。</summary>
    public double? Adr { get; set; }
    /// <summary>有贡献回合占比（0~1）。</summary>
    public double? Kast { get; set; }
    /// <summary>存活回合占比（0~1）。</summary>
    public double? Surviving { get; set; }
    /// <summary>影响力（本地近似，见 docs/formulas.md）。</summary>
    public double? Impact { get; set; }
    /// <summary>Rating 2.0 本地近似，非 HLTV 官方值（见 docs/formulas.md）。</summary>
    public double? Rating20 { get; set; }
}
