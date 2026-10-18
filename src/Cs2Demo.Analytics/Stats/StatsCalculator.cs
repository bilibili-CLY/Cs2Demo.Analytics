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
    public const string Rating30Version = "3.2.0";

    // ── 对枪胜率模型（HLTV 六档装备）：p = sigmoid(b0 + 自身档位偏移 - 对手档位偏移 + b2*是否T) ──
    // 偏移按 HLTV 公布的锚点校准：同档 ≈48%、一线步枪 vs 初始手枪 ≈75%、AWP vs 一线步枪 T 56%/CT 60%。
    internal const double DuelBeta0 = -0.0800;
    internal const double DuelBeta2 = -0.0905;

    /// <summary>HLTV 的六档装备（从低到高）。</summary>
    internal enum DuelTier
    {
        StarterPistol = 0,
        UpgradedPistol = 1,
        SmgOrShotgun = 2,
        TierTwoRifle = 3,
        TierOneRifle = 4,
        Sniper = 5,
    }

    // 档位偏移（logit）。0 / 0.30 / 0.55 / 0.85 / 1.18 按公开锚点线性插值，
    // 1.63 由 AWP 锚点反推（Sniper - TierOne = 0.45）。
    internal static readonly double[] DuelTierOffsets = { 0.0, 0.30, 0.55, 0.85, 1.18, 1.63 };

    // ── Rating30 拟合系数（docs/fit30.py，用 100 行官方 HLTV Rating 3.0 拟合，见 docs/formulas.md）──
    internal const double AvgEKpr = 0.695300;
    internal const double AvgEAdr = 75.566920;
    internal const double AvgSurviving = 0.337601;
    internal const double AvgKast = 0.723368;
    internal const double AvgMultiKill = 0.152553;
    internal const double SwingScale = 0.039305;
    internal const double WeightKpr = 0.130933;
    internal const double WeightAdr = 0.235356;
    internal const double WeightSurviving = 0.086975;
    internal const double WeightKast = 0.320494;
    internal const double WeightMultiKill = 0.143041;
    internal const double WeightSwing = 0.158369;

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

        // 六项子评分的内部输入：击杀/伤害使用对枪难度，存活/KAST/多杀使用动作发生概率。
        var eco = ComputeEcoMetrics(data, steamId);

        // 多杀回合占比（单回合 ≥2 杀），同时也是 Rating30 模型的一项输入。
        var multiKillFraction = (cs.TwoKillCount + cs.ThreeKillCount + cs.FourKillCount + cs.FiveKillCount) / r;

        var swingTotal = SwingCalculator.Compute(data).TryGetValue(steamId, out var s) ? s : 0.0;
        var swingAvg = swingTotal / r;

        // 现有 100 条官方单图样本上，动作概率校正的样本外证据不足；
        // 因此 3.1 先保留原始 KAST/存活/多杀作为合成输入，情境校正值单独输出供后续模型迭代。
        var rating = WeightKpr * (eco.EKpr / AvgEKpr)
            + WeightAdr * (eco.EAdr / AvgEAdr)
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
        stats.EKpr = Round3(eco.EKpr);
        stats.EAdr = Round3(eco.EAdr);
        stats.EKast = Round3(eco.EKast * 100.0);
        stats.ESurviving = Round3(eco.ESurviving * 100.0);
        stats.EMultiKill = Round3(eco.EMultiKill * 100.0);
        stats.Kast = Round3(cs.KastPercent);
        stats.Surviving = Round3(surviving * 100.0);
        stats.MultiKill = Round3(multiKillFraction * 100.0);
        stats.RoundSwing = Round3(swingAvg * 100);
        stats.Rating30 = Round3(Math.Max(0, rating));
        return stats;
    }

    /// <summary>计算六项 Rating30 的内部情境特征。</summary>
    private static EcoMetrics ComputeEcoMetrics(MatchData data, string steamId)
    {
        var rounds = data.Rounds.Count;
        if (rounds <= 0)
            return new EcoMetrics(0.0, 0.0, 0.0, 0.0, 0.0);

        var equipment = new Dictionary<(int Round, string SteamId), int>();
        foreach (var s in data.Survivals)
            equipment[(s.RoundNumber, s.SteamId)] = s.EquipmentValue;

        int EquipmentOf(int round, string id) => equipment.TryGetValue((round, id), out var v) ? v : 0;

        // 受害者没有直接的武器字段；用其本回合作为击杀者时的武器推断（同一回合武器一般不变）。
        var weaponByRoundPlayer = new Dictionary<(int Round, string SteamId), string>();
        foreach (var k in data.Kills)
        {
            if (k.AttackerSteamId.Length == 0)
                continue;
            var key = (k.RoundNumber, k.AttackerSteamId);
            if (!weaponByRoundPlayer.ContainsKey(key))
                weaponByRoundPlayer[key] = k.Weapon;
        }
        string WeaponOf(int round, string id)
            => weaponByRoundPlayer.TryGetValue((round, id), out var w) ? w : "";

        var killSum = 0.0;
        var damageSum = 0.0;
        foreach (var k in data.Kills)
        {
            if (k.AttackerSteamId != steamId)
                continue;
            var p = DuelWinProb(
                EquipmentOf(k.RoundNumber, k.AttackerSteamId),
                EquipmentOf(k.RoundNumber, k.VictimSteamId),
                k.AttackerSide == Sides.Terrorist,
                k.Weapon,
                WeaponOf(k.RoundNumber, k.VictimSteamId));
            killSum += EcoDifficulty(p);
        }

        foreach (var d in data.Damages)
        {
            if (d.AttackerSteamId != steamId)
                continue;
            var p = DuelWinProb(
                EquipmentOf(d.RoundNumber, d.AttackerSteamId),
                EquipmentOf(d.RoundNumber, d.VictimSteamId),
                d.AttackerSide == Sides.Terrorist,
                d.Weapon,
                WeaponOf(d.RoundNumber, d.VictimSteamId));
            damageSum += d.Damage * EcoDifficulty(p);
        }

        var survivalsByRound = data.Survivals
            .GroupBy(s => s.RoundNumber)
            .ToDictionary(g => g.Key, g => g.ToList());
        var killCountByRound = data.Kills
            .Where(k => k.AttackerSteamId.Length > 0)
            .GroupBy(k => (k.RoundNumber, k.AttackerSteamId))
            .ToDictionary(g => g.Key, g => g.Count());
        var kastActions = new HashSet<(int Round, string SteamId)>();
        foreach (var k in data.Kills)
        {
            if (k.AttackerSteamId.Length > 0)
                kastActions.Add((k.RoundNumber, k.AttackerSteamId));
            if (k.AssisterSteamId.Length > 0)
                kastActions.Add((k.RoundNumber, k.AssisterSteamId));
            if (k.IsTradeDeath && k.VictimSteamId.Length > 0)
                kastActions.Add((k.RoundNumber, k.VictimSteamId));
        }
        foreach (var s in data.Survivals)
        {
            if (s.Survived)
                kastActions.Add((s.RoundNumber, s.SteamId));
        }

        var survivalSum = 0.0;
        var kastSum = 0.0;
        var multiKillSum = 0.0;
        foreach (var s in data.Survivals.Where(s => s.SteamId == steamId))
        {
            var opponents = survivalsByRound.TryGetValue(s.RoundNumber, out var roster)
                ? roster.Where(x => x.Side != s.Side && x.EquipmentValue > 0).ToList()
                : new List<SurvivalInfo>();
            var opponentAverage = opponents.Count == 0 ? 0.0 : opponents.Average(x => x.EquipmentValue);
            var diff = (s.EquipmentValue - opponentAverage) / 1000.0;

            var survivalProbability = ActionProbability(SurvivalIntercept + SurvivalDiff * diff);
            var kastProbability = ActionProbability(KastIntercept + KastDiff * diff);
            var multiProbability = ActionProbability(MultiKillIntercept + MultiKillDiff * diff);

            if (s.Survived)
                survivalSum += ActionDifficulty(SurvivalBaseRate, survivalProbability);
            if (kastActions.Contains((s.RoundNumber, steamId)))
                kastSum += ActionDifficulty(KastBaseRate, kastProbability);
            if (killCountByRound.TryGetValue((s.RoundNumber, steamId), out var killsThisRound)
                && killsThisRound >= 2)
            {
                var multiBase = killsThisRound switch
                {
                    2 => 1.0,
                    3 => 1.6,
                    4 => 2.4,
                    _ => 3.4,
                };
                multiKillSum += multiBase * ActionDifficulty(MultiKillBaseRate, multiProbability);
            }
        }

        var r = (double)rounds;
        return new EcoMetrics(
            killSum / r,
            damageSum / r,
            survivalSum / r,
            kastSum / r,
            multiKillSum / r);
    }

    // 由 10 张训练 demo 的逐玩家逐回合标签拟合；输入是自己装备 - 敌方平均装备（千元）。
    // 运行时只求值，不训练。官方精确参数未公开，因此这些参数只代表本库近似。
    private const double KastIntercept = 1.064364;
    private const double KastDiff = 0.229506;
    private const double SurvivalIntercept = -0.899842;
    private const double SurvivalDiff = 0.268005;
    private const double MultiKillIntercept = -1.658862;
    private const double MultiKillDiff = 0.149109;
    private const double KastBaseRate = 0.7226851851851852;
    private const double SurvivalBaseRate = 0.33935185185185185;
    private const double MultiKillBaseRate = 0.15185185185185185;

    private static double ActionProbability(double logit)
        => 1.0 / (1.0 + Math.Exp(-logit));

    private static double ActionDifficulty(double baseRate, double probability)
        => Math.Clamp(baseRate / Math.Max(0.02, probability), 0.5, 2.0);

    private readonly record struct EcoMetrics(
        double EKpr,
        double EAdr,
        double ESurviving,
        double EKast,
        double EMultiKill);

    /// <summary>武器名 → 六档装备；无法归类（刀/雷/机枪/UNKNOWN）返回 null，调用方回退装备价值。</summary>
    internal static DuelTier? TierForWeapon(string? weapon) => weapon switch
    {
        "AWP" or "SCAR-20" or "G3SG1" => DuelTier.Sniper,
        "AK-47" or "M4A4" or "M4A1" or "M4A1-S" or "AUG" or "SG 553" => DuelTier.TierOneRifle,
        "Galil AR" or "FAMAS" or "SSG 08" => DuelTier.TierTwoRifle,
        "MAC-10" or "UMP-45" or "P90" or "MP7" or "MP5-SD" or "MP9" or "PP-Bizon"
            or "Nova" or "XM1014" or "MAG-7" or "Sawed-Off" => DuelTier.SmgOrShotgun,
        "Desert Eagle" or "R8 Revolver" or "CZ75 Auto" or "Five-SeveN" or "Tec-9"
            or "Dual Berettas" or "P250" => DuelTier.UpgradedPistol,
        "Glock-18" or "P2000" or "USP-S" => DuelTier.StarterPistol,
        _ => null,
    };

    /// <summary>只有装备价值时的档位回退（用于拿不到武器的受害者/伤害事件）。</summary>
    internal static DuelTier TierForEquipment(int equipmentValue) => equipmentValue switch
    {
        < 1000 => DuelTier.StarterPistol,
        < 1800 => DuelTier.UpgradedPistol,
        < 2600 => DuelTier.SmgOrShotgun,
        < 3400 => DuelTier.TierTwoRifle,
        < 5000 => DuelTier.TierOneRifle,
        _ => DuelTier.Sniper,
    };

    /// <summary>对枪胜率：双方装备（六档），含 T/CT 侧修正。武器未知时回退装备价值档位。</summary>
    internal static double DuelWinProb(int equipmentOwn, int equipmentFoe, bool ownIsT)
        => DuelWinProb(equipmentOwn, equipmentFoe, ownIsT, "", "");

    /// <summary>对枪胜率：优先用武器定档；拿不到武器的一侧用装备价值回退。</summary>
    internal static double DuelWinProb(
        int equipmentOwn, int equipmentFoe, bool ownIsT, string ownWeapon, string foeWeapon)
    {
        var ownTier = TierForWeapon(ownWeapon) ?? TierForEquipment(equipmentOwn);
        var foeTier = TierForWeapon(foeWeapon) ?? TierForEquipment(equipmentFoe);
        var z = DuelBeta0
            + DuelTierOffsets[(int)ownTier]
            - DuelTierOffsets[(int)foeTier]
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
