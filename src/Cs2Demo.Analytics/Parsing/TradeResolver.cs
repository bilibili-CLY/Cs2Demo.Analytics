namespace Cs2Demo.Analytics;

/// <summary>
/// 补枪（trade）判定：纯函数，可单测。
/// 口径：同一回合内，当前击杀发生前 <see cref="WindowTicks"/> 个 tick 内，
/// 若有人打死了“当前这次的受害者”，且那次击杀的受害方等于本次击杀的攻击方，
/// 则本次击杀补掉了那一枪，记被补者为那次击杀的受害者。
/// </summary>
internal static class TradeResolver
{
    /// <summary>补枪回溯窗口：5 秒，按 64 tick/秒折算。</summary>
    public const int WindowTicks = 5 * 64;

    public static string Resolve(
        IReadOnlyList<KillEvent> priorKillsInSameRound,
        string currentVictimSteamId,
        string currentAttackerSide,
        int currentTick)
    {
        for (var i = priorKillsInSameRound.Count - 1; i >= 0; i--)
        {
            var prev = priorKillsInSameRound[i];
            if (currentTick - prev.Tick > WindowTicks)
                break;
            if (prev.AttackerSteamId == currentVictimSteamId &&
                string.Equals(prev.VictimSide, currentAttackerSide, StringComparison.OrdinalIgnoreCase))
                return prev.VictimSteamId;
        }

        return "";
    }
}
