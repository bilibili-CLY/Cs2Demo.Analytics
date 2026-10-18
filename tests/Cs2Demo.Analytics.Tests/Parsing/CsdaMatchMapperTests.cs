using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

/// <summary>
/// CsdaMatchMapper 的映射规则：阵营整数转字符串、击杀/伤害过滤、存活还原。
/// 用一段手写 JSON（字段名与 csda 一致）驱动，不依赖真 demo。
/// </summary>
public sealed class CsdaMatchMapperTests
{
    private const string Json = """
    {
      "mapName": "de_test",
      "tickrate": 64,
      "maxRounds": 24,
      "players": {
        "76561199771616955": { "steamId": 76561199771616955, "name": "A", "killCount": 2, "deathCount": 0, "assistCount": 1,
          "kast": 50.0, "healthDamage": 150, "armorDamage": 10, "averageKillPerRound": 1.0, "averageDeathPerRound": 0.5,
          "averageDamagePerRound": 75.0, "hltvRating": 1.1, "hltvRating2": 1.2,
          "oneKillCount": 1, "twoKillCount": 0, "threeKillCount": 0, "fourKillCount": 0, "fiveKillCount": 0,
          "team": { "name": "Team A", "currentSide": 2 } },
        "76561198080515127": { "steamId": 76561198080515127, "name": "B", "killCount": 0, "deathCount": 1, "assistCount": 0,
          "kast": 0.0, "healthDamage": 0, "armorDamage": 0, "averageKillPerRound": 0.0, "averageDeathPerRound": 1.0,
          "averageDamagePerRound": 0.0, "hltvRating": 0.0, "hltvRating2": 0.0,
          "oneKillCount": 0, "twoKillCount": 0, "threeKillCount": 0, "fourKillCount": 0, "fiveKillCount": 0,
          "team": { "name": "Team B", "currentSide": 3 } },
        "76561198436531748": { "steamId": 76561198436531748, "name": "C", "killCount": 0, "deathCount": 1, "assistCount": 0,
          "kast": 0.0, "healthDamage": 0, "armorDamage": 0, "averageKillPerRound": 0.0, "averageDeathPerRound": 1.0,
          "averageDamagePerRound": 0.0, "hltvRating": 0.0, "hltvRating2": 0.0,
          "oneKillCount": 0, "twoKillCount": 0, "threeKillCount": 0, "fourKillCount": 0, "fiveKillCount": 0,
          "team": { "name": "Team A", "currentSide": 2 } }
      },
      "rounds": [
        { "number": 1, "startTick": 10, "endTick": 100, "winnerSide": 2, "winnerName": "Team A" }
      ],
      "kills": [
        { "roundNumber": 1, "tick": 50, "weaponName": "AK-47", "killerSteamId": 76561199771616955, "killerSide": 2,
          "victimSteamId": 76561198080515127, "victimSide": 3, "assisterSteamId": 0, "isTradeKill": false, "isTradeDeath": true },
        { "roundNumber": 1, "tick": 60, "weaponName": "M4A1", "killerSteamId": 76561199771616955, "killerSide": 2,
          "victimSteamId": 76561198436531748, "victimSide": 2, "assisterSteamId": 0, "isTradeKill": false, "isTradeDeath": false },
        { "roundNumber": 1, "tick": 70, "weaponName": "Knife", "killerSteamId": 76561198436531748, "killerSide": 2,
          "victimSteamId": 76561198436531748, "victimSide": 2, "assisterSteamId": 0, "isTradeKill": false, "isTradeDeath": false },
        { "roundNumber": 1, "tick": 80, "weaponName": "C4", "killerSteamId": 0, "killerSide": 0,
          "victimSteamId": 76561198436531748, "victimSide": 2, "assisterSteamId": 0, "isTradeKill": false, "isTradeDeath": false }
      ],
      "damages": [
        { "roundNumber": 1, "healthDamage": 25, "attackerSteamId": 76561199771616955, "attackerSide": 2,
          "victimSteamId": 76561198080515127, "victimSide": 3, "isAttackerControllingBot": false, "weaponName": "AK-47" },
        { "roundNumber": 1, "healthDamage": 30, "attackerSteamId": 76561199771616955, "attackerSide": 2,
          "victimSteamId": 76561198436531748, "victimSide": 2, "isAttackerControllingBot": false, "weaponName": "AK-47" },
        { "roundNumber": 1, "healthDamage": 40, "attackerSteamId": 76561199771616955, "attackerSide": 2,
          "victimSteamId": 76561199771616955, "victimSide": 2, "isAttackerControllingBot": false, "weaponName": "AK-47" },
        { "roundNumber": 1, "healthDamage": 50, "attackerSteamId": 76561198080515127, "attackerSide": 3,
          "victimSteamId": 76561199771616955, "victimSide": 2, "isAttackerControllingBot": true, "weaponName": "AWP" },
        { "roundNumber": 1, "healthDamage": 60, "attackerSteamId": 0, "attackerSide": 0,
          "victimSteamId": 76561199771616955, "victimSide": 2, "isAttackerControllingBot": false, "weaponName": "World" }
      ],
      "playerEconomies": [
        { "roundNumber": 1, "steamId": 76561199771616955, "playerSide": 2 },
        { "roundNumber": 1, "steamId": 76561198080515127, "playerSide": 3 },
        { "roundNumber": 1, "steamId": 76561198436531748, "playerSide": 2 }
      ]
    }
    """;

