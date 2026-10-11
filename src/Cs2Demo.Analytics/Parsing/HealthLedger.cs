namespace Cs2Demo.Analytics;

/// <summary>
/// 受害者血条台账：把 GOTV 上报的毛伤害折成真实扣血（纯逻辑，可单测）。
/// 每（回合，受害者）从 100 起步：真实伤害 = 上次剩余 - clamp(上报剩余血量, 0, 上次剩余)。
/// 溢出伤害（比如人只有 30 血却打出 100 伤害）只记 30。
/// </summary>
internal sealed class HealthLedger
{
    private readonly Dictionary<(int Round, string Victim), int> _remaining = new();

    /// <returns>本次真实扣血（可能为 0）。</returns>
    public int Apply(int round, string victimSteamId, int reportedHealth)
    {
        var key = (round, victimSteamId);
        var prev = _remaining.TryGetValue(key, out var v) ? v : 100;
        var post = Math.Clamp(reportedHealth, 0, prev);
        _remaining[key] = post;
        return Math.Max(0, prev - post);
    }

    public void DiscardRound(int roundNumber)
    {
        foreach (var key in _remaining.Keys.Where(k => k.Round == roundNumber).ToList())
            _remaining.Remove(key);
    }
}
