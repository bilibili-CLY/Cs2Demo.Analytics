using System.Text.Json.Serialization;

namespace Cs2Demo.Analytics;

// csda（akiver/cs-demo-analyzer）JSON 的最小映射 DTO。
// 只声明本库真正用到的字段；steamId 用 ulong（超过 2^53，double 会丢精度）。
internal sealed class CsdaMatch
{
    [JsonPropertyName("mapName")] public string? MapName { get; set; }
    [JsonPropertyName("game")] public string? Game { get; set; }
    [JsonPropertyName("tickrate")] public int Tickrate { get; set; }
    [JsonPropertyName("maxRounds")] public int MaxRounds { get; set; }
    [JsonPropertyName("players")] public Dictionary<string, CsdaPlayer>? Players { get; set; }
    [JsonPropertyName("kills")] public List<CsdaKill>? Kills { get; set; }
    [JsonPropertyName("damages")] public List<CsdaDamage>? Damages { get; set; }
    [JsonPropertyName("rounds")] public List<CsdaRound>? Rounds { get; set; }
    [JsonPropertyName("playerEconomies")] public List<CsdaPlayerEconomy>? PlayerEconomies { get; set; }
    [JsonPropertyName("bombsPlanted")] public List<CsdaBombPlant>? BombsPlanted { get; set; }
    [JsonPropertyName("bombsDefused")] public List<CsdaBombDefuse>? BombsDefused { get; set; }
    [JsonPropertyName("clutches")] public List<CsdaClutch>? Clutches { get; set; }
}

internal sealed class CsdaPlayer
{
    [JsonPropertyName("steamId")] public ulong SteamId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("killCount")] public int KillCount { get; set; }
    [JsonPropertyName("deathCount")] public int DeathCount { get; set; }
    [JsonPropertyName("assistCount")] public int AssistCount { get; set; }
    [JsonPropertyName("kast")] public double Kast { get; set; }
    [JsonPropertyName("healthDamage")] public int HealthDamage { get; set; }
    [JsonPropertyName("armorDamage")] public int ArmorDamage { get; set; }
    [JsonPropertyName("averageKillPerRound")] public double AverageKillPerRound { get; set; }
    [JsonPropertyName("averageDeathPerRound")] public double AverageDeathPerRound { get; set; }
    [JsonPropertyName("averageDamagePerRound")] public double AverageDamagePerRound { get; set; }
    [JsonPropertyName("hltvRating")] public double HltvRating { get; set; }
    [JsonPropertyName("hltvRating2")] public double HltvRating2 { get; set; }
    [JsonPropertyName("oneKillCount")] public int OneKillCount { get; set; }
    [JsonPropertyName("twoKillCount")] public int TwoKillCount { get; set; }
    [JsonPropertyName("threeKillCount")] public int ThreeKillCount { get; set; }
    [JsonPropertyName("fourKillCount")] public int FourKillCount { get; set; }
    [JsonPropertyName("fiveKillCount")] public int FiveKillCount { get; set; }
    [JsonPropertyName("team")] public CsdaTeam? Team { get; set; }
}

internal sealed class CsdaTeam
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("letter")] public string? Letter { get; set; }
    [JsonPropertyName("score")] public int Score { get; set; }
    [JsonPropertyName("currentSide")] public int CurrentSide { get; set; }
}

internal sealed class CsdaKill
{
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; set; }
    [JsonPropertyName("tick")] public int Tick { get; set; }
    [JsonPropertyName("weaponName")] public string? WeaponName { get; set; }
    [JsonPropertyName("weaponType")] public string? WeaponType { get; set; }
    [JsonPropertyName("isHeadshot")] public bool IsHeadshot { get; set; }
    [JsonPropertyName("isAssistedFlash")] public bool IsAssistedFlash { get; set; }
    [JsonPropertyName("killerSteamId")] public ulong KillerSteamId { get; set; }
    [JsonPropertyName("killerSide")] public int KillerSide { get; set; }
    [JsonPropertyName("victimSteamId")] public ulong VictimSteamId { get; set; }
    [JsonPropertyName("victimSide")] public int VictimSide { get; set; }
    [JsonPropertyName("assisterSteamId")] public ulong AssisterSteamId { get; set; }
    [JsonPropertyName("isTradeKill")] public bool IsTradeKill { get; set; }
    [JsonPropertyName("isTradeDeath")] public bool IsTradeDeath { get; set; }
}

