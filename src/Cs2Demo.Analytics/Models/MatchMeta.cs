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
    /// <summary>解析平台（csda 的 source，如 perfectworld / faceit / 5eplay）；未知为空字符串。</summary>
    public string Source { get; set; } = "";
    /// <summary>整场 demo 时长（秒），取 csda 的 duration；缺失或非法时为 0。</summary>
    public int DurationSeconds { get; set; }
    /// <summary>demo header 里的服务器名（可能含中文）；缺失为空字符串。</summary>
    public string ServerName { get; set; } = "";
    /// <summary>csda 的 date（实测取 demo 文件修改时间，不一定是真实比赛时间）；缺失或非法时为 null。</summary>
    public DateTimeOffset? MatchDate { get; set; }
    /// <summary>demo 总 tick 数；未知时为 0。</summary>
    public int TickCount { get; set; }
}
