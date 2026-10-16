using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// Round Swing v2（经济/安包加权 + 贡献分摊 + 回合末残值零和分摊）单元测试。
/// 场景：1 回合，T1 击杀 C1，T 方获胜。详见 docs/formulas.md。
/// </summary>
public sealed class SwingCalculatorTests
{
    private static MatchData BuildOneRound(int evT = 0, int evCT = 0, bool flashAssist = false, bool tradeKill = false)
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_test" } };
        for (var i = 1; i <= 5; i++)
            data.Players.Add(new PlayerInfo { SteamId = "T" + i, Name = "T" + i });
        for (var i = 1; i <= 5; i++)
            data.Players.Add(new PlayerInfo { SteamId = "C" + i, Name = "C" + i });

        data.Rounds.Add(new RoundInfo
        {
            RoundNumber = 1,
            WinnerSide = Sides.Terrorist,
            TEquipmentValue = evT,
            CTEquipmentValue = evCT,
        });
        data.Kills.Add(new KillEvent
        {
            RoundNumber = 1,
            Tick = 100,
            AttackerSteamId = "T1",
            AttackerSide = Sides.Terrorist,
            AssisterSteamId = flashAssist ? "T2" : "",
            IsAssistedFlash = flashAssist,
            IsTradeKill = tradeKill,
            VictimSteamId = "C1",
            VictimSide = Sides.CounterTerrorist,
        });

        for (var i = 1; i <= 5; i++)
            data.Survivals.Add(new SurvivalInfo { RoundNumber = 1, SteamId = "T" + i, Survived = true, Side = Sides.Terrorist });
        data.Survivals.Add(new SurvivalInfo { RoundNumber = 1, SteamId = "C1", Survived = false, Side = Sides.CounterTerrorist });
        for (var i = 2; i <= 5; i++)
            data.Survivals.Add(new SurvivalInfo { RoundNumber = 1, SteamId = "C" + i, Survived = true, Side = Sides.CounterTerrorist });

        return data;
    }

    [Theory]
    [InlineData(5, 5, 0, 0, false, 0.488)]  // 公开示例：5v5 同经济 48.8%
    [InlineData(5, 4, 0, 0, false, 0.721)]  // 公开示例：5v4 72.1%
    [InlineData(5, 5, 0, 0, true, 0.694)]   // 5v5 已下包
    public void RoundWinProb_MatchesPublishedAnchors(int t, int ct, int evT, int evCT, bool bomb, double expected)
    {
        Assert.Equal(expected, StatsCalculator.Round3(SwingCalculator.RoundWinProb(t, ct, evT, evCT, bomb)), 3);
    }

    [Fact]
    public void RoundWinProb_EconomyShiftsProbability()
    {
        var even = SwingCalculator.RoundWinProb(5, 5, 0, 0, false);
        var richT = SwingCalculator.RoundWinProb(5, 5, 25000, 5000, false);

        Assert.True(richT > even);
    }

    [Fact]
    public void Compute_IsZeroSum()
    {
        var total = SwingCalculator.Compute(BuildOneRound());

        Assert.Equal(0.0, total.Values.Sum(), 6);
    }

    [Fact]
    public void Compute_KillerAndSurvivorsOutrankPassiveTeammates()
    {
        var total = SwingCalculator.Compute(BuildOneRound());

        Assert.True(total["T1"] > 0);
        Assert.True(total["T1"] > total["T2"]); // 击杀者 > 只存活的队友
        Assert.True(total["T2"] > 0);           // 存活且赢下回合
        Assert.True(total["C1"] < 0);           // 阵亡的受害者
    }

    [Fact]
    public void Compute_FlashAssister_GetsExtraCredit()
    {
        var plain = SwingCalculator.Compute(BuildOneRound());
        var flash = SwingCalculator.Compute(BuildOneRound(flashAssist: true));

        Assert.True(flash["T2"] > plain["T2"]); // 闪光助攻分到 0.10 份额
        Assert.True(flash["T1"] < plain["T1"]); // 击杀者相应少拿
    }

    [Fact]
    public void Compute_RichEconomy_ShrinksKillSwing()
    {
        var even = SwingCalculator.Compute(BuildOneRound());
        var rich = SwingCalculator.Compute(BuildOneRound(evT: 25000, evCT: 5000));

        // 经济已大幅领先时，单杀带来的胜率增量更小。
        Assert.True(rich["T1"] < even["T1"]);
    }
}
