using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// Rating30 手算用例（4 回合，P 为 T，Q 为 CT；X/Y 为凑 R3 连杀的非名单受害者）。
/// 胜率函数 P(t,c)=1/(1+e^-(1.0*(t-c)-0.05))：P(5,5)=0.487503，P(5,4)=0.721115，
/// P(5,3)=0.875447，P(5,2)=0.950263，P(4,5)=0.259225（python 独立复核）。
/// 无残值，纯零和：R1：P 杀 Q（P +0.233613，Q -0.233613）；
/// R2/R4：Q 杀 P（Q +0.228278，P -0.228278）；
/// R3：P 三连杀（0.233613+0.154331+0.074817=0.462761），Q 首杀受害 -0.233613。
/// P 总 0.239818/4 回合=5.995461%→5.995；Q 总 -0.010670/4=-0.266754%→-0.267。
/// P：4 杀/2 死/伤 400/存活 {R1,R3}/KAST{R1,R3} → KPR 1.0，ADR 100.0，KAST 0.5，存活 0.5；
///     连杀 (1+9)/4/1.1693=2.138031→2.138；
///     Rating=0.0576*(1/0.6606)+0.4018*(100/72.195)+0.0238*(0.5/0.3376)
///       +0.2552*(0.5/0.7134)+0.1337*2.138031+0.2089*(1+0.059955/0.076)
///       =1.517403→1.517。
/// Q：2 杀/2 死/伤 320 → KPR 0.5，ADR 80.0；连杀 2/4/1.1693=0.427606→0.428；
///     Rating=0.961685→0.962。
/// </summary>
public sealed class StatsCalculator30Tests
{
    private const string P = "76561198000000001";
    private const string Q = "76561198000000002";
    private const string X = "76561198000000003";
    private const string Y = "76561198000000004";

    private static MatchData FourRounds()
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_mirage" } };
        data.Players.Add(new PlayerInfo { SteamId = P, Name = "TestPlayer" });
        data.Players.Add(new PlayerInfo { SteamId = Q, Name = "Other" });
        data.Rounds.Add(new RoundInfo { RoundNumber = 1, WinnerSide = Sides.Terrorist });
        data.Rounds.Add(new RoundInfo { RoundNumber = 2, WinnerSide = Sides.CounterTerrorist });
        data.Rounds.Add(new RoundInfo { RoundNumber = 3, WinnerSide = Sides.Terrorist });
        data.Rounds.Add(new RoundInfo { RoundNumber = 4, WinnerSide = Sides.CounterTerrorist });

        data.Kills.Add(new KillEvent { RoundNumber = 1, Tick = 10, AttackerSteamId = P, VictimSteamId = Q, AttackerSide = Sides.Terrorist, VictimSide = Sides.CounterTerrorist });
        data.Kills.Add(new KillEvent { RoundNumber = 2, Tick = 10, AttackerSteamId = Q, VictimSteamId = P, AttackerSide = Sides.CounterTerrorist, VictimSide = Sides.Terrorist });
        data.Kills.Add(new KillEvent { RoundNumber = 3, Tick = 10, AttackerSteamId = P, VictimSteamId = Q, AttackerSide = Sides.Terrorist, VictimSide = Sides.CounterTerrorist });
        data.Kills.Add(new KillEvent { RoundNumber = 3, Tick = 20, AttackerSteamId = P, VictimSteamId = X, AttackerSide = Sides.Terrorist, VictimSide = Sides.CounterTerrorist });
        data.Kills.Add(new KillEvent { RoundNumber = 3, Tick = 30, AttackerSteamId = P, VictimSteamId = Y, AttackerSide = Sides.Terrorist, VictimSide = Sides.CounterTerrorist });
        data.Kills.Add(new KillEvent { RoundNumber = 4, Tick = 10, AttackerSteamId = Q, VictimSteamId = P, AttackerSide = Sides.CounterTerrorist, VictimSide = Sides.Terrorist });

