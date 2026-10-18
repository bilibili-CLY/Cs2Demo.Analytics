using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// Rating30 七项：KPR/ADR/KAST/Surviving/MultiKill 由 csda 原料派生，RoundSwing/Rating30 由本库计算。
/// 4 回合小场景：P 每回合击杀 Q 一次，手算值见断言注释。
/// </summary>
public sealed class StatsCalculator30Tests
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
                KillCount = 4,
                DeathCount = 2,
                AssistCount = 1,
                HealthDamage = 400,
                KastPercent = 50.0,
                AverageKillPerRound = 1.0,
                AverageDamagePerRound = 100.0,
                OneKillCount = 4,
            },
        });
        data.Players.Add(new PlayerInfo { SteamId = "2", Name = "Q" });
        data.Players.Add(new PlayerInfo { SteamId = "3", Name = "X" });
        data.Players.Add(new PlayerInfo { SteamId = "4", Name = "Y" });

        for (var round = 1; round <= 4; round++)
        {
            data.Rounds.Add(new RoundInfo { RoundNumber = round, WinnerSide = Sides.Terrorist });
            data.Kills.Add(new KillEvent
            {
                RoundNumber = round,
                Tick = round * 100,
                AttackerSteamId = "1",
                AttackerSide = Sides.Terrorist,
                VictimSteamId = "2",
                VictimSide = Sides.CounterTerrorist,
            });
        }

        return data;
    }

    [Fact]
    public void Calculate30_DerivesExpectedMetrics()
    {
        var s = StatsCalculator.Calculate30(BuildData(), "1");

        Assert.Equal(StatsCalculator.Rating30Version, s.Version);
        Assert.Equal(4, s.Rounds);
        Assert.Equal(4, s.Kills);
        Assert.Equal(2, s.Deaths);
        Assert.Equal(1, s.Assists);
        Assert.Equal(400, s.TotalDamage);
        Assert.Equal(2, s.SurvivedRounds);   // 4 - 2
        Assert.Equal(2, s.KastRounds);        // round(50/100 * 4)
        Assert.Equal(1.0, s.Kpr);
        Assert.Equal(100.0, s.Adr);
        Assert.Equal(1.085, s.EKpr!.Value, 3); // 无经济数据（equipment=0）时每杀难度系数 1.085
        Assert.Equal(0.0, s.EAdr);
        Assert.Equal(50.0, s.Kast);
        Assert.Equal(50.0, s.Surviving);      // (4 - 2) / 4 * 100，0~100 百分数
        Assert.Equal(0.0, s.MultiKill);       // 无多杀回合（每人每回合只杀 1 个）
        Assert.Equal(23.361, s.RoundSwing!.Value, 3); // 每杀 +0.2336 胜率/回合
        Assert.Equal(1.654, s.Rating30!.Value, 3);
    }

    [Fact]
    public void Calculate30_MultiKillIsMultiKillRoundPercent()
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_test" } };
        data.Players.Add(new PlayerInfo
        {
            SteamId = "1",
            Name = "P",
            Csda = new PlayerCsdaStats
            {
                KillCount = 7,
                OneKillCount = 2,
                TwoKillCount = 1,
                ThreeKillCount = 1,
            },
        });
        for (var round = 1; round <= 4; round++)
            data.Rounds.Add(new RoundInfo { RoundNumber = round });

        var s = StatsCalculator.Calculate30(data, "1");

        Assert.Equal(50.0, s.MultiKill); // 多杀回合 2（2K+3K）/ 4 = 50%
    }

    [Fact]
    public void Calculate30_VictimRoundSwingIsNegative()
    {
        var s = StatsCalculator.Calculate30(BuildData(), "2");

        Assert.Equal(-23.361, s.RoundSwing!.Value, 3);
        Assert.Equal(0.0, s.Rating30); // 负 swing 压到 0 以下，钳为 0
    }

    [Theory]
    [InlineData(5, 5, 0.488)]
    [InlineData(5, 4, 0.721)]
    internal void WinProb_MatchesPublishedExample(int tAlive, int ctAlive, double expected)
    {
        Assert.Equal(expected, StatsCalculator.Round3(StatsCalculator.WinProb(tAlive, ctAlive)), 3);
    }

    [Fact]
    public void Calculate30_ZeroRounds_AllMetricsNull()
    {
        var data = new MatchData();
        data.Players.Add(new PlayerInfo { SteamId = "1", Name = "P" });

        var stats = StatsCalculator.Calculate30(data, "1");

        Assert.Equal(0, stats.Rounds);
        Assert.Null(stats.Kpr);
        Assert.Null(stats.Adr);
        Assert.Null(stats.Kast);
        Assert.Null(stats.Surviving);
        Assert.Null(stats.MultiKill);
        Assert.Null(stats.RoundSwing);
        Assert.Null(stats.Rating30);
    }

    [Fact]
    public void Calculate30_UnknownPlayer_Throws()
    {
        Assert.Throws<ArgumentException>(() => StatsCalculator.Calculate30(BuildData(), "999"));
    }
}
