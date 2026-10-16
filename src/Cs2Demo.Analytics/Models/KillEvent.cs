namespace Cs2Demo.Analytics;

/// <summary>一次有效击杀。自杀、队伤会被解析器直接丢掉，不会进入此表。</summary>
public sealed class KillEvent
{
    /// <summary>所在回合号。</summary>
    public int RoundNumber { get; set; }
    /// <summary>发生 tick，用于补枪（trade）判定。</summary>
    public int Tick { get; set; }
    /// <summary>
    /// 击杀者 SteamId。炸弹/世界击杀与队伤只记受害者死亡时为空字符串（死亡事件，
    /// 不给任何人记击杀/助攻/KAST击杀轮，Swing 只给受害者记负、不给凶手记正）。
    /// </summary>
    public string AttackerSteamId { get; set; } = "";
    /// <summary>受害者 SteamId。</summary>
    public string VictimSteamId { get; set; } = "";
    /// <summary>助攻者 SteamId，无助攻为空字符串。</summary>
    public string AssisterSteamId { get; set; } = "";
    /// <summary>该击杀是否为补枪击杀（csda 的 isTradeKill 原值）。</summary>
    public bool IsTradeKill { get; set; }
    /// <summary>该死亡是否被补枪（csda 的 isTradeDeath 原值）。</summary>
    public bool IsTradeDeath { get; set; }
    /// <summary>击杀者阵营（T/CT/空）。</summary>
    public string AttackerSide { get; set; } = Sides.Unknown;
    /// <summary>受害者阵营（T/CT/空）。</summary>
    public string VictimSide { get; set; } = Sides.Unknown;
    /// <summary>归一化后的武器名（如 AK-47、AWP、Knife），未知为 UNKNOWN，缺失为空字符串。</summary>
    public string Weapon { get; set; } = "";
    /// <summary>csda 的武器类别（pistol / rifle / smg / sniper / grenade / melee / equipment），未知为空字符串。</summary>
    public string WeaponType { get; set; } = "";
    /// <summary>是否爆头击杀（csda 原值）。</summary>
    public bool IsHeadshot { get; set; }
    /// <summary>助攻是否为闪光助攻（csda 的 isAssistedFlash 原值）。</summary>
    public bool IsAssistedFlash { get; set; }
    /// <summary>击杀者本回合初始装备总值（来自 csda playerEconomies），未知为 0。</summary>
    public int KillerEquipmentValue { get; set; }
    /// <summary>受害者本回合初始装备总值（来自 csda playerEconomies），未知为 0。</summary>
    public int VictimEquipmentValue { get; set; }
}