        data.Damages.Add(new DamageEvent { RoundNumber = 1, AttackerSteamId = P, VictimSteamId = Q, Damage = 200 });
        data.Damages.Add(new DamageEvent { RoundNumber = 3, AttackerSteamId = P, VictimSteamId = Q, Damage = 200 });
        data.Damages.Add(new DamageEvent { RoundNumber = 2, AttackerSteamId = Q, VictimSteamId = P, Damage = 160 });
        data.Damages.Add(new DamageEvent { RoundNumber = 4, AttackerSteamId = Q, VictimSteamId = P, Damage = 160 });

        data.Survivals.Add(new SurvivalInfo { RoundNumber = 1, SteamId = P, Survived = true, Side = Sides.Terrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 1, SteamId = Q, Survived = false, Side = Sides.CounterTerrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 2, SteamId = P, Survived = false, Side = Sides.Terrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 2, SteamId = Q, Survived = true, Side = Sides.CounterTerrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 3, SteamId = P, Survived = true, Side = Sides.Terrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 3, SteamId = Q, Survived = false, Side = Sides.CounterTerrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 4, SteamId = P, Survived = false, Side = Sides.Terrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 4, SteamId = Q, Survived = true, Side = Sides.CounterTerrorist });

        return data;
    }

    [Fact]
    public void Calculate30_FullCase_MatchesHandComputedValues()
    {
        var stats = StatsCalculator.Calculate30(FourRounds(), P);

        Assert.Equal(4, stats.Rounds);
        Assert.Equal(4, stats.Kills);
        Assert.Equal(2, stats.Deaths);
        Assert.Equal(0, stats.Assists);
        Assert.Equal(400, stats.TotalDamage);
        Assert.Equal(2, stats.SurvivedRounds);
        Assert.Equal(2, stats.KastRounds);
        Assert.Equal(1.0, stats.Kpr);
        Assert.Equal(100.0, stats.Adr);
        Assert.Equal(0.5, stats.Kast);
        Assert.Equal(0.5, stats.Surviving);
        Assert.Equal(2.138, stats.MultiKill);
        Assert.Equal(5.995, stats.RoundSwing);
        Assert.Equal(1.517, stats.Rating30);
    }

    [Fact]
    public void CalculateAll30_CoversEveryPlayer()
    {
        var all = StatsCalculator.CalculateAll30(FourRounds());
        Assert.Equal(2, all.Count);

        var q = all.Single(s => s.SteamId == Q);
        Assert.Equal(2, q.Kills);
        Assert.Equal(2, q.Deaths);
        Assert.Equal(0.5, q.Kpr);
        Assert.Equal(80.0, q.Adr);
        Assert.Equal(0.5, q.Kast);
        Assert.Equal(0.5, q.Surviving);
        Assert.Equal(0.428, q.MultiKill);
        Assert.Equal(-0.267, q.RoundSwing);
        Assert.Equal(0.962, q.Rating30);
    }

    [Fact]
    public void WinProb_MatchesPublishedExample()
    {
        // HLTV 公开示例：5v5 T 胜率约 48.8%。
        Assert.Equal(0.488, StatsCalculator.Round3(StatsCalculator.WinProb(5, 5)));
        // 拟合形状 slope=1.0 锁定：5v4 T 胜率约 72.1%。
        Assert.Equal(0.721, StatsCalculator.Round3(StatsCalculator.WinProb(5, 4)));
    }

    [Fact]
    public void Calculate30_NoDamageData_AdrNullButRatingStillComputed()
    {
        var data = FourRounds();
        data.Damages.Clear();

        var stats = StatsCalculator.Calculate30(data, P);

        Assert.Null(stats.TotalDamage);
        Assert.Null(stats.Adr);
        Assert.NotNull(stats.Rating30);
    }

    [Fact]
    public void Calculate30_ZeroRounds_AllMetricsNull()
    {
        var data = new MatchData();
        data.Players.Add(new PlayerInfo { SteamId = P, Name = "Nobody" });

        var stats = StatsCalculator.Calculate30(data, P);

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
        Assert.Throws<ArgumentException>(() => StatsCalculator.Calculate30(FourRounds(), "nope"));
        Assert.Throws<ArgumentNullException>(() => StatsCalculator.CalculateAll30(null!));
    }
}
