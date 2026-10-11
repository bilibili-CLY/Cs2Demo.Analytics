using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// 手算期望值用例。场景：10 回合，玩家 P：12 杀 / 8 死 / 3 助攻 / 总伤 850 /
/// 存活 6 回合 / KAST 9 回合。手算：
/// KPR=1.2，ADR=85.0，KAST=0.9，Surviving=0.6，
/// Impact=2.13*1.2+0.42*0.3-0.41=2.272，
/// Rating=0.0073*90+0.3591*1.2-0.5329*0.8+0.2372*2.272+0.0032*85+0.1587=1.631218→1.631。
/// </summary>
public sealed class StatsCalculatorTests
{
    private const string P = "76561198000000001";
    private const string Q = "76561198000000002";

    private static MatchData TenRounds()
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_mirage" } };
        data.Players.Add(new PlayerInfo { SteamId = P, Name = "TestPlayer" });
        data.Players.Add(new PlayerInfo { SteamId = Q, Name = "Other" });
        for (var i = 1; i <= 10; i++)
            data.Rounds.Add(new RoundInfo { RoundNumber = i });

        // 12 杀：R1×5，R2×4，R3×3
        var tick = 10;
        for (var i = 0; i < 5; i++) data.Kills.Add(new KillEvent { RoundNumber = 1, Tick = tick += 10, AttackerSteamId = P, VictimSteamId = Q });
        for (var i = 0; i < 4; i++) data.Kills.Add(new KillEvent { RoundNumber = 2, Tick = tick += 10, AttackerSteamId = P, VictimSteamId = Q });
        for (var i = 0; i < 3; i++) data.Kills.Add(new KillEvent { RoundNumber = 3, Tick = tick += 10, AttackerSteamId = P, VictimSteamId = Q });

        // 8 死（R4~R8）
        for (var r = 4; r <= 8; r++)
            data.Kills.Add(new KillEvent { RoundNumber = r, Tick = 10, AttackerSteamId = Q, VictimSteamId = P });
        for (var i = 0; i < 3; i++)
            data.Kills.Add(new KillEvent { RoundNumber = 9, Tick = 20 + i, AttackerSteamId = Q, VictimSteamId = P });

        // 3 助攻（R4）+ 1 次被补枪（R5）
        for (var i = 0; i < 3; i++)
            data.Kills.Add(new KillEvent { RoundNumber = 4, Tick = 100 + i, AttackerSteamId = Q, VictimSteamId = "X", AssisterSteamId = P });
        data.Kills.Add(new KillEvent { RoundNumber = 5, Tick = 100, AttackerSteamId = Q, VictimSteamId = "Y", TradedSteamId = P });

        // 总伤 850
        data.Damages.Add(new DamageEvent { RoundNumber = 1, AttackerSteamId = P, VictimSteamId = Q, Damage = 500 });
        data.Damages.Add(new DamageEvent { RoundNumber = 2, AttackerSteamId = P, VictimSteamId = Q, Damage = 350 });

        // 存活：R1,R2,R6,R7,R8,R10
        for (var r = 1; r <= 10; r++)
            data.Survivals.Add(new SurvivalInfo
            {
                RoundNumber = r,
                SteamId = P,
                Survived = r is 1 or 2 or 6 or 7 or 8 or 10,
            });

        return data;
    }

    [Fact]
    public void Calculate_FullCase_MatchesHandComputedValues()
    {
        var stats = StatsCalculator.Calculate(TenRounds(), P);

        Assert.Equal(10, stats.Rounds);
        Assert.Equal(12, stats.Kills);
        Assert.Equal(8, stats.Deaths);
        Assert.Equal(3, stats.Assists);
        Assert.Equal(850, stats.TotalDamage);
        Assert.Equal(6, stats.SurvivedRounds);
        Assert.Equal(9, stats.KastRounds);
        Assert.Equal(1.2, stats.Kpr);
        Assert.Equal(85.0, stats.Adr);
        Assert.Equal(0.9, stats.Kast);
        Assert.Equal(0.6, stats.Surviving);
        Assert.Equal(2.272, stats.Impact);
        Assert.Equal(1.631, stats.Rating20);
    }

    [Fact]
    public void CalculateAll_CoversEveryPlayer()
    {
        var all = StatsCalculator.CalculateAll(TenRounds());
        Assert.Equal(2, all.Count);

        var q = all.Single(s => s.SteamId == Q);
        // 夹具里 Q：12 杀（8 次打死 P + R4 3 次 + R5 1 次）/ 12 死 / 0 助攻 /
        // 无伤害无存活、有击杀回合 {4,5,6,7,8,9}。
        // Impact=2.13*1.2-0.41=2.146；
        // Rating=0.0073*60+0.3591*1.2-0.5329*1.2+0.2372*2.146+0+0.1587=0.897171→0.897。
        Assert.Equal(12, q.Kills);
        Assert.Equal(12, q.Deaths);
        Assert.Equal(1.2, q.Kpr);
        Assert.Equal(0.0, q.Adr);
        Assert.Equal(0.6, q.Kast);
        Assert.Equal(2.146, q.Impact);
        Assert.Equal(0.897, q.Rating20);
    }

    [Fact]
    public void Calculate_NoDamageData_AdrNullButRatingStillComputed()
    {
        var data = TenRounds();
        data.Damages.Clear();

        var stats = StatsCalculator.Calculate(data, P);

        Assert.Null(stats.TotalDamage);
        Assert.Null(stats.Adr);
        Assert.NotNull(stats.Rating20);
    }

    [Fact]
    public void Calculate_ZeroRounds_AllMetricsNull()
    {
        var data = new MatchData();
        data.Players.Add(new PlayerInfo { SteamId = P, Name = "Nobody" });

        var stats = StatsCalculator.Calculate(data, P);

        Assert.Equal(0, stats.Rounds);
        Assert.Null(stats.Kpr);
        Assert.Null(stats.Adr);
        Assert.Null(stats.Kast);
        Assert.Null(stats.Surviving);
        Assert.Null(stats.Impact);
        Assert.Null(stats.Rating20);
    }

    [Fact]
    public void Calculate_KastDedupesSameRound()
    {
        var data = new MatchData();
        data.Players.Add(new PlayerInfo { SteamId = P, Name = "X" });
        data.Rounds.Add(new RoundInfo { RoundNumber = 1 });
        data.Kills.Add(new KillEvent { RoundNumber = 1, Tick = 1, AttackerSteamId = P, VictimSteamId = Q });
        data.Kills.Add(new KillEvent { RoundNumber = 1, Tick = 2, AttackerSteamId = Q, VictimSteamId = "Z", AssisterSteamId = P, TradedSteamId = P });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 1, SteamId = P, Survived = true });

        var stats = StatsCalculator.Calculate(data, P);

        Assert.Equal(1, stats.KastRounds);
        Assert.Equal(1.0, stats.Kast);
    }

    [Fact]
    public void Calculate_UnknownPlayer_Throws()
    {
        Assert.Throws<ArgumentException>(() => StatsCalculator.Calculate(TenRounds(), "nope"));
        Assert.Throws<ArgumentNullException>(() => StatsCalculator.CalculateAll(null!));
    }
}
