namespace Cs2Demo.Analytics;

/// <summary>一名玩家的基础身份。SteamId 为准，名字只做展示（同一 SteamId 改名以最后一次为准）。</summary>
public sealed class PlayerInfo
{
    /// <summary>Steam64 ID 字符串。BOT（SteamID 为 0）不会进入此表。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>玩家名。</summary>
    public string Name { get; set; } = "";
    /// <summary>csda 算好的该玩家聚合值，作为各项指标的口径来源。</summary>
    public PlayerCsdaStats Csda { get; set; } = new();
}
