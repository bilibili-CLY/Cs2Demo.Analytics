namespace Cs2Demo.Analytics;

/// <summary>整场 demo 的元信息。</summary>
public sealed class MatchMeta
{
    /// <summary>地图名（如 de_mirage），取不到为空字符串。</summary>
    public string MapName { get; set; } = "";
    /// <summary>Demo tickrate；未知或缺失时为 0。</summary>
    public int TickRate { get; set; }
    /// <summary>常规回合上限（如 MR12 为 24）；未知时为 0。</summary>
    public int MaxRounds { get; set; }
    /// <summary>demo 文件 SHA256（流式解析时为空字符串）。</summary>
    public string FileSha256 { get; set; } = "";
}
