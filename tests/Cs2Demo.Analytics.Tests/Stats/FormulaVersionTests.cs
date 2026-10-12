using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// 公式版本标识：Calculate / Calculate30 返回的结果必须带上当前公式版本，
/// 调用方靠它判断口径。公式改动时把 StatsCalculator 上的常量往上加，本测试同步改期望。
/// </summary>
public sealed class FormulaVersionTests
{
    [Fact]
    public void Calculate_SetsRating20Version()
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_mirage" } };
        data.Players.Add(new PlayerInfo { SteamId = "1", Name = "P" });
        data.Rounds.Add(new RoundInfo { RoundNumber = 1 });

        var stats = StatsCalculator.Calculate(data, "1");

        Assert.Equal("1.0.0", stats.Version);
        Assert.Equal(StatsCalculator.Rating20Version, stats.Version);
    }

    [Fact]
    public void Calculate30_SetsRating30Version()
    {
        var data = new MatchData { Meta = new MatchMeta { MapName = "de_mirage" } };
        data.Players.Add(new PlayerInfo { SteamId = "1", Name = "P" });
        data.Rounds.Add(new RoundInfo { RoundNumber = 1 });

        var stats = StatsCalculator.Calculate30(data, "1");

        Assert.Equal("1.0.0", stats.Version);
        Assert.Equal(StatsCalculator.Rating30Version, stats.Version);
    }
}
