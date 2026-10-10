namespace Cs2Demo.Analytics;

/// <summary>整场 demo 的元信息。</summary>
public sealed class MatchMeta
{
    /// <summary>地图名（如 de_mirage），取不到为空字符串。</summary>
    public string MapName { get; set; } = "";
    /// <summary>demo 文件 SHA256（流式解析时为空字符串）。</summary>
    public string FileSha256 { get; set; } = "";
}
