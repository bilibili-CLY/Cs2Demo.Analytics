using System;
using System.Collections.Generic;
using System.Linq;

namespace Cs2Demo.Analytics;

/// <summary>
/// Rating30 的 Round Swing v2（本地近似）：经济 + 安包加权的回合胜率变化 + 击杀贡献分摊 +
/// 回合末残值零和分摊。返回每名玩家的 swing（自己队伍视角的胜率小数之和）。详见 docs/formulas.md。
/// </summary>
internal static class SwingCalculator
{
    // 回合胜率函数系数：存活数系数锚定 1.0、偏置 -0.05（保持 5v5≈48.8%、5v4≈72.1% 公开示例），
    // 经济 / 安包系数由 11 张 EPL demo 逻辑回归拟合（见 docs/formulas.md）。
    internal const double BaseOffset = -0.05;
    internal const double AliveCoef = 1.0;
    internal const double EcoCoef = 0.0689;
    internal const double BombCoef = 0.8672;

    // 单次击杀 swing 的贡献分摊权重（其余归击杀者）。
    private const double KillerShare = 0.60;
    private const double DamageShare = 0.25;
    private const double FlashShare = 0.10;
    private const double TradeShare = 0.05;

    /// <summary>T 方回合胜率：存活差 + 经济差 + 安包。</summary>
    internal static double RoundWinProb(int tAlive, int ctAlive, int evT, int evCT, bool bombPlanted)
    {
        var z = BaseOffset
            + AliveCoef * (tAlive - ctAlive)
            + EcoCoef * (evT - evCT) / 1000.0
            + (bombPlanted ? BombCoef : 0.0);
        return 1.0 / (1.0 + Math.Exp(-z));
    }

    /// <summary>算出每名玩家的 swing 合计（胜率小数，自己队伍视角；全玩家零和）。</summary>
    internal static Dictionary<string, double> Compute(MatchData data)
    {
        var total = data.Players.ToDictionary(p => p.SteamId, _ => 0.0);
        var rounds = data.Rounds.ToDictionary(r => r.RoundNumber);

        var rosterByRound = new Dictionary<int, List<SurvivalInfo>>();
        foreach (var s in data.Survivals)
        {
            if (!rosterByRound.TryGetValue(s.RoundNumber, out var list))
                rosterByRound[s.RoundNumber] = list = new List<SurvivalInfo>();
            list.Add(s);
        }

        var clutchersByRound = new Dictionary<int, HashSet<string>>();
        foreach (var c in data.Clutches)
        {
            if (!clutchersByRound.TryGetValue(c.RoundNumber, out var set))
                clutchersByRound[c.RoundNumber] = set = new HashSet<string>();
            set.Add(c.SteamId);
        }

        var damageByRoundVictim = new Dictionary<(int Round, string Victim), List<DamageEvent>>();
        foreach (var d in data.Damages)
        {
            var key = (d.RoundNumber, d.VictimSteamId);
            if (!damageByRoundVictim.TryGetValue(key, out var list))
                damageByRoundVictim[key] = list = new List<DamageEvent>();
            list.Add(d);
        }

        foreach (var group in data.Kills.GroupBy(k => k.RoundNumber))
        {
            var roundNumber = group.Key;
            rounds.TryGetValue(roundNumber, out var round);
            var evT = round?.TEquipmentValue ?? 0;
            var evCT = round?.CTEquipmentValue ?? 0;
            var plantTick = round?.BombPlantedTick ?? 0;

            var kills = group.OrderBy(k => k.Tick).ToList();
            var tAlive = 5;
            var ctAlive = 5;
            var wpaByPlayer = new Dictionary<string, double>();

            foreach (var k in kills)
            {
                if (k.AttackerSteamId.Length > 0 && k.AttackerSteamId == k.VictimSteamId)
                    continue; // 自杀

                var victimSide = k.VictimSide;
                if (victimSide != Sides.Terrorist && victimSide != Sides.CounterTerrorist)
                {
                    victimSide = k.AttackerSide == Sides.Terrorist ? Sides.CounterTerrorist
                        : k.AttackerSide == Sides.CounterTerrorist ? Sides.Terrorist
                        : Sides.Unknown;
                }
                if (victimSide != Sides.Terrorist && victimSide != Sides.CounterTerrorist)
                    continue;

                var bomb = plantTick > 0 && k.Tick >= plantTick;
                var before = RoundWinProb(tAlive, ctAlive, evT, evCT, bomb);
                if (victimSide == Sides.Terrorist)
                    tAlive = Math.Max(0, tAlive - 1);
                else
                    ctAlive = Math.Max(0, ctAlive - 1);
                var after = RoundWinProb(tAlive, ctAlive, evT, evCT, bomb);

                // 队伤/世界击杀（无击杀者）改存活数但不计 swing（也不破坏零和）。
                if (k.AttackerSteamId.Length == 0)
                    continue;

                var dT = after - before; // T 视角胜率变化
                var gain = Math.Max(0.0, k.AttackerSide == Sides.Terrorist ? dT : -dT);
                if (gain <= 0)
                    continue;

                DistributeKill(k, gain, damageByRoundVictim, kills, total, wpaByPlayer);
                if (total.ContainsKey(k.VictimSteamId))
                    total[k.VictimSteamId] -= gain; // 受害者承担等量负值
            }

            // 回合末残值（实际结果 - 模型胜率），按份额在双方之间零和分摊。
            var winner = round?.WinnerSide ?? Sides.Unknown;
            if (round is null || (winner != Sides.Terrorist && winner != Sides.CounterTerrorist))
                continue;
            if (!rosterByRound.TryGetValue(roundNumber, out var roster))
                continue;

            var pEnd = RoundWinProb(tAlive, ctAlive, evT, evCT, plantTick > 0);
            var truth = winner == Sides.Terrorist ? 1.0 : 0.0;
            var residual = truth - pEnd;
            if (Math.Abs(residual) >= 1e-12)
                DistributeResidual(roster, residual, wpaByPlayer, kills, clutchersByRound, round, total);
        }

        return total;
    }

