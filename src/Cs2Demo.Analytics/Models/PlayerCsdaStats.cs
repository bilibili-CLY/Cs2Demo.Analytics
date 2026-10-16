namespace Cs2Demo.Analytics;

/// <summary>
/// 由 csda（akiver/cs-demo-analyzer）算好的单名玩家聚合值，原样带出，不二次加工。
/// 这是 V1 / Rating30 的口径来源：能在 csda 拿到的值，本库不再自己重算。
/// csda 未提供的量（Surviving / Impact / MultiKill / RoundSwing / Rating30）由
/// <see cref="StatsCalculator"/> 用这里的原料派生或计算。
/// </summary>
public sealed class PlayerCsdaStats
{
    /// <summary>所属队伍名（如 "Team A"），取不到为空字符串。</summary>
    public string Team { get; set; } = "";
    /// <summary>击杀数（不含自杀；队杀按 csda 口径）。</summary>
    public int KillCount { get; set; }
    /// <summary>死亡数（含各种死因）。</summary>
    public int DeathCount { get; set; }
    /// <summary>助攻数。</summary>
    public int AssistCount { get; set; }
    /// <summary>对敌方造成的原始血量伤害（**不折叠溢出**，csda 口径）。</summary>
    public int HealthDamage { get; set; }
    /// <summary>对敌方造成的护甲伤害（csda 口径）。</summary>
    public int ArmorDamage { get; set; }
    /// <summary>KAST 百分比（0~100，csda 原值）。</summary>
    public double KastPercent { get; set; }
    /// <summary>平均每回合击杀（csda 的 averageKillPerRound）。</summary>
    public double AverageKillPerRound { get; set; }
    /// <summary>平均每回合死亡（csda 的 averageDeathPerRound）。</summary>
    public double AverageDeathPerRound { get; set; }
    /// <summary>平均每回合伤害（csda 的 averageDamagePerRound = HealthDamage / 总回合数）。</summary>
    public double AverageDamagePerRound { get; set; }
    /// <summary>csda 的 HLTV Rating 1.0（逆拟合值，非官方）。</summary>
    public double HltvRating { get; set; }
    /// <summary>csda 的 HLTV Rating 2.0（逆拟合值，非官方）。</summary>
    public double HltvRating2 { get; set; }
    /// <summary>恰好 1 杀的回合数。</summary>
    public int OneKillCount { get; set; }
    /// <summary>恰好 2 杀的回合数。</summary>
    public int TwoKillCount { get; set; }
    /// <summary>恰好 3 杀的回合数。</summary>
    public int ThreeKillCount { get; set; }
    /// <summary>恰好 4 杀的回合数。</summary>
    public int FourKillCount { get; set; }
    /// <summary>5 杀及以上封顶的回合数。</summary>
    public int FiveKillCount { get; set; }
}
