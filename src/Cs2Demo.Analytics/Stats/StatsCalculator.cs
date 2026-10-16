namespace Cs2Demo.Analytics;

/// <summary>
/// 玩家数据计算（两步 API 的第二步）：只读 <see cref="MatchData"/>。
/// 口径来源：KPR / ADR / KAST / Rating2.0 直接取 csda 算好的值；Surviving / Impact 由 csda 原料派生。
/// KAST / Surviving 均为 0~100 百分数。
/// 回合数为 0 时所有指标为 null，否则全部有值；传入的 data 为 null 或玩家不存在时才抛 <see cref="ArgumentException"/>。
/// Rating30 另见 <see cref="Calculate30(MatchData, string)"/>（独立方法、独立返回类型，与本方法互不影响）。
/// </summary>
public static class StatsCalculator
{
    /// <summary>Rating20 公式版本。公式或口径有改动时往上加，调用方用它判断口径。</summary>
    public const string Rating20Version = "2.0.0";
    /// <summary>Rating30 公式版本。公式或口径有改动时往上加，调用方用它判断口径。</summary>
    public const string Rating30Version = "3.0.0";

    // ── 对枪胜率模型（经济/武器权重）：p = sigmoid(b0 + b1*(自身装备-对手装备)/1000 + b2*是否T) ──
    // 系数按 HLTV Rating 3.0 公布的锚点校准（同装 ≈48%、步枪 vs 初始手枪 ≈75%），见 docs/formulas.md。
    internal const double DuelBeta0 = -0.0800;
    internal const double DuelBeta1 = 0.5124;
    internal const double DuelBeta2 = -0.0905;

    // ── Rating30 拟合系数（docs/fit30.py，用 100 行官方 HLTV Rating 3.0 拟合，见 docs/formulas.md）──
    internal const double AvgEKpr = 0.627240;
    internal const double AvgEAdr = 69.722210;
    internal const double AvgSurviving = 0.337601;
    internal const double AvgKast = 0.723368;
    internal const double AvgMultiKill = 0.152553;
    internal const double SwingScale = 0.038818;
    internal const double WeightKpr = 0.148393;
    internal const double WeightAdr = 0.223011;
    internal const double WeightSurviving = 0.083448;
    internal const double WeightKast = 0.322636;
    internal const double WeightMultiKill = 0.135395;
    internal const double WeightSwing = 0.161542;

    /// <summary>算出所有玩家的单场统计。</summary>
    public static IReadOnlyList<PlayerMatchStats> CalculateAll(MatchData data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        return data.Players
            .Select(p => Calculate(data, p.SteamId))
            .ToList();
    }

