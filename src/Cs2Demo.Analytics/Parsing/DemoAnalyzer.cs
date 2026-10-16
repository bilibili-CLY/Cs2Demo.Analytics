using System.Security.Cryptography;

namespace Cs2Demo.Analytics;

/// <summary>
/// Demo 解析入口。以 csda（akiver/cs-demo-analyzer）作为唯一解析后端：
/// 先调用 csda 拿到 JSON，再映射为 <see cref="MatchData"/>（只解析，不计算指标）。
/// </summary>
public static class DemoAnalyzer
{
    /// <summary>
    /// 手动指定 csda 可执行文件路径（自备二进制或非常规部署时用）。
    /// 为 null（默认）时使用随包二进制自动定位。
    /// </summary>
    public static string? CsdaExecutablePath { get; set; }

    /// <summary>解析一个 .dem 文件，得到原始比赛数据（不做 Rating/ADR 等计算）。</summary>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> 为 null。</exception>
    /// <exception cref="FileNotFoundException">文件不存在，或找不到 csda 可执行文件。</exception>
    /// <exception cref="InvalidOperationException">csda 执行失败、demo 损坏/截断，或结果中无玩家/回合。</exception>
    public static MatchData ParseFile(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Demo 文件不存在。", path);

        var csdaPath = CsdaExecutablePath ?? CsdaBinaryLocator.Locate();
        var json = CsdaRunner.Run(path, csdaPath);
        var data = CsdaMatchMapper.Map(json);

        data.Meta.FileSha256 = Sha256OfFile(path);

        if (data.Players.Count == 0 || data.Rounds.Count == 0)
            throw new InvalidOperationException("解析结果不含玩家或回合数据，demo 可能不完整。");

        return data;
    }

    private static string Sha256OfFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }
}
