using System.Security.Cryptography;
using System.Text;
using DemoFile;
using DemoFile.Game.Cs;

namespace Cs2Demo.Analytics;

/// <summary>
/// Demo 解析入口（两步 API 的第一步）。只做“demo 文件 -&gt; 原始数据”，不做任何指标计算。
/// 热身回合、自杀、队伤会被丢掉；BOT（SteamID 为 0）不建档。
/// </summary>
public static class DemoAnalyzer
{
    /// <summary>解析单个 .dem 文件。文件不存在抛 <see cref="FileNotFoundException"/>；解析不出玩家/回合抛 <see cref="InvalidOperationException"/>。</summary>
    public static MatchData ParseFile(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Demo 文件不存在。", path);

        var builder = new MatchBuilder();
        builder.Meta.FileSha256 = Sha256OfFile(path);

        using var stream = File.OpenRead(path);
        ParseInto(stream, builder);
        return Finish(builder);
    }

    /// <summary>解析已打开的 demo 流（不负责关闭流）。流式解析不计算文件哈希，<c>FileSha256</c> 为空。</summary>
    public static MatchData ParseStream(Stream stream, string? sourceName = null)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));

        var builder = new MatchBuilder();
        try
        {
            ParseInto(stream, builder);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Demo 解析失败{(sourceName is null ? "" : $"（{sourceName}）")}：{ex.Message}", ex);
        }

        return Finish(builder);
    }

    private static void ParseInto(Stream stream, MatchBuilder builder)
    {
        var demo = new CsDemoParser();
        demo.Source1GameEvents.RoundStart += _ => OnRoundStart(demo, builder);
        demo.Source1GameEvents.RoundFreezeEnd += _ => OnRoundFreezeEnd(demo, builder);
        demo.Source1GameEvents.PlayerDeath += e => OnKill(demo, builder, e);
        demo.Source1GameEvents.PlayerHurt += e => OnDamage(demo, builder, e);
        demo.Source1GameEvents.RoundEnd += e => OnRoundEnd(demo, builder, e);

        var reader = DemoFileReader.Create(demo, stream);
        try
        {
            reader.ReadAllAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (IsRecoverableWireError(ex))
        {
            // 尾部坏包：丢掉没打完的最后一回合，保留之前已成功的数据。
            builder.DiscardOpenRound();
        }

        if (demo.ServerInfo?.MapName is { Length: > 0 } map)
            builder.Meta.MapName = map;
        CollectPlayers(demo, builder);
    }

    private static MatchData Finish(MatchBuilder builder)
    {
        var data = builder.Build();
        if (data.Players.Count == 0)
            throw new InvalidOperationException("Demo 解析失败：未识别到玩家列表。");
        if (data.Rounds.Count == 0)
            throw new InvalidOperationException("Demo 解析失败：未识别到有效回合。");
        return data;
    }

    private static void OnRoundStart(CsDemoParser demo, MatchBuilder builder)
    {
        if (demo.GameRules.WarmupPeriod)
            return;
        builder.StartRound(CurrentTick(demo));
        CollectPlayers(demo, builder);
    }

    private static void OnRoundFreezeEnd(CsDemoParser demo, MatchBuilder builder)
    {
        if (demo.GameRules.WarmupPeriod)
            return;
        builder.EnsureRound(CurrentTick(demo));
        CollectPlayers(demo, builder);
    }

    private static void OnKill(CsDemoParser demo, MatchBuilder builder, Source1PlayerDeathEvent e)
    {
        if (demo.GameRules.WarmupPeriod)
            return;

        var killer = SteamId(e.Attacker);
        var victim = SteamId(e.Player);
        if (string.IsNullOrEmpty(victim))
            return;

        var killerSide = SideOf(e.Attacker);
        var victimSide = SideOf(e.Player);

        if (!string.IsNullOrEmpty(killer) && killer == victim)
            return; // 自杀：HLTV 不计，直接丢掉

        builder.EnsureRound(CurrentTick(demo));
        builder.AddPlayer(victim, e.Player!.PlayerName);

        // 炸弹/世界击杀（无凶手）与队伤：HLTV 给受害者记死亡，不给凶手记击杀。
        // 记一条“只有受害者”的死亡事件（助攻一并丢掉，HLTV 不会对这类击杀记助攻）。
        if (string.IsNullOrEmpty(killer) || (!string.IsNullOrEmpty(killerSide) && killerSide == victimSide))
        {
            builder.AddKill(new KillEvent
            {
                RoundNumber = builder.CurrentRound,
                Tick = CurrentTick(demo),
                AttackerSteamId = "",
                VictimSteamId = victim,
                AssisterSteamId = "",
                AttackerSide = Sides.Unknown,
                VictimSide = victimSide,
                Weapon = WeaponNames.Normalize(e.Weapon),
            });
            return;
        }

        builder.AddPlayer(killer, e.Attacker!.PlayerName);
        var assister = SteamId(e.Assister);
        if (!string.IsNullOrEmpty(assister))
            builder.AddPlayer(assister, e.Assister!.PlayerName);

        builder.AddKill(new KillEvent
        {
            RoundNumber = builder.CurrentRound,
            Tick = CurrentTick(demo),
            AttackerSteamId = killer,
            VictimSteamId = victim,
            AssisterSteamId = assister,
            AttackerSide = killerSide,
            VictimSide = victimSide,
            Weapon = WeaponNames.Normalize(e.Weapon),
        });
    }

    private static void OnDamage(CsDemoParser demo, MatchBuilder builder, Source1PlayerHurtEvent e)
    {
        if (demo.GameRules.WarmupPeriod || e.DmgHealth <= 0)
            return;

        var attacker = SteamId(e.Attacker);
        var victim = SteamId(e.Player);
        if (string.IsNullOrEmpty(victim))
            return;

        var attackerSide = SideOf(e.Attacker);
        var victimSide = SideOf(e.Player);

        builder.EnsureRound(CurrentTick(demo));
        if (!string.IsNullOrEmpty(attacker))
            builder.AddPlayer(attacker, e.Attacker!.PlayerName);
        builder.AddPlayer(victim, e.Player!.PlayerName);

        // 血条永远要跟（自伤/队伤/世界伤害也会掉血，不跟的话下一次敌人命中会把这部分
        // 误算到敌人头上，ADR 系统性偏高）；只有敌方造成的才记入伤害表。
        var real = builder.TrackHealth(builder.CurrentRound, victim, e.Health);
        if (string.IsNullOrEmpty(attacker) || attacker == victim)
            return;
        if (!string.IsNullOrEmpty(attackerSide) && attackerSide == victimSide)
            return; // 队伤：跟血条，不记伤害
        if (real <= 0)
            return;

        builder.RecordDamage(new DamageEvent
        {
            RoundNumber = builder.CurrentRound,
            AttackerSteamId = attacker,
            VictimSteamId = victim,
            Damage = real,
            AttackerSide = attackerSide,
            VictimSide = victimSide,
            Weapon = WeaponNames.Normalize(e.Weapon),
        });
    }

    private static void OnRoundEnd(CsDemoParser demo, MatchBuilder builder, Source1RoundEndEvent e)
    {
        if (demo.GameRules.WarmupPeriod)
            return;
        builder.EndRound(MapSide((CSTeamNumber)e.Winner), CurrentTick(demo));

        var round = builder.CurrentRound;
        foreach (var player in demo.Players)
        {
            var id = SteamId(player);
            if (string.IsNullOrEmpty(id))
                continue;
            if ((CSTeamNumber)player.TeamNum is not (CSTeamNumber.Terrorist or CSTeamNumber.CounterTerrorist))
                continue;
            builder.AddPlayer(id, player.PlayerName);
            builder.AddSurvival(new SurvivalInfo
            {
                RoundNumber = round,
                SteamId = id,
                Survived = player.PawnIsAlive,
                Side = MapSide((CSTeamNumber)player.TeamNum),
            });
        }

        CollectPlayers(demo, builder);
    }

    private static void CollectPlayers(CsDemoParser demo, MatchBuilder builder)
    {
        foreach (var player in demo.Players)
        {
            if ((CSTeamNumber)player.TeamNum is not (CSTeamNumber.Terrorist or CSTeamNumber.CounterTerrorist))
                continue;
            builder.AddPlayer(SteamId(player), player.PlayerName);
        }
    }

    private static int CurrentTick(CsDemoParser demo) => (int)demo.CurrentDemoTick.Value;

    private static string SteamId(CCSPlayerController? player)
        => player is null || player.SteamID == 0 ? "" : player.SteamID.ToString();

    private static string SideOf(CCSPlayerController? player)
        => player is null ? Sides.Unknown : MapSide((CSTeamNumber)player.TeamNum);

    private static string MapSide(CSTeamNumber team) => team switch
    {
        CSTeamNumber.Terrorist => Sides.Terrorist,
        CSTeamNumber.CounterTerrorist => Sides.CounterTerrorist,
        _ => Sides.Unknown,
    };

    private static bool IsRecoverableWireError(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur.GetType().FullName == "Google.Protobuf.InvalidProtocolBufferException")
                return true;
        }

        return false;
    }

    private static string Sha256OfFile(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
