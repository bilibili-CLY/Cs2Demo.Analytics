namespace Cs2Demo.Analytics;

/// <summary>
/// 玩家数据计算（两步 API 的第二步）：只读 <see cref="MatchData"/>，算 V1 的 6 项指标。
/// 口径见 docs/formulas.md。算不出来的指标为 null，不抛异常；
/// 传入的 data 为 null 或玩家不存在时才抛 <see cref="ArgumentException"/>。
/// Rating30 另见 <see cref="Calculate30(MatchData, string)"/>（独立方法、独立返回类型，与本方法互不影响）。
/// </summary>
public static class StatsCalculator
{
    /// <summary>Rating20 公式版本。公式有改动时往上加，调用方用它判断口径。</summary>
    public const string Rating20Version = "1.0.0";
    /// <summary>Rating30 公式版本。公式有改动时往上加，调用方用它判断口径。</summary>
    public const string Rating30Version = "1.0.0";
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

        var b = GetBase(data, steamId);

        var r = (double)rounds;
        var kpr = b.Kills / r;
        var dpr = b.Deaths / r;
        var apr = b.Assists / r;
        var kast = b.KastRounds / r;
        var surviving = b.Survived / r;
        double? adr = b.TotalDamage is null ? null : b.TotalDamage.Value / r;
        var impact = 2.13 * kpr + 0.42 * apr - 0.41;
        var rating = 0.0073 * kast * 100 + 0.3591 * kpr - 0.5329 * dpr
            + 0.2372 * impact + 0.0032 * (adr ?? 0.0) + 0.1587;

