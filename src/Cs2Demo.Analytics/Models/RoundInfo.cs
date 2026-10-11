namespace Cs2Demo.Analytics;

/// <summary>一个回合的起止与胜方。热身回合不会进入此表。</summary>
public sealed class RoundInfo
{
    /// <summary>回合号，从 1 开始连续编号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>胜方。<see cref="Sides.Terrorist"/> / <see cref="Sides.CounterTerrorist"/>，未知为 <see cref="Sides.Unknown"/>。</summary>
    public string WinnerSide { get; set; } = Sides.Unknown;
    /// <summary>回合开始 tick。</summary>
    public int StartTick { get; set; }
    /// <summary>回合结束 tick。demo 尾部损坏被丢弃的回合不会进入此表。</summary>
    public int EndTick { get; set; }
}
