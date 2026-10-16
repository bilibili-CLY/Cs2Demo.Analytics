using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// V1 六项：KPR/ADR/KAST/Rating2.0 直接取 csda 值，Surviving/Impact 由 csda 原料派生。
/// 用最小 MatchData（手动填 PlayerInfo.Csda）验证派生与取数逻辑，不依赖真 demo。
/// </summary>
public sealed class StatsCalculatorTests
{
    private static MatchData BuildData()
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_test" } };

        data.Players.Add(new PlayerInfo
        {
            SteamId = "1",
            Name = "P",
            Csda = new PlayerCsdaStats
            {
                KillCount = 12,
                DeathCount = 4,
                AssistCount = 3,
                HealthDamage = 850,
                KastPercent = 90.0,
                AverageKillPerRound = 1.2,
                AverageDamagePerRound = 85.0,
                HltvRating2 = 1.631,
            },
        });
        data.Players.Add(new PlayerInfo { SteamId = "2", Name = "Q" });

        for (var round = 1; round <= 10; round++)
            data.Rounds.Add(new RoundInfo { RoundNumber = round });

        return data;
    }

    [Fact]
    public void Calculate_DerivesExpectedMetrics()
    {
        var stats = StatsCalculator.Calculate(BuildData(), "1");

        Assert.Equal("1", stats.SteamId);
        Assert.Equal("P", stats.Name);
        Assert.Equal(StatsCalculator.Rating20Version, stats.Version);
        Assert.Equal(10, stats.Rounds);
        Assert.Equal(12, stats.Kills);
        Assert.Equal(4, stats.Deaths);
        Assert.Equal(3, stats.Assists);
        Assert.Equal(850, stats.TotalDamage);
        Assert.Equal(6, stats.SurvivedRounds);   // 10 - 4
        Assert.Equal(9, stats.KastRounds);        // round(90/100 * 10)
        Assert.Equal(1.2, stats.Kpr);
        Assert.Equal(85.0, stats.Adr);
        Assert.Equal(90.0, stats.Kast);           // 0~100，csda 口径
        Assert.Equal(60.0, stats.Surviving);      // (10 - 4) / 10 * 100，0~100 百分数
        Assert.Equal(2.272, stats.Impact);        // 2.13*1.2 + 0.42*0.3 - 0.41
        Assert.Equal(1.631, stats.Rating20);
    }

    [Fact]
    public void CalculateAll_CoversEveryPlayer()
    {
        var all = StatsCalculator.CalculateAll(BuildData());

        Assert.Equal(2, all.Count);
        Assert.Contains(all, s => s.SteamId == "1");
        Assert.Contains(all, s => s.SteamId == "2");
    }

    [Fact]
    public void Calculate_ZeroRounds_AllMetricsNull()
    {
        var data = new MatchData();
        data.Players.Add(new PlayerInfo { SteamId = "1", Name = "P" });

        var stats = StatsCalculator.Calculate(data, "1");

        Assert.Equal(0, stats.Rounds);
        Assert.Null(stats.Kpr);
        Assert.Null(stats.Adr);
        Assert.Null(stats.Kast);
        Assert.Null(stats.Surviving);
        Assert.Null(stats.Impact);
        Assert.Null(stats.Rating20);
    }

    [Fact]
    public void Calculate_UnknownPlayer_Throws()
    {
        Assert.Throws<ArgumentException>(() => StatsCalculator.Calculate(BuildData(), "999"));
    }

    [Fact]
    public void Calculate_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => StatsCalculator.Calculate(null!, "1"));
    }
}
