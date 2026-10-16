namespace Cs2Demo.Analytics;

/// <summary>
/// 一名玩家单场统计结果。V1 共 6 项：KPR / ADR / KAST / Surviving / Impact / Rating2.0。
/// 其中 KPR / ADR / KAST / Rating2.0 直接取 csda 口径（KAST 为 0~100 百分数）；
/// Surviving（0~100 百分数）/ Impact 由 csda 原料派生。回合数为 0 时所有指标为 null，否则全部有值。
/// </summary>
public sealed class PlayerMatchStats
{
    /// <summary>玩家 SteamId。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>玩家名。</summary>
    public string Name { get; set; } = "";
    /// <summary>Rating20 公式版本（如 "2.0.0"），与 <c>StatsCalculator.Rating20Version</c> 一致。</summary>
    public string Version { get; set; } = "";
    /// <summary>总回合数。</summary>
    public int Rounds { get; set; }
    /// <summary>击杀数（csda 口径，不含自杀）。</summary>
    public int Kills { get; set; }
    /// <summary>死亡数。</summary>
    public int Deaths { get; set; }
    /// <summary>助攻数。</summary>
    public int Assists { get; set; }
    /// <summary>总伤害（csda 的 HealthDamage，原始扣血、不折叠溢出）。</summary>
    public int? TotalDamage { get; set; }
    /// <summary>存活回合数（总回合数 - 死亡数）。</summary>
    public int SurvivedRounds { get; set; }
    /// <summary>KAST 回合数（由 csda 的 KAST% 反推）。</summary>
    public int KastRounds { get; set; }
    /// <summary>平均每回合击杀（csda 的 averageKillPerRound）。</summary>
    public double? Kpr { get; set; }
    /// <summary>平均每回合伤害（csda 的 averageDamagePerRound）。</summary>
    public double? Adr { get; set; }
    /// <summary>有贡献回合占比（0~100，csda 原值）。</summary>
    public double? Kast { get; set; }
    /// <summary>存活回合占比（0~100 百分数）。</summary>
    public double? Surviving { get; set; }
    /// <summary>影响力（由 csda 原料按官方公式派生，见 docs/formulas.md）。</summary>
    public double? Impact { get; set; }
    /// <summary>Rating 2.0（csda 的 hltvRating2，非 HLTV 官方值，见 docs/formulas.md）。</summary>
    public double? Rating20 { get; set; }
}
