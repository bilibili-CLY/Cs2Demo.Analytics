using Cs2Demo.Analytics;
using Xunit.Abstractions;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// 真 demo golden 测试：固定文件 tests/TestData/perfect-world.dem（de_nuke）。
/// 口径自 V2 起改用 csda（akiver/cs-demo-analyzer）：KPR/ADR/KAST/Rating2.0 直接取 csda 值，
/// Surviving/Impact 由 csda 原料派生，MultiKill（多杀回合占比%）由 csda 连杀回合数派生，RoundSwing/Rating30 由本库计算。
/// 期望值由本库实跑 csda 得到，并交叉轧平：总杀 135 = 总死 135；每人 ADR = HealthDamage / 回合数。
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

    [Fact]
    public void ParseFixedDemo_MatchesGolden30()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", FileName);
        if (!File.Exists(path))
        {
            _output.WriteLine($"TestData 下没有 {FileName}，跳过 Rating30 golden。");
            return;
        }

        var data = DemoAnalyzer.ParseFile(path);
        var stats = StatsCalculator.CalculateAll30(data);

        foreach (var (steamId, multiKill, swing, rating30, eKpr, eAdr) in GoldenRows30())
        {
            var s = stats.Single(x => x.SteamId == steamId);
            Assert.Equal(multiKill, s.MultiKill);
            Assert.Equal(swing, s.RoundSwing);
            Assert.Equal(rating30, s.Rating30);
            Assert.Equal(eKpr, s.EKpr);
            Assert.Equal(eAdr, s.EAdr);
        }

        _output.WriteLine($"{FileName}: Rating30 golden 全对上。");
    }

    // SteamId, K, D, A, Damage, Survived, KastRounds, KPR, ADR, KAST, Surviving%, Impact, Rating2.0
    private static (string, int, int, int, int, int, int, double, double, double, double, double, double)[] GoldenRows() =>
    [
        ("76561198080515127", 13, 15, 2, 1313, 3, 11, 0.722, 72.944, 61.111, 16.667, 1.175, 0.932),
        ("76561198089280661", 11, 17, 4, 1220, 1, 12, 0.611, 67.778, 66.667, 5.556, 0.985, 0.812),
        ("76561198132511395", 6, 17, 3, 891, 1, 11, 0.333, 49.5, 61.111, 5.556, 0.37, 0.467),
        ("76561198132857257", 9, 16, 0, 1249, 2, 12, 0.5, 69.389, 66.667, 11.111, 0.655, 0.729),
        ("76561198213920660", 19, 6, 5, 1825, 12, 16, 1.056, 101.389, 88.889, 66.667, 1.955, 1.797),
        ("76561198311293108", 14, 17, 3, 1633, 1, 13, 0.778, 90.722, 72.222, 5.556, 1.317, 1.065),
        ("76561198436531748", 18, 13, 7, 2037, 5, 15, 1.0, 113.167, 83.333, 27.778, 1.883, 1.55),
        ("76561199369495762", 13, 12, 5, 1539, 6, 15, 0.722, 85.5, 83.333, 33.333, 1.245, 1.24),
        ("76561199570455133", 10, 13, 4, 1212, 5, 14, 0.556, 67.333, 77.778, 27.778, 0.867, 0.962),
        ("76561199771616955", 22, 9, 3, 1600, 9, 16, 1.222, 88.889, 88.889, 50.0, 2.263, 1.801),
    ];

    // SteamId, MultiKill%（多杀回合占比）, RoundSwing, Rating30, eKPR, eADR
    private static (string, double, double, double, double, double)[] GoldenRows30() =>
    [
        // 3.2.0：经济模型改为六档装备 + 补枪窗口自算，下列数值随口径一起更新。
        ("76561198080515127", 16.667, -0.357, 1.046, 0.856, 87.049),
        ("76561198089280661", 11.111, -2.968, 0.828, 0.735, 76.172),
        ("76561198132511395", 0.0, -9.525, 0.309, 0.367, 57.826),
        ("76561198132857257", 5.556, -4.413, 0.713, 0.588, 78.934),
        ("76561198213920660", 38.889, 9.142, 2.016, 1.174, 108.57),
        ("76561198311293108", 27.778, 6.059, 1.45, 0.796, 97.181),
        ("76561198436531748", 33.333, 0.251, 1.418, 0.882, 105.967),
        ("76561199369495762", 5.556, 0.657, 1.155, 0.838, 97.937),
        ("76561199570455133", 16.667, -4.576, 0.86, 0.53, 68.478),
        ("76561199771616955", 27.778, 5.729, 1.663, 1.193, 85.478),
    ];
}
