using Cs2Demo.Analytics;
using Xunit.Abstractions;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// docs/demo 下的真 demo golden 测试（docs/demo 不进 git，文件缺失时给提示并跳过，不编造数据）。
/// 期望值由本库实跑 csda 得到，并交叉轧平：ADR = TotalDamage / 回合数、KPR = Kills / 回合数。
/// </summary>
public sealed class DocsDemoGoldenTests
{
    private readonly ITestOutputHelper _output;

    public DocsDemoGoldenTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // —— 4128951568_1000003329.dem（de_ancient，21 回合） ——

    [Fact]
    public void ParseDocsDemo_4128951568_MatchesGolden20()
    {
        var data = Parse("4128951568_1000003329.dem", "912147b76e8598bf2bb61651a0a7037f33c9e38579c59f131a00cc66f50a86aa");
        if (data is null)
            return;

        Assert.Equal("de_ancient", data.Meta.MapName);
        Assert.Equal(21, data.Rounds.Count);
        Assert.Equal(10, data.Players.Count);
        Assert.Equal(152, data.Kills.Count);

        var s = StatsCalculator.Calculate(data, "76561199645551476");

        Assert.Equal(StatsCalculator.Rating20Version, s.Version);
        Assert.Equal(21, s.Rounds);
        Assert.Equal(27, s.Kills);
        Assert.Equal(17, s.Deaths);
        Assert.Equal(3, s.Assists);
        Assert.Equal(2671, s.TotalDamage);
        Assert.Equal(4, s.SurvivedRounds);
        Assert.Equal(17, s.KastRounds);
        Assert.Equal(1.286, s.Kpr);          // 27 / 21
        Assert.Equal(127.19, s.Adr);         // 2671 / 21
        Assert.Equal(80.952, s.Kast);
        Assert.Equal(19.048, s.Surviving);   // (21 - 17) / 21 * 100
        Assert.Equal(2.389, s.Impact);
        Assert.Equal(1.754, s.Rating20);
    }

    [Fact]
    public void ParseDocsDemo_4128951568_MatchesGolden30()
    {
        var data = Parse("4128951568_1000003329.dem", "912147b76e8598bf2bb61651a0a7037f33c9e38579c59f131a00cc66f50a86aa");
        if (data is null)
            return;

        var s = StatsCalculator.Calculate30(data, "76561199645551476");

        Assert.Equal(StatsCalculator.Rating30Version, s.Version);
        Assert.Equal(21, s.Rounds);
        Assert.Equal(27, s.Kills);
        Assert.Equal(17, s.Deaths);
        Assert.Equal(3, s.Assists);
        Assert.Equal(2671, s.TotalDamage);
        Assert.Equal(4, s.SurvivedRounds);
        Assert.Equal(17, s.KastRounds);
        Assert.Equal(1.286, s.Kpr);
        Assert.Equal(127.19, s.Adr);
        Assert.Equal(1.413, s.EKpr);
        Assert.Equal(136.181, s.EAdr);
        Assert.Equal(80.952, s.Kast);
        Assert.Equal(19.048, s.Surviving);
        Assert.Equal(47.619, s.MultiKill);
        Assert.Equal(3.119, s.RoundSwing);
        Assert.Equal(1.829, s.Rating30);
    }

    // —— 4129333648_1000003329.dem（de_dust2，22 回合） ——

    [Fact]
    public void ParseDocsDemo_4129333648_MatchesGolden20()
    {
        var data = Parse("4129333648_1000003329.dem", "dfbe6f0a3b324905d064f6cff9ab228e5997780e891ef35a42c0f3872fdad115");
        if (data is null)
            return;

        Assert.Equal("de_dust2", data.Meta.MapName);
        Assert.Equal(22, data.Rounds.Count);
        Assert.Equal(10, data.Players.Count);
        Assert.Equal(135, data.Kills.Count);

        var s = StatsCalculator.Calculate(data, "76561198436531748");

        Assert.Equal(StatsCalculator.Rating20Version, s.Version);
        Assert.Equal(22, s.Rounds);
        Assert.Equal(16, s.Kills);
        Assert.Equal(11, s.Deaths);
        Assert.Equal(1, s.Assists);
        Assert.Equal(1482, s.TotalDamage);
        Assert.Equal(11, s.SurvivedRounds);
        Assert.Equal(17, s.KastRounds);
        Assert.Equal(0.727, s.Kpr);          // 16 / 22
        Assert.Equal(67.364, s.Adr);         // 1482 / 22
        Assert.Equal(77.273, s.Kast);
        Assert.Equal(50.0, s.Surviving);     // (22 - 11) / 22 * 100
        Assert.Equal(1.158, s.Impact);
        Assert.Equal(1.208, s.Rating20);
    }

    [Fact]
    public void ParseDocsDemo_4129333648_MatchesGolden30()
    {
        var data = Parse("4129333648_1000003329.dem", "dfbe6f0a3b324905d064f6cff9ab228e5997780e891ef35a42c0f3872fdad115");
        if (data is null)
            return;

        var s = StatsCalculator.Calculate30(data, "76561198436531748");

        Assert.Equal(StatsCalculator.Rating30Version, s.Version);
        Assert.Equal(22, s.Rounds);
        Assert.Equal(16, s.Kills);
        Assert.Equal(11, s.Deaths);
        Assert.Equal(1, s.Assists);
        Assert.Equal(1482, s.TotalDamage);
        Assert.Equal(11, s.SurvivedRounds);
        Assert.Equal(17, s.KastRounds);
        Assert.Equal(0.727, s.Kpr);
        Assert.Equal(67.364, s.Adr);
        Assert.Equal(0.663, s.EKpr);
        Assert.Equal(63.89, s.EAdr);
        Assert.Equal(77.273, s.Kast);
        Assert.Equal(50.0, s.Surviving);
        Assert.Equal(13.636, s.MultiKill);
        Assert.Equal(5.587, s.RoundSwing);
        Assert.Equal(1.306, s.Rating30);
    }

    // 从测试输出目录往上找仓库根（含 Cs2Demo.Analytics.slnx），再拼 docs/demo/<文件名>。
    // 文件不存在时给出提示并返回 null（调用方直接结束，视为跳过）。
    private MatchData? Parse(string fileName, string expectedSha256)
    {
        var path = FindDemo(fileName);
        if (path is null)
        {
            _output.WriteLine($"docs/demo 下没有 {fileName}（不进 git），跳过。");
            return null;
        }

        var data = DemoAnalyzer.ParseFile(path);
        Assert.Equal(expectedSha256, data.Meta.FileSha256);
        return data;
    }

    private static string? FindDemo(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Cs2Demo.Analytics.slnx")))
            {
                var path = Path.Combine(dir.FullName, "docs", "demo", fileName);
                return File.Exists(path) ? path : null;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