    /// <summary>算出一名玩家的单场统计。玩家不在本场时抛 <see cref="ArgumentException"/>。</summary>
    public static PlayerMatchStats Calculate(MatchData data, string steamId)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrEmpty(steamId))
            throw new ArgumentException("SteamId 不能为空。", nameof(steamId));

        var player = data.Players.FirstOrDefault(p => p.SteamId == steamId)
            ?? throw new ArgumentException("该玩家不在本场数据中。", nameof(steamId));

        var rounds = data.Rounds.Count;
        var stats = new PlayerMatchStats
        {
            SteamId = steamId,
            Name = player.Name,
            Rounds = rounds,
            Version = Rating20Version,
        };

        if (rounds <= 0)
            return stats; // 6 项全为 null

        var cs = player.Csda;
        var r = (double)rounds;
        var survived = Math.Max(0, rounds - cs.DeathCount);
        var surviving = survived / r;
        var impact = 2.13 * cs.AverageKillPerRound + 0.42 * (cs.AssistCount / r) - 0.41;

        stats.Kills = cs.KillCount;
        stats.Deaths = cs.DeathCount;
        stats.Assists = cs.AssistCount;
        stats.TotalDamage = cs.HealthDamage;
        stats.SurvivedRounds = survived;
        stats.KastRounds = RecoverKastRounds(cs.KastPercent, rounds);
        stats.Kpr = Round3(cs.AverageKillPerRound);
        stats.Adr = Round3(cs.AverageDamagePerRound);
        stats.Kast = Round3(cs.KastPercent);
        stats.Surviving = Round3(surviving * 100.0);
        stats.Impact = Round3(Math.Max(0, impact));
        stats.Rating20 = Round3(Math.Max(0, cs.HltvRating2));
        return stats;
    }

    /// <summary>
    /// 算出所有玩家的 Rating30 统计（本地近似，非 HLTV 官方值，见 docs/formulas.md）。
    /// 与 <see cref="CalculateAll"/> 完全隔开：本方法只返回 <see cref="PlayerMatchStats30"/>。
    /// </summary>
    public static IReadOnlyList<PlayerMatchStats30> CalculateAll30(MatchData data)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));

        return data.Players
            .Select(p => Calculate30(data, p.SteamId))
            .ToList();
    }

    /// <summary>
    /// 算出一名玩家的 Rating30 统计（本地近似，非 HLTV 官方值，见 docs/formulas.md）。
    /// 玩家不在本场时抛 <see cref="ArgumentException"/>。
    /// </summary>
    public static PlayerMatchStats30 Calculate30(MatchData data, string steamId)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrEmpty(steamId))
            throw new ArgumentException("SteamId 不能为空。", nameof(steamId));

        var player = data.Players.FirstOrDefault(p => p.SteamId == steamId)
            ?? throw new ArgumentException("该玩家不在本场数据中。", nameof(steamId));

        var rounds = data.Rounds.Count;
        var stats = new PlayerMatchStats30
        {
            SteamId = steamId,
            Name = player.Name,
            Rounds = rounds,
            Version = Rating30Version,
        };

        if (rounds <= 0)
            return stats; // 全部为 null

        var cs = player.Csda;
        var r = (double)rounds;
        var survived = Math.Max(0, rounds - cs.DeathCount);
        var surviving = survived / r;

        // 经济加权的击杀/伤害（eKPR / eADR）：按对枪胜率难度加权。
        var (eKpr, eAdr) = ComputeEcoMetrics(data, steamId);

        // 多杀回合占比（单回合 ≥2 杀），同时也是 Rating30 模型的一项输入。
        var multiKillFraction = (cs.TwoKillCount + cs.ThreeKillCount + cs.FourKillCount + cs.FiveKillCount) / r;

        var swingTotal = SwingCalculator.Compute(data).TryGetValue(steamId, out var s) ? s : 0.0;
        var swingAvg = swingTotal / r;

        var rating = WeightKpr * (eKpr / AvgEKpr)
            + WeightAdr * (eAdr / AvgEAdr)
            + WeightSurviving * (surviving / AvgSurviving)
            + WeightKast * ((cs.KastPercent / 100.0) / AvgKast)
            + WeightMultiKill * (multiKillFraction / AvgMultiKill)
            + WeightSwing * (1 + swingAvg / SwingScale);

        stats.Kills = cs.KillCount;
        stats.Deaths = cs.DeathCount;
        stats.Assists = cs.AssistCount;
        stats.TotalDamage = cs.HealthDamage;
        stats.SurvivedRounds = survived;
        stats.KastRounds = RecoverKastRounds(cs.KastPercent, rounds);
        stats.Kpr = Round3(cs.AverageKillPerRound);
        stats.Adr = Round3(cs.AverageDamagePerRound);
        stats.EKpr = Round3(eKpr);
        stats.EAdr = Round3(eAdr);
        stats.Kast = Round3(cs.KastPercent);
        stats.Surviving = Round3(surviving * 100.0);
        stats.MultiKill = Round3(multiKillFraction * 100.0);
        stats.RoundSwing = Round3(swingAvg * 100);
        stats.Rating30 = Round3(Math.Max(0, rating));
        return stats;
    }

    /// <summary>经济加权的 eKPR / eADR：按每回合各自与对手的装备差算对枪胜率，再用难度权重加权。</summary>
    private static (double EKpr, double EAdr) ComputeEcoMetrics(MatchData data, string steamId)
    {
        var rounds = data.Rounds.Count;
        if (rounds <= 0)
            return (0.0, 0.0);

        var equipment = new Dictionary<(int Round, string SteamId), int>();
        foreach (var s in data.Survivals)
            equipment[(s.RoundNumber, s.SteamId)] = s.EquipmentValue;

        int EquipmentOf(int round, string id) => equipment.TryGetValue((round, id), out var v) ? v : 0;

        var killSum = 0.0;
        var damageSum = 0.0;
        foreach (var k in data.Kills)
        {
            if (k.AttackerSteamId != steamId)
                continue;
            var p = DuelWinProb(
                EquipmentOf(k.RoundNumber, k.AttackerSteamId),
                EquipmentOf(k.RoundNumber, k.VictimSteamId),
                k.AttackerSide == Sides.Terrorist);
            killSum += EcoDifficulty(p);
        }

        foreach (var d in data.Damages)
        {
            if (d.AttackerSteamId != steamId)
                continue;
            var p = DuelWinProb(
                EquipmentOf(d.RoundNumber, d.AttackerSteamId),
                EquipmentOf(d.RoundNumber, d.VictimSteamId),
                d.AttackerSide == Sides.Terrorist);
            damageSum += d.Damage * EcoDifficulty(p);
        }

        var r = (double)rounds;
        return (killSum / r, damageSum / r);
    }

    /// <summary>对枪胜率：自己装备 vs 对手装备，含 T/CT 侧修正。</summary>
    internal static double DuelWinProb(int equipmentOwn, int equipmentFoe, bool ownIsT)
    {
        var z = DuelBeta0
            + DuelBeta1 * (equipmentOwn - equipmentFoe) / 1000.0
            + DuelBeta2 * (ownIsT ? 1.0 : 0.0);
        return 1.0 / (1.0 + Math.Exp(-z));
    }

    /// <summary>难度权重：势均力敌 ≈1.04，越占便宜越低、越吃亏越高。</summary>
    internal static double EcoDifficulty(double duelWinProb) => (1.0 - duelWinProb) / 0.5;

    /// <summary>无经济信息的简化回合胜率（T 方视角）：只按存活人数差，保持 5v5≈0.488、5v4≈0.721。</summary>
    internal static double WinProb(int tAlive, int ctAlive)
        => SwingCalculator.RoundWinProb(tAlive, ctAlive, 0, 0, false);

    /// <summary>四舍五入到 3 位小数。</summary>
    internal static double Round3(double value) => Math.Round(value * 1000) / 1000;

    /// <summary>由 KAST 百分比反推 KAST 回合数。</summary>
    private static int RecoverKastRounds(double kastPercent, int rounds)
        => (int)Math.Round(kastPercent / 100.0 * rounds);
}