internal sealed class CsdaDamage
{
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; set; }
    [JsonPropertyName("healthDamage")] public int HealthDamage { get; set; }
    [JsonPropertyName("attackerSteamId")] public ulong AttackerSteamId { get; set; }
    [JsonPropertyName("attackerSide")] public int AttackerSide { get; set; }
    [JsonPropertyName("victimSteamId")] public ulong VictimSteamId { get; set; }
    [JsonPropertyName("victimSide")] public int VictimSide { get; set; }
    [JsonPropertyName("victimArmor")] public int VictimArmor { get; set; }
    [JsonPropertyName("isAttackerControllingBot")] public bool IsAttackerControllingBot { get; set; }
    [JsonPropertyName("weaponName")] public string? WeaponName { get; set; }
}

internal sealed class CsdaRound
{
    [JsonPropertyName("number")] public int Number { get; set; }
    [JsonPropertyName("startTick")] public int StartTick { get; set; }
    [JsonPropertyName("endTick")] public int EndTick { get; set; }
    [JsonPropertyName("winnerSide")] public int WinnerSide { get; set; }
    [JsonPropertyName("winnerName")] public string? WinnerName { get; set; }
    [JsonPropertyName("teamAName")] public string? TeamAName { get; set; }
    [JsonPropertyName("teamBName")] public string? TeamBName { get; set; }
    [JsonPropertyName("teamAScore")] public int TeamAScore { get; set; }
    [JsonPropertyName("teamBScore")] public int TeamBScore { get; set; }
    [JsonPropertyName("teamASide")] public int TeamASide { get; set; }
    [JsonPropertyName("teamBSide")] public int TeamBSide { get; set; }
    [JsonPropertyName("teamAEquipmentValue")] public int TeamAEquipmentValue { get; set; }
    [JsonPropertyName("teamBEquipmentValue")] public int TeamBEquipmentValue { get; set; }
    [JsonPropertyName("teamAMoneySpent")] public int TeamAMoneySpent { get; set; }
    [JsonPropertyName("teamBmoneySpent")] public int TeamBMoneySpent { get; set; }
    [JsonPropertyName("teamAEconomyType")] public string? TeamAEconomyType { get; set; }
    [JsonPropertyName("teamBEconomyType")] public string? TeamBEconomyType { get; set; }
}

internal sealed class CsdaPlayerEconomy
{
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; set; }
    [JsonPropertyName("steamId")] public ulong SteamId { get; set; }
    [JsonPropertyName("playerSide")] public int PlayerSide { get; set; }
    [JsonPropertyName("startMoney")] public int StartMoney { get; set; }
    [JsonPropertyName("moneySpent")] public int MoneySpent { get; set; }
    [JsonPropertyName("equipmentValue")] public int EquipmentValue { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}

internal sealed class CsdaBombPlant
{
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; set; }
    [JsonPropertyName("tick")] public int Tick { get; set; }
    [JsonPropertyName("planterSteamId")] public ulong PlanterSteamId { get; set; }
    [JsonPropertyName("site")] public string? Site { get; set; }
}

internal sealed class CsdaBombDefuse
{
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; set; }
    [JsonPropertyName("tick")] public int Tick { get; set; }
    [JsonPropertyName("defuserSteamId")] public ulong DefuserSteamId { get; set; }
    [JsonPropertyName("site")] public string? Site { get; set; }
}

internal sealed class CsdaClutch
{
    [JsonPropertyName("roundNumber")] public int RoundNumber { get; set; }
    [JsonPropertyName("opponentCount")] public int OpponentCount { get; set; }
    [JsonPropertyName("side")] public int Side { get; set; }
    [JsonPropertyName("hasWon")] public bool HasWon { get; set; }
    [JsonPropertyName("clutcherSteamId")] public ulong ClutcherSteamId { get; set; }
    [JsonPropertyName("clutcherSurvived")] public bool ClutcherSurvived { get; set; }
    [JsonPropertyName("clutcherKillCount")] public int ClutcherKillCount { get; set; }
}
