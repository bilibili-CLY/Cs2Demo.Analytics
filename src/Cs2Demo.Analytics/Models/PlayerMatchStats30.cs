namespace Cs2Demo.Analytics;

/// <summary>
/// 一名玩家单场 Rating30 统计（实验性本地近似，非 HLTV 官方值，见 docs/formulas.md）。
/// 与 <see cref="PlayerMatchStats"/>（Rating20 那 6 项）完全隔开：调 <c>Calculate30 / CalculateAll30</c> 才返回本类型。
/// 共 7 项：KPR / ADR / KAST / Surviving / MultiKill / RoundSwing / Rating30。
/// 算不出来的指标为 null（回合数为 0 时全为 null；缺伤害数据时只有 Adr 为 null，Rating30 会按 ADR=0 继续算）。
/// </summary>
public sealed class PlayerMatchStats30
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
    /// <summary>连杀评分（本地近似，见 docs/formulas.md）。</summary>
    public double? MultiKill { get; set; }
    /// <summary>回合胜率波动（百分点/回合，本地近似，见 docs/formulas.md）。</summary>
    public double? RoundSwing { get; set; }
    /// <summary>Rating 3.0 本地近似，非 HLTV 官方值（见 docs/formulas.md）。</summary>
    public double? Rating30 { get; set; }
}
