namespace Cs2Demo.Analytics;

/// <summary>一次有效击杀。自杀、队伤会被解析器直接丢掉，不会进入此表。</summary>
public sealed class KillEvent
{
    /// <summary>所在回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>发生 tick，用于补枪（trade）判定。</summary>
    public int Tick { get; set; }
    /// <summary>击杀者 SteamId。</summary>
    public string AttackerSteamId { get; set; } = "";
    /// <summary>受害者 SteamId。</summary>
    public string VictimSteamId { get; set; } = "";
    /// <summary>助攻者 SteamId，无助攻为空字符串。</summary>
    public string AssisterSteamId { get; set; } = "";
    /// <summary>
    /// 被补枪回合记账对象：本次击杀补掉了哪一位队友的枪。
    /// 例如 A 打死 B，5 秒内 C（B 的队友）打死 A，则 C 这次击杀的 <c>TradedSteamId</c> 为 B。
    /// 无补枪为空字符串。
    /// </summary>
    public string TradedSteamId { get; set; } = "";
    /// <summary>击杀者阵营（T/CT/空）。</summary>
    public string AttackerSide { get; set; } = Sides.Unknown;
    /// <summary>受害者阵营（T/CT/空）。</summary>
    public string VictimSide { get; set; } = Sides.Unknown;
    /// <summary>归一化后的武器名（如 AK-47、AWP、Knife），未知为 UNKNOWN，缺失为空字符串。</summary>
    public string Weapon { get; set; } = "";
}
