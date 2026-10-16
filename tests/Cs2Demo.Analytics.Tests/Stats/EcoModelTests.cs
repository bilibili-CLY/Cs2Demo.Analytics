using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// 经济/武器难度模型（eKPR / eADR 用的对枪胜率）单元测试。
/// 系数锚定到 HLTV Rating 3.0 公开示例：同装备约 48%、一线步枪打初始手枪约 75%。
/// </summary>
public sealed class EcoModelTests
{
    [Theory]
    [InlineData(0, 0, false, 0.48)]     // 同装备、CT：约 48%
    [InlineData(0, 0, true, 0.457)]     // 同装备、T：略低
    [InlineData(2700, 400, false, 0.75)] // 一线步枪(2700) 打初始手枪(400)、CT：约 75%
    public void DuelWinProb_MatchesHltvAnchors(int own, int foe, bool ownIsT, double expected)
    {
        var p = StatsCalculator.DuelWinProb(own, foe, ownIsT);
        Assert.Equal(expected, StatsCalculator.Round3(p), 3);
    }

    [Fact]
    public void DuelWinProb_IsMonotonicInEquipment()
    {
        var poor = StatsCalculator.DuelWinProb(400, 5100, true);
        var even = StatsCalculator.DuelWinProb(3000, 3000, true);
        var rich = StatsCalculator.DuelWinProb(5100, 400, true);

        Assert.True(poor < even);
        Assert.True(even < rich);
    }

    [Theory]
    [InlineData(0.5, 1.0)]   // 五五开：难度 1
    [InlineData(0.75, 0.5)]  // 优势对枪：难度减半
    [InlineData(0.25, 1.5)]  // 劣势对枪：难度放大
    public void EcoDifficulty_ScalesAroundEvenDuel(double p, double expected)
    {
        Assert.Equal(expected, StatsCalculator.EcoDifficulty(p), 3);
    }
}