        stats.Kills = b.Kills;
        stats.Deaths = b.Deaths;
        stats.Assists = b.Assists;
        stats.TotalDamage = b.TotalDamage;
        stats.SurvivedRounds = b.Survived;
        stats.KastRounds = b.KastRounds;
        stats.Kpr = Round3(kpr);
        stats.Adr = adr is null ? null : Round3(adr.Value);
        stats.Kast = Round3(kast);
        stats.Surviving = Round3(surviving);
        stats.Impact = Round3(Math.Max(0, impact));
        stats.Rating20 = Round3(Math.Max(0, rating));
        return stats;
    }

    /// <summary>
    /// 算出所有玩家的 Rating30 统计（实验性本地近似，非 HLTV 官方值，见 docs/formulas.md）。
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
    /// 算出一名玩家的 Rating30 统计（实验性本地近似，非 HLTV 官方值，见 docs/formulas.md）。
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
            return stats; // 7 项全为 null

        var b = GetBase(data, steamId);

        var r = (double)rounds;
        var kpr = b.Kills / r;
        var kast = b.KastRounds / r;
        var surviving = b.Survived / r;
        double? adr = b.TotalDamage is null ? null : b.TotalDamage.Value / r;

        // 连杀：同回合击杀数分组（5 杀封顶），权重 1/4/9/16/25。
        var mkCount = new int[6];
        foreach (var g in data.Kills
            .Where(k => k.AttackerSteamId == steamId && k.VictimSteamId != steamId)
            .GroupBy(k => k.RoundNumber))
        {
            var n = g.Count();
            mkCount[n >= 5 ? 5 : n]++;
        }

        var mkRaw = (mkCount[1] + 4.0 * mkCount[2] + 9.0 * mkCount[3]
            + 16.0 * mkCount[4] + 25.0 * mkCount[5]) / r;

        var swingByPlayer = ComputeSwing(data);
        var swingTotal = swingByPlayer.TryGetValue(steamId, out var sw) ? sw : 0.0;
        var swingAvg = swingTotal / r; // 胜率小数/回合
        var swingOut = swingAvg * 100.0; // 百分点/回合

        var rating = WeightKpr * (kpr / AvgKpr)
            + WeightAdr * ((adr ?? 0.0) / AvgAdr)
            + WeightSurviving * (surviving / AvgSurviving)
            + WeightKast * (kast / AvgKast)
            + WeightMultiKill * (mkRaw / AvgMultiKill)
            + WeightSwing * (1.0 + swingAvg / SwingScale);

        stats.Kills = b.Kills;
        stats.Deaths = b.Deaths;
        stats.Assists = b.Assists;
        stats.TotalDamage = b.TotalDamage;
        stats.SurvivedRounds = b.Survived;
        stats.KastRounds = b.KastRounds;
        stats.Kpr = Round3(kpr);
        stats.Adr = adr is null ? null : Round3(adr.Value);
        stats.Kast = Round3(kast);
        stats.Surviving = Round3(surviving);
        stats.MultiKill = Round3(mkRaw / AvgMultiKill);
        stats.RoundSwing = Round3(swingOut);
        stats.Rating30 = Round3(Math.Max(0, rating));
        return stats;
    }

    // ---- Rating30 拟合常量（10 图 / 100 样本 NNLS 无截距拟合，见 docs/formulas.md） ----

    /// <summary>样本均值：回合击杀。</summary>
    internal const double AvgKpr = 0.6606;
    /// <summary>样本均值：回合伤害。</summary>
    internal const double AvgAdr = 72.195;
    /// <summary>样本均值：存活率。</summary>
    internal const double AvgSurviving = 0.3376;
    /// <summary>样本均值：KAST。</summary>
    internal const double AvgKast = 0.7134;
    /// <summary>样本均值：连杀。</summary>
    internal const double AvgMultiKill = 1.1693;
    /// <summary>Swing 缩放：官方 Swing ≈ 0.658 × 本地 Swing，0.05 口径换算得 0.076。</summary>
    internal const double SwingScale = 0.076;

    /// <summary>KPR 项权重。</summary>
    internal const double WeightKpr = 0.0576;
    /// <summary>ADR 项权重。</summary>
    internal const double WeightAdr = 0.4018;
    /// <summary>存活项权重。</summary>
    internal const double WeightSurviving = 0.0238;
    /// <summary>KAST 项权重。</summary>
    internal const double WeightKast = 0.2552;
    /// <summary>连杀项权重。</summary>
    internal const double WeightMultiKill = 0.1337;
    /// <summary>Swing 项权重（居中形态）。</summary>
    internal const double WeightSwing = 0.2089;

    /// <summary>
    /// T 方回合胜率函数（拟合形状：slope=1.0/offset=-0.05，无残值、无经济加权）。
    /// 5v5 时约 0.488，与 HLTV 公开示例 48.8% 一致；见 docs/formulas.md。
    /// </summary>
    internal static double WinProb(int tAlive, int ctAlive)
    {
        var x = 1.0 * (tAlive - ctAlive) - 0.05;
        return 1.0 / (1.0 + Math.Exp(-x));
    }

    /// <summary>两套计算共用的原始计数（与 V1 口径完全一致）。</summary>
    private sealed record BaseCounts(
        int Kills, int Deaths, int Assists, int? TotalDamage, int Survived, int KastRounds);

    private static BaseCounts GetBase(MatchData data, string steamId)
    {
        var kills = 0;
        var deaths = 0;
        var assists = 0;
        foreach (var k in data.Kills)
        {
            if (k.AttackerSteamId == steamId && k.VictimSteamId != steamId)
                kills++;
            if (k.VictimSteamId == steamId)
                deaths++;
            if (k.AssisterSteamId == steamId)
                assists++;
        }

        int? totalDamage = null;
        if (data.Damages.Count > 0)
        {
            var sum = 0;
            foreach (var d in data.Damages)
            {
                if (d.AttackerSteamId == steamId && d.Damage > 0)
                    sum += d.Damage;
            }

            totalDamage = sum;
        }

        var survived = 0;
        foreach (var s in data.Survivals)
        {
            if (s.SteamId == steamId && s.Survived)
                survived++;
        }

        var kastRounds = new HashSet<int>();
        foreach (var k in data.Kills)
        {
            if (k.AttackerSteamId == steamId || k.AssisterSteamId == steamId || k.TradedSteamId == steamId)
                kastRounds.Add(k.RoundNumber);
        }

        foreach (var s in data.Survivals)
        {
            if (s.SteamId == steamId && s.Survived)
                kastRounds.Add(s.RoundNumber);
        }

        return new BaseCounts(kills, deaths, assists, totalDamage, survived, kastRounds.Count);
    }

    /// <summary>
    /// 简化 Swing：逐杀按存活数查 <see cref="WinProb"/>，击杀者得正、受害者得负（零和，无残值）。
    /// 无经济、无安包/残局/拆包加权（见 docs/formulas.md 已知缺口）。
    /// </summary>
    private static Dictionary<string, double> ComputeSwing(MatchData data)
    {
        var total = data.Players.ToDictionary(p => p.SteamId, _ => 0.0);

        foreach (var group in data.Kills.GroupBy(k => k.RoundNumber))
        {
            var ordered = group.OrderBy(k => k.Tick).ToList();
            var t = 5;
            var c = 5;
            var p = WinProb(t, c);

            foreach (var k in ordered)
            {
                if (k.AttackerSteamId == k.VictimSteamId)
                    continue;

                // 受害方：未知时按“队伤已过滤，凶手必为对方”反推；两边都未知则跳过。
                var vSide = k.VictimSide;
                if (vSide != Sides.Terrorist && vSide != Sides.CounterTerrorist)
                {
                    vSide = k.AttackerSide == Sides.Terrorist ? Sides.CounterTerrorist
                        : k.AttackerSide == Sides.CounterTerrorist ? Sides.Terrorist
                        : Sides.Unknown;
                }

                if (vSide != Sides.Terrorist && vSide != Sides.CounterTerrorist)
                    continue;

                if (vSide == Sides.Terrorist)
                    t = Math.Max(0, t - 1);
                else
                    c = Math.Max(0, c - 1);

                var after = WinProb(t, c);
                var dT = after - p; // T 视角胜率变化
                var victimSwing = vSide == Sides.Terrorist ? dT : -dT;

                if (total.ContainsKey(k.VictimSteamId))
                    total[k.VictimSteamId] += victimSwing;
                if (total.ContainsKey(k.AttackerSteamId))
                    total[k.AttackerSteamId] += -victimSwing;

                p = after;
            }
        }

        return total;
    }

    internal static double Round3(double value) => Math.Round(value * 1000) / 1000;
}
