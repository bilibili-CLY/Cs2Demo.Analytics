namespace Cs2Demo.Analytics;

/// <summary>某玩家在某回合结束时是否存活，以及其本回合初始装备。</summary>
public sealed class SurvivalInfo
{
    /// <summary>回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>玩家 SteamId。</summary>
    public string SteamId { get; set; } = "";
    /// <summary>是否存活到回合结束。</summary>
    public bool Survived { get; set; }
    /// <summary>回合结束时阵营（T/CT/空）。</summary>
    public string Side { get; set; } = Sides.Unknown;
    /// <summary>本回合初始装备总值（csda playerEconomies 的 equipmentValue），未知为 0。</summary>
    public int EquipmentValue { get; set; }
    /// <summary>本回合经济类型（pistol / eco / full，csda 原值），未知为空字符串。</summary>
    public string EconomyType { get; set; } = "";
}
