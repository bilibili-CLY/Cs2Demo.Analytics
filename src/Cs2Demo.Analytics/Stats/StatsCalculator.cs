namespace Cs2Demo.Analytics;

/// <summary>
/// 玩家数据计算（两步 API 的第二步）：只读 <see cref="MatchData"/>，算 V1 的 6 项指标。
/// 口径见 docs/formulas.md。算不出来的指标为 null，不抛异常；
/// 传入的 data 为 null 或玩家不存在时才抛 <see cref="ArgumentException"/>。
/// </summary>
public static class StatsCalculator
{
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
        };

        if (rounds <= 0)
            return stats; // 6 项全为 null

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

        var r = (double)rounds;
        var kpr = kills / r;
        var dpr = deaths / r;
        var apr = assists / r;
        var kast = kastRounds.Count / r;
        var surviving = survived / r;
        double? adr = totalDamage is null ? null : totalDamage.Value / r;
        var impact = 2.13 * kpr + 0.42 * apr - 0.41;
        var rating = 0.0073 * kast * 100 + 0.3591 * kpr - 0.5329 * dpr
            + 0.2372 * impact + 0.0032 * (adr ?? 0.0) + 0.1587;

        stats.Kills = kills;
        stats.Deaths = deaths;
        stats.Assists = assists;
        stats.TotalDamage = totalDamage;
        stats.SurvivedRounds = survived;
        stats.KastRounds = kastRounds.Count;
        stats.Kpr = Round3(kpr);
        stats.Adr = adr is null ? null : Round3(adr.Value);
        stats.Kast = Round3(kast);
        stats.Surviving = Round3(surviving);
        stats.Impact = Round3(Math.Max(0, impact));
        stats.Rating20 = Round3(Math.Max(0, rating));
        return stats;
    }

    internal static double Round3(double value) => Math.Round(value * 1000) / 1000;
}
