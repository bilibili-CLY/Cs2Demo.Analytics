namespace Cs2Demo.Analytics;

/// <summary>
/// 解析过程中的累加器：只操作本库自己的 DTO，不碰 DemoFile 类型，所以可以直接单测。
/// 自杀、队伤、热身由调用方（DemoAnalyzer）在送入前过滤。
/// </summary>
internal sealed class MatchBuilder
{
    private readonly Dictionary<string, PlayerInfo> _players = new(StringComparer.Ordinal);
    private readonly List<RoundInfo> _rounds = new();
    private readonly List<KillEvent> _kills = new();
    private readonly List<DamageEvent> _damages = new();
    private readonly List<SurvivalInfo> _survivals = new();
    private readonly HealthLedger _ledger = new();

    private int _currentRound;

    public MatchMeta Meta { get; } = new();

    internal int CurrentRound => _currentRound;

    public void AddPlayer(string steamId, string name)
    {
        if (string.IsNullOrEmpty(steamId))
            return;
        _players[steamId] = new PlayerInfo { SteamId = steamId, Name = name };
    }

    public void StartRound(int startTick)
    {
        _currentRound++;
        _rounds.Add(new RoundInfo { RoundNumber = _currentRound, StartTick = startTick });
    }

    public void EnsureRound(int startTick = 0)
    {
        if (_currentRound == 0)
            StartRound(startTick);
    }

    public void EndRound(string winnerSide, int endTick)
    {
        EnsureRound();
        _rounds[^1].WinnerSide = winnerSide;
        _rounds[^1].EndTick = endTick;
    }

    public void AddKill(KillEvent kill)
    {
        kill.TradedSteamId = TradeResolver.Resolve(
            _kills.Where(k => k.RoundNumber == kill.RoundNumber).ToList(),
            kill.VictimSteamId,
            kill.AttackerSide,
            kill.Tick);
        _kills.Add(kill);
    }

    /// <summary>送入一次伤害事件；reportedHealth 为 GOTV 上报的受害者剩余血量。折出来 0 伤害的不记录。</summary>
    public void AddDamage(DamageEvent evt, int reportedHealth)
    {
        var real = TrackHealth(evt.RoundNumber, evt.VictimSteamId, reportedHealth);
        if (real <= 0)
            return;
        evt.Damage = real;
        _damages.Add(evt);
    }

    /// <summary>
    /// 只跟血条、不记伤害：自伤/队伤/世界伤害也要走这里，否则下一次敌人命中会把这部分
    /// 误算到敌人头上。返回本次真实扣血（可能为 0）。
    /// </summary>
    public int TrackHealth(int round, string victimSteamId, int reportedHealth)
        => _ledger.Apply(round, victimSteamId, reportedHealth);

    /// <summary>记入一条已折好的敌方伤害（<see cref="Damage"/> 必须已填好真实扣血）。</summary>
    public void RecordDamage(DamageEvent evt) => _damages.Add(evt);

    public void AddSurvival(SurvivalInfo survival) => _survivals.Add(survival);

    /// <summary>丢弃当前未闭合回合（demo 尾部损坏时用）。</summary>
    public void DiscardOpenRound()
    {
        if (_currentRound == 0 || _rounds.Count == 0)
            return;
        var round = _rounds[^1].RoundNumber;
        _rounds.RemoveAt(_rounds.Count - 1);
        _kills.RemoveAll(k => k.RoundNumber == round);
        _damages.RemoveAll(d => d.RoundNumber == round);
        _survivals.RemoveAll(s => s.RoundNumber == round);
        _ledger.DiscardRound(round);
    }

    public MatchData Build()
    {
        // 存活纠正：回合结束事件与最后一杀同 tick 时，GOTV 的 PawnIsAlive 快照还没翻转，
        // 会把终结者记成“存活”。反例：被杀=未存活（CS 无复活），以击杀表为准强制纠正。
        var killed = new HashSet<(int Round, string Victim)>();
        foreach (var k in _kills)
        {
            if (!string.IsNullOrEmpty(k.VictimSteamId))
                killed.Add((k.RoundNumber, k.VictimSteamId));
        }

        foreach (var s in _survivals)
        {
            if (s.Survived && killed.Contains((s.RoundNumber, s.SteamId)))
                s.Survived = false;
        }

        return new MatchData
        {
            Players = _players.Values.ToList(),
            Rounds = _rounds,
            Kills = _kills,
            Damages = _damages,
            Survivals = _survivals,
            Meta = Meta,
        };
    }
}
