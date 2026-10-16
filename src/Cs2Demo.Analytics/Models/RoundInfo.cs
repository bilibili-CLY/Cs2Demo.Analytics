namespace Cs2Demo.Analytics;

/// <summary>一个回合的起止与胜方，以及本回合双方的经济信息。热身回合不会进入此表。</summary>
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
    /// <summary>T 方本回合初始装备总值（csda 的 team 装备值，按阵营归并）。</summary>
    public int TEquipmentValue { get; set; }
    /// <summary>CT 方本回合初始装备总值。</summary>
    public int CTEquipmentValue { get; set; }
    /// <summary>T 方本回合经济类型（pistol / eco / full，csda 原值），未知为空字符串。</summary>
    public string TEconomyType { get; set; } = "";
    /// <summary>CT 方本回合经济类型（pistol / eco / full，csda 原值），未知为空字符串。</summary>
    public string CTEconomyType { get; set; } = "";
    /// <summary>本回合安包 tick；未安包为 0。</summary>
    public int BombPlantedTick { get; set; }
    /// <summary>本回合拆包者 SteamId；未拆包为空字符串。</summary>
    public string DefuserSteamId { get; set; } = "";
}
