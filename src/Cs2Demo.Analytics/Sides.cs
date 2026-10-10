namespace Cs2Demo.Analytics;

/// <summary>阵营取值说明。用字符串而不是枚举，是为了 demo 里出现未知阵营时也能表达（空字符串）。</summary>
public static class Sides
{
    /// <summary>T 阵营。</summary>
    public const string Terrorist = "T";
    /// <summary>CT 阵营。</summary>
    public const string CounterTerrorist = "CT";
    /// <summary>未知阵营（空字符串）。</summary>
    public const string Unknown = "";
}
