using System.Text.Json;

namespace Cs2Demo.Analytics;

/// <summary>
/// 把 csda JSON 映射成本库对外的 <see cref="MatchData"/>。
/// 对外的阵营用 <see cref="Sides"/> 字符串；csda 的整数阵营在此转换。
/// </summary>
internal static class CsdaMatchMapper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static MatchData Map(string json)
    {
        var match = JsonSerializer.Deserialize<CsdaMatch>(json, Options)
            ?? throw new InvalidOperationException("csda 输出为空，无法解析。");

        var data = new MatchData
        {
            Meta = new MatchMeta
            {
                MapName = match.MapName ?? "",
                TickRate = match.Tickrate,
                MaxRounds = match.MaxRounds,
            },
        };

        // (回合, 玩家) → 经济记录，用于给击杀/存活补上装备值。
        var economy = new Dictionary<(int Round, ulong SteamId), CsdaPlayerEconomy>();
        if (match.PlayerEconomies is not null)
        {
            foreach (var e in match.PlayerEconomies)
                economy[(e.RoundNumber, e.SteamId)] = e;
        }

        if (match.Players is not null)
        {
            foreach (var player in match.Players.Values)
                data.Players.Add(MapPlayer(player));
        }

        // 安包 tick / 拆包者，按回合归并。
        var plantTickByRound = new Dictionary<int, int>();
        if (match.BombsPlanted is not null)
        {
            foreach (var plant in match.BombsPlanted)
            {
                if (!plantTickByRound.ContainsKey(plant.RoundNumber))
                    plantTickByRound[plant.RoundNumber] = plant.Tick;
            }
        }

        var defuserByRound = new Dictionary<int, ulong>();
        if (match.BombsDefused is not null)
        {
            foreach (var defuse in match.BombsDefused)
                defuserByRound[defuse.RoundNumber] = defuse.DefuserSteamId;
        }

        if (match.Rounds is not null)
        {
            foreach (var round in match.Rounds)
            {
                var (tEv, ctEv, tType, ctType) = TeamEquipment(round);
                data.Rounds.Add(new RoundInfo
                {
                    RoundNumber = round.Number,
                    WinnerSide = Side(round.WinnerSide),
                    StartTick = round.StartTick,
                    EndTick = round.EndTick,
                    TEquipmentValue = tEv,
                    CTEquipmentValue = ctEv,
                    TEconomyType = tType,
                    CTEconomyType = ctType,
                    BombPlantedTick = plantTickByRound.TryGetValue(round.Number, out var tick) ? tick : 0,
                    DefuserSteamId = defuserByRound.TryGetValue(round.Number, out var defuser) && defuser != 0
                        ? defuser.ToString()
                        : "",
                });
            }
        }

        if (match.Kills is not null)
        {
            foreach (var kill in match.Kills)
            {
                if (kill.VictimSteamId == 0)
                    continue;
                if (kill.KillerSteamId != 0 && kill.KillerSteamId == kill.VictimSteamId)
                    continue; // 自杀

                var killerSide = Side(kill.KillerSide);
                var victimSide = Side(kill.VictimSide);

                // 队伤/世界（炸弹）击杀记为"死亡事件"：AttackerSteamId 留空，不给任何人记击杀。
                var isEnemyKill = kill.KillerSteamId != 0
                    && !(killerSide != Sides.Unknown && killerSide == victimSide);

                data.Kills.Add(new KillEvent
                {
                    RoundNumber = kill.RoundNumber,
                    Tick = kill.Tick,
                    AttackerSteamId = isEnemyKill ? kill.KillerSteamId.ToString() : "",
                    VictimSteamId = kill.VictimSteamId.ToString(),
                    AssisterSteamId = isEnemyKill && kill.AssisterSteamId != 0 ? kill.AssisterSteamId.ToString() : "",
                    AttackerSide = isEnemyKill ? killerSide : Sides.Unknown,
                    VictimSide = victimSide,
                    Weapon = WeaponNames.Normalize(kill.WeaponName),
                    WeaponType = kill.WeaponType ?? "",
                    IsHeadshot = kill.IsHeadshot,
                    IsAssistedFlash = kill.IsAssistedFlash,
                    IsTradeKill = kill.IsTradeKill,
                    IsTradeDeath = kill.IsTradeDeath,
                    KillerEquipmentValue = EquipmentOf(economy, kill.RoundNumber, kill.KillerSteamId),
                    VictimEquipmentValue = EquipmentOf(economy, kill.RoundNumber, kill.VictimSteamId),
                });
            }
        }

        if (match.Damages is not null)
        {
            foreach (var damage in match.Damages)
            {
                // 与 csda 的 Player.HealthDamage 口径保持一致。
                if (damage.AttackerSteamId == 0 || damage.VictimSteamId == 0)
                    continue;
                if (damage.AttackerSteamId == damage.VictimSteamId)
                    continue;
                if (damage.AttackerSide == damage.VictimSide)
                    continue;
                if (damage.IsAttackerControllingBot)
                    continue;

                data.Damages.Add(new DamageEvent
                {
                    RoundNumber = damage.RoundNumber,
                    AttackerSteamId = damage.AttackerSteamId.ToString(),
                    VictimSteamId = damage.VictimSteamId.ToString(),
                    Damage = Math.Max(0, damage.HealthDamage),
                    AttackerSide = Side(damage.AttackerSide),
                    VictimSide = Side(damage.VictimSide),
                    Weapon = WeaponNames.Normalize(damage.WeaponName),
                    VictimArmor = damage.VictimArmor,
                });
            }
        }

        BuildSurvivals(match, data);

        if (match.Clutches is not null)
        {
            foreach (var clutch in match.Clutches)
            {
                if (clutch.ClutcherSteamId == 0)
                    continue;

                data.Clutches.Add(new ClutchInfo
                {
                    RoundNumber = clutch.RoundNumber,
                    SteamId = clutch.ClutcherSteamId.ToString(),
                    OpponentCount = clutch.OpponentCount,
                    Side = Side(clutch.Side),
                    Won = clutch.HasWon,
                    Survived = clutch.ClutcherSurvived,
                    KillCount = clutch.ClutcherKillCount,
                });
            }
        }

        return data;
    }

    /// <summary>把 csda 的 teamA/teamB 装备值按阵营（T/CT）归并；阵营未知时全为 0。</summary>
    private static (int TEv, int CtEv, string TType, string CtType) TeamEquipment(CsdaRound round)
    {
        // teamASide: 2=T / 3=CT。
        if (round.TeamASide == 2)
            return (round.TeamAEquipmentValue, round.TeamBEquipmentValue, round.TeamAEconomyType ?? "", round.TeamBEconomyType ?? "");
        if (round.TeamASide == 3)
            return (round.TeamBEquipmentValue, round.TeamAEquipmentValue, round.TeamBEconomyType ?? "", round.TeamAEconomyType ?? "");
        if (round.TeamBSide == 2)
            return (round.TeamBEquipmentValue, round.TeamAEquipmentValue, round.TeamBEconomyType ?? "", round.TeamAEconomyType ?? "");
        return (0, 0, "", "");
    }

    private static int EquipmentOf(Dictionary<(int Round, ulong SteamId), CsdaPlayerEconomy> economy, int round, ulong steamId)
        => steamId != 0 && economy.TryGetValue((round, steamId), out var e) ? e.EquipmentValue : 0;

    /// <summary>
    /// csda 没有逐回合存活表，用「该回合所有死亡 + 每名玩家的 playerEconomies 出场记录」还原：
    /// 出了场但没死 = 存活。同时把该回合的装备值/经济类型带出。
    /// </summary>
    private static void BuildSurvivals(CsdaMatch match, MatchData data)
    {
        var deathsByRound = new Dictionary<int, HashSet<ulong>>();
        if (match.Kills is not null)
        {
            foreach (var kill in match.Kills)
            {
                if (kill.VictimSteamId == 0)
                    continue;
                if (!deathsByRound.TryGetValue(kill.RoundNumber, out var set))
                {
                    set = new HashSet<ulong>();
                    deathsByRound[kill.RoundNumber] = set;
                }
                set.Add(kill.VictimSteamId);
            }
        }

        if (match.PlayerEconomies is null)
            return;

        foreach (var economy in match.PlayerEconomies)
        {
            if (economy.SteamId == 0)
                continue;

            var died = deathsByRound.TryGetValue(economy.RoundNumber, out var set)
                && set.Contains(economy.SteamId);

            data.Survivals.Add(new SurvivalInfo
            {
                RoundNumber = economy.RoundNumber,
                SteamId = economy.SteamId.ToString(),
                Survived = !died,
                Side = Side(economy.PlayerSide),
                EquipmentValue = economy.EquipmentValue,
                EconomyType = economy.Type ?? "",
            });
        }
    }

    private static PlayerInfo MapPlayer(CsdaPlayer player) => new()
    {
        SteamId = player.SteamId.ToString(),
        Name = player.Name ?? "",
        Csda = new PlayerCsdaStats
        {
            Team = player.Team?.Name ?? "",
            KillCount = player.KillCount,
            DeathCount = player.DeathCount,
            AssistCount = player.AssistCount,
            HealthDamage = player.HealthDamage,
            ArmorDamage = player.ArmorDamage,
            KastPercent = player.Kast,
            AverageKillPerRound = player.AverageKillPerRound,
            AverageDeathPerRound = player.AverageDeathPerRound,
            AverageDamagePerRound = player.AverageDamagePerRound,
            HltvRating = player.HltvRating,
            HltvRating2 = player.HltvRating2,
            OneKillCount = player.OneKillCount,
            TwoKillCount = player.TwoKillCount,
            ThreeKillCount = player.ThreeKillCount,
            FourKillCount = player.FourKillCount,
            FiveKillCount = player.FiveKillCount,
        },
    };

    /// <summary>csda 的 common.Team 整数枚举：0 未定 / 1 观战 / 2 T / 3 CT。</summary>
    internal static string Side(int side) => side switch
    {
        2 => Sides.Terrorist,
        3 => Sides.CounterTerrorist,
        _ => Sides.Unknown,
    };
}
