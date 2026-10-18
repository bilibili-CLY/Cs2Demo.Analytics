namespace Cs2Demo.Analytics;

/// <summary>
/// 一名玩家单场 Rating30 统计（实验性本地近似，非 HLTV 官方值，见 docs/formulas.md）。
/// 与 <see cref="PlayerMatchStats"/>（Rating20 那 6 项）完全隔开：调 <c>Calculate30 / CalculateAll30</c> 才返回本类型。
/// 共 12 项：KPR / ADR / eKPR / eADR / eKAST / eSurviving / eMultiKill / KAST / Surviving / MultiKill / RoundSwing / Rating30。
/// eKPR / eADR / eKAST / eSurviving / eMultiKill / RoundSwing / Rating30 由本库计算（Rating30 用 eKPR / eADR 替代 KPR / ADR）；
/// MultiKill（多杀回合占比，0~100）由 csda 的连杀回合数派生。
/// 回合数为 0 时所有指标为 null，否则全部有值。
/// </summary>
public sealed class PlayerMatchStats30
{
    /// <summary>玩家 SteamId。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>玩家名。</summary>
    public string Name { get; set; } = "";
    /// <summary>Rating30 公式版本（如 "3.2.0"），与 <c>StatsCalculator.Rating30Version</c> 一致。</summary>
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
    /// <summary>经济加权 eKPR（按对枪胜率难度加权的每回合击杀，本地近似）。Rating30 用它替代 KPR。</summary>
    public double? EKpr { get; set; }
    /// <summary>经济加权 eADR（按对枪胜率难度加权的每回合伤害，本地近似）。Rating30 用它替代 ADR。</summary>
    public double? EAdr { get; set; }
    /// <summary>经济/情境加权 eKAST，仅作为 Rating30 内部输入和调试字段。</summary>
    public double? EKast { get; set; }
    /// <summary>经济/情境加权存活率，仅作为 Rating30 内部输入和调试字段。</summary>
    public double? ESurviving { get; set; }
    /// <summary>经济/情境加权多杀强度，仅作为 Rating30 内部输入和调试字段。</summary>
    public double? EMultiKill { get; set; }
    /// <summary>有贡献回合占比（0~100，csda 原值）。</summary>
    public double? Kast { get; set; }
    /// <summary>存活回合占比（0~100 百分数）。</summary>
    public double? Surviving { get; set; }
    /// <summary>多杀回合占比（0~100 百分数；单回合 ≥2 杀的回合占全部回合的百分比）。</summary>
    public double? MultiKill { get; set; }
    /// <summary>回合胜率波动（百分点/回合，本地近似，见 docs/formulas.md）。</summary>
    public double? RoundSwing { get; set; }
    /// <summary>Rating 3.0 本地近似，非 HLTV 官方值（见 docs/formulas.md）。</summary>
    public double? Rating30 { get; set; }
}
