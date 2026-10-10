namespace Cs2Demo.Analytics;

/// <summary>
/// 一场 demo 解析后的全部原始数据。计算器只读这些数据，不再碰 demo 文件。
/// 约定：列表永不为 null；<see cref="Damages"/> 为空表示没有可用的伤害数据（此时 ADR 算不出来，为 null）。
/// </summary>
public sealed class MatchData
{
    /// <summary>玩家表（已去重，不含 BOT）。</summary>
    public List<PlayerInfo> Players { get; set; } = new();
    /// <summary>有效回合表（不含热身）。</summary>
    public List<RoundInfo> Rounds { get; set; } = new();
    /// <summary>有效击杀表。</summary>
    public List<KillEvent> Kills { get; set; } = new();
    /// <summary>有效伤害表。为空表示缺伤害数据。</summary>
    public List<DamageEvent> Damages { get; set; } = new();
    /// <summary>每回合存活表。</summary>
    public List<SurvivalInfo> Survivals { get; set; } = new();
    /// <summary>元信息。</summary>
    public MatchMeta Meta { get; set; } = new();
}