    /// <summary>把单次击杀的 swing 按其贡献在击杀者队伍内分摊；受害者另记等量负值。</summary>
    private static void DistributeKill(
        KillEvent kill,
        double gain,
        Dictionary<(int Round, string Victim), List<DamageEvent>> damageByRoundVictim,
        List<KillEvent> kills,
        Dictionary<string, double> total,
        Dictionary<string, double> wpaByPlayer)
    {
        var attacker = kill.AttackerSteamId;
        var team = kill.AttackerSide;
        var assigned = 0.0;

        // 伤害贡献者：本回合对受害者造成伤害的队友，按伤害占比分摊 0.25。
        if (damageByRoundVictim.TryGetValue((kill.RoundNumber, kill.VictimSteamId), out var damages))
        {
            var contributors = damages
                .Where(d => d.AttackerSteamId != kill.VictimSteamId
                    && d.AttackerSteamId != attacker
                    && d.AttackerSide == team
                    && d.Damage > 0)
                .GroupBy(d => d.AttackerSteamId)
                .Select(g => new { Id = g.Key, Damage = g.Sum(x => x.Damage) })
                .ToList();
            var totalDamage = contributors.Sum(x => x.Damage);
            if (totalDamage > 0)
            {
                foreach (var c in contributors)
                    Add(total, c.Id, gain * DamageShare * c.Damage / totalDamage);
                assigned += DamageShare;
            }
        }

        // 闪光助攻 0.10。
        if (kill.IsAssistedFlash && kill.AssisterSteamId.Length > 0 && kill.AssisterSteamId != attacker)
        {
            Add(total, kill.AssisterSteamId, gain * FlashShare);
            assigned += FlashShare;
        }

        // 补枪：被补掉的那名（最近的队友阵亡者）分 0.05。
        if (kill.IsTradeKill)
        {
            var partner = kills
                .Where(x => x.Tick < kill.Tick && x.VictimSide == team && x.VictimSteamId.Length > 0)
                .OrderByDescending(x => x.Tick)
                .Select(x => x.VictimSteamId)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(partner) && partner != attacker)
            {
                Add(total, partner, gain * TradeShare);
                assigned += TradeShare;
            }
        }

        // 剩余（≥0.60）归击杀者。
        Add(total, attacker, gain * (1.0 - assigned));
        wpaByPlayer[attacker] = (wpaByPlayer.TryGetValue(attacker, out var wpa) ? wpa : 0.0) + gain;
    }

    /// <summary>
    /// 回合末残值按份额分摊：1 份额（残局）+ 2 份额（正向击杀 swing 占比）+ 1 份额（拆包或补枪）
    /// + 1 份额（存活），归一化后胜方得正、负方得负，保证零和。
    /// </summary>
    private static void DistributeResidual(
        List<SurvivalInfo> roster,
        double residual,
        Dictionary<string, double> wpaByPlayer,
        List<KillEvent> kills,
        Dictionary<int, HashSet<string>> clutchersByRound,
        RoundInfo round,
        Dictionary<string, double> total)
    {
        var clutchers = clutchersByRound.TryGetValue(round.RoundNumber, out var set) ? set : new HashSet<string>();
        var tradeKillers = kills.Where(k => k.IsTradeKill && k.AttackerSteamId.Length > 0)
            .Select(k => k.AttackerSteamId).ToHashSet();

        foreach (var side in new[] { Sides.Terrorist, Sides.CounterTerrorist })
        {
            var players = roster.Where(s => s.Side == side).ToList();
            if (players.Count == 0)
                continue;

            var totalWpa = players.Sum(s => wpaByPlayer.TryGetValue(s.SteamId, out var w) && w > 0 ? w : 0.0);
            var weights = new double[players.Count];
            var sum = 0.0;
            for (var i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var w = 0.0;
                if (clutchers.Contains(p.SteamId)) w += 1.0;
                if (totalWpa > 0 && wpaByPlayer.TryGetValue(p.SteamId, out var wpa) && wpa > 0)
                    w += 2.0 * wpa / totalWpa;
                if (round.DefuserSteamId == p.SteamId) w += 1.0;
                if (tradeKillers.Contains(p.SteamId)) w += 1.0;
                if (p.Survived) w += 1.0;
                weights[i] = w;
                sum += w;
            }

            var factor = side == Sides.Terrorist ? residual : -residual;
            if (sum <= 0)
            {
                var share = factor / players.Count;
                foreach (var p in players)
                    Add(total, p.SteamId, share);
            }
            else
            {
                for (var i = 0; i < players.Count; i++)
                    Add(total, players[i].SteamId, weights[i] / sum * factor);
            }
        }
    }

    private static void Add(Dictionary<string, double> total, string steamId, double value)
    {
        if (steamId.Length > 0 && total.ContainsKey(steamId))
            total[steamId] += value;
    }
}