    private static MatchData Map() => CsdaMatchMapper.Map(Json);

    [Fact]
    public void Map_SetsMetaAndPlayers()
    {
        var data = Map();

        Assert.Equal("de_test", data.Meta.MapName);
        Assert.Equal(64, data.Meta.TickRate);
        Assert.Equal(24, data.Meta.MaxRounds);
        Assert.Equal(3, data.Players.Count);

        var a = data.Players.Single(p => p.SteamId == "76561199771616955");
        Assert.Equal("A", a.Name);
        Assert.Equal("Team A", a.Csda.Team);
        Assert.Equal(2, a.Csda.KillCount);
        Assert.Equal(150, a.Csda.HealthDamage);
        Assert.Equal(50.0, a.Csda.KastPercent);
        Assert.Equal(1.2, a.Csda.HltvRating2);
    }

    [Fact]
    public void Map_Rounds_MapsSideAndTicks()
    {
        var round = Assert.Single(Map().Rounds);
        Assert.Equal(1, round.RoundNumber);
        Assert.Equal(Sides.Terrorist, round.WinnerSide);
        Assert.Equal(10, round.StartTick);
        Assert.Equal(100, round.EndTick);
    }

    [Fact]
    public void Map_Kills_FiltersSuicideAndWorld_KeepsTeamkillAsDeathOnly()
    {
        var kills = Map().Kills;

        Assert.Equal(3, kills.Count); // 4 条输入：自杀被丢，其余 3 条保留

        var enemy = kills.Single(k => k.Tick == 50);
        Assert.Equal("76561199771616955", enemy.AttackerSteamId);
        Assert.Equal("76561198080515127", enemy.VictimSteamId);
        Assert.Equal(Sides.Terrorist, enemy.AttackerSide);
        Assert.Equal(Sides.CounterTerrorist, enemy.VictimSide);
        Assert.Equal("AK-47", enemy.Weapon);
        Assert.True(enemy.IsTradeDeath);
        Assert.False(enemy.IsTradeKill);

        var teamkill = kills.Single(k => k.Tick == 60);
        Assert.Equal("", teamkill.AttackerSteamId);
        Assert.Equal(Sides.Unknown, teamkill.AttackerSide);
        Assert.Equal("76561198436531748", teamkill.VictimSteamId);
        Assert.Equal(Sides.Terrorist, teamkill.VictimSide);

        var world = kills.Single(k => k.Tick == 80);
        Assert.Equal("", world.AttackerSteamId);
        Assert.Equal("C4", world.Weapon);
    }

    [Fact]
    public void Map_Damages_FiltersSelfTeamBotAndWorld()
    {
        var damages = Map().Damages;

        var single = Assert.Single(damages);
        Assert.Equal("76561199771616955", single.AttackerSteamId);
        Assert.Equal("76561198080515127", single.VictimSteamId);
        Assert.Equal(25, single.Damage);
        Assert.Equal(Sides.Terrorist, single.AttackerSide);
        Assert.Equal(Sides.CounterTerrorist, single.VictimSide);
    }

    [Fact]
    public void Map_Survivals_DerivedFromEconomiesAndDeaths()
    {
        var survivals = Map().Survivals;

        Assert.Equal(3, survivals.Count);

        Assert.True(survivals.Single(s => s.SteamId == "76561199771616955").Survived);
        Assert.False(survivals.Single(s => s.SteamId == "76561198080515127").Survived);
        Assert.False(survivals.Single(s => s.SteamId == "76561198436531748").Survived);

        Assert.Equal(Sides.CounterTerrorist, survivals.Single(s => s.SteamId == "76561198080515127").Side);
    }

    [Theory]
    [InlineData(0, Sides.Unknown)]
    [InlineData(1, Sides.Unknown)]
    [InlineData(2, Sides.Terrorist)]
    [InlineData(3, Sides.CounterTerrorist)]
    internal void Side_MapsCsdaTeamEnum(int side, string expected)
    {
        Assert.Equal(expected, CsdaMatchMapper.Side(side));
    }
}
