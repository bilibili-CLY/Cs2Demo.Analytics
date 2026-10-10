using Cs2Demo.Analytics;
using Xunit.Abstractions;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// 真 demo golden 测试：固定文件 tests/TestData/perfect-world.dem（de_nuke）。
/// 表中数值经两轮验证：一、总杀 135 = 总死 135（交叉轧平）；
/// 二、Nek0v0 与“狱卒与我不出门”两行用独立脚本手算复核通过。
/// 文件缺失时给出提示并返回（不编造数据）；文件被替换（SHA 对不上）则直接失败。
/// </summary>
public sealed class GoldenDemoTests
{
    private const string FileName = "perfect-world.dem";
    private const string ExpectedSha256 = "87e7057cffd7257e7111e184acb324cb6acab7006e88f83f46d03f94146045ed";

    private readonly ITestOutputHelper _output;

    public GoldenDemoTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ParseFixedDemo_MatchesGolden()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", FileName);
        if (!File.Exists(path))
        {
            _output.WriteLine($"TestData 下没有 {FileName}（193MB，未进 git；CI 从 GitHub Release 附件 dem-data 下载，本地去 Release 页取）。");
            return;
        }

        var data = DemoAnalyzer.ParseFile(path);

        Assert.Equal(ExpectedSha256, data.Meta.FileSha256);
        Assert.Equal("de_nuke", data.Meta.MapName);
        Assert.Equal(18, data.Rounds.Count);
        Assert.Equal(10, data.Players.Count);
        Assert.Equal(135, data.Kills.Count);

        var stats = StatsCalculator.CalculateAll(data);
        Assert.Equal(10, stats.Count);
        Assert.Equal(135, stats.Sum(s => s.Kills));
        Assert.Equal(135, stats.Sum(s => s.Deaths));

        foreach (var (steamId, kills, deaths, assists, damage, survived, kastRounds, kpr, adr, kast, surviving, impact, rating) in GoldenRows())
        {
            var s = stats.Single(x => x.SteamId == steamId);
            Assert.Equal(18, s.Rounds);
            Assert.Equal(kills, s.Kills);
            Assert.Equal(deaths, s.Deaths);
            Assert.Equal(assists, s.Assists);
            Assert.Equal(damage, s.TotalDamage);
            Assert.Equal(survived, s.SurvivedRounds);
            Assert.Equal(kastRounds, s.KastRounds);
            Assert.Equal(kpr, s.Kpr);
            Assert.Equal(adr, s.Adr);
            Assert.Equal(kast, s.Kast);
            Assert.Equal(surviving, s.Surviving);
            Assert.Equal(impact, s.Impact);
            Assert.Equal(rating, s.Rating20);
        }

        _output.WriteLine($"{FileName}: golden 全对上（18 回合 / 135 杀 / 10 人）。");
    }

    // SteamId, K, D, A, Damage, Survived, KastRounds, KPR, ADR, KAST, Surviving, Impact, Rating2.0
    private static (string, int, int, int, int, int, int, double, double, double, double, double, double)[] GoldenRows() =>
    [
        ("76561199771616955", 22, 9, 3, 1632, 12, 17, 1.222, 90.667, 0.944, 0.667, 2.263, 1.848),
        ("76561198080515127", 13, 15, 2, 1323, 4, 10, 0.722, 73.5, 0.556, 0.222, 1.175, 0.893),
        ("76561198436531748", 18, 13, 7, 2060, 6, 15, 1.0, 114.444, 0.833, 0.333, 1.883, 1.554),
        ("76561198311293108", 14, 17, 3, 1657, 3, 13, 0.778, 92.056, 0.722, 0.167, 1.317, 1.069),
        ("76561198132511395", 6, 17, 3, 900, 2, 11, 0.333, 50.0, 0.611, 0.111, 0.37, 0.469),
        ("76561199570455133", 10, 13, 4, 1230, 5, 14, 0.556, 68.333, 0.778, 0.278, 0.867, 0.965),
        ("76561198132857257", 9, 16, 0, 1271, 7, 13, 0.5, 70.611, 0.722, 0.389, 0.655, 0.773),
        ("76561198213920660", 19, 6, 5, 1836, 12, 16, 1.056, 102.0, 0.889, 0.667, 1.955, 1.799),
        ("76561198089280661", 11, 17, 4, 1231, 3, 13, 0.611, 68.389, 0.722, 0.167, 0.985, 0.855),
        ("76561199369495762", 13, 12, 5, 1549, 6, 15, 0.722, 86.056, 0.833, 0.333, 1.245, 1.242),
    ];
}
