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
        if (string.IsNullOrEmpty(killer) || string.IsNullOrEmpty(victim) || killer == victim)
            return;

        var killerSide = SideOf(e.Attacker);
        var victimSide = SideOf(e.Player);
        if (!string.IsNullOrEmpty(killerSide) && killerSide == victimSide)
            return; // 队伤

        builder.EnsureRound(CurrentTick(demo));
        builder.AddPlayer(killer, e.Attacker!.PlayerName);
        builder.AddPlayer(victim, e.Player!.PlayerName);
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
        if (string.IsNullOrEmpty(attacker) || string.IsNullOrEmpty(victim) || attacker == victim)
            return;

        var attackerSide = SideOf(e.Attacker);
        var victimSide = SideOf(e.Player);
        if (!string.IsNullOrEmpty(attackerSide) && attackerSide == victimSide)
            return; // 队伤

        builder.EnsureRound(CurrentTick(demo));
        builder.AddPlayer(attacker, e.Attacker!.PlayerName);
        builder.AddPlayer(victim, e.Player!.PlayerName);
        builder.AddDamage(new DamageEvent
        {
            RoundNumber = builder.CurrentRound,
            AttackerSteamId = attacker,
            VictimSteamId = victim,
            AttackerSide = attackerSide,
            VictimSide = victimSide,
            Weapon = WeaponNames.Normalize(e.Weapon),
        }, e.Health);
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
