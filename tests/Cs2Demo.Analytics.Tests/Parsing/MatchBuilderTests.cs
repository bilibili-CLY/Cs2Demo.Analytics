using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

public sealed class MatchBuilderTests
{
    [Fact]
    public void AddKill_SetsTradedSteamId()
    {
        var builder = new MatchBuilder();
        builder.StartRound(0);
        builder.AddKill(new KillEvent { RoundNumber = 1, Tick = 100, AttackerSteamId = "A", VictimSteamId = "B", VictimSide = Sides.Terrorist });
        builder.AddKill(new KillEvent { RoundNumber = 1, Tick = 200, AttackerSteamId = "C", VictimSteamId = "A", AttackerSide = Sides.Terrorist });

        var data = builder.Build();
        Assert.Equal("B", data.Kills[1].TradedSteamId);
    }

    [Fact]
    public void AddDamage_ZeroRealDamage_NotRecorded()
    {
        var builder = new MatchBuilder();
        builder.AddDamage(new DamageEvent { RoundNumber = 1, AttackerSteamId = "A", VictimSteamId = "P" }, 100);
        Assert.Empty(builder.Build().Damages);
    }

    [Fact]
    public void DiscardOpenRound_RemovesRoundAndItsEvents()
    {
        var builder = new MatchBuilder();
        builder.StartRound(0);
        builder.AddKill(new KillEvent { RoundNumber = 1, Tick = 1, AttackerSteamId = "A", VictimSteamId = "B" });
        builder.AddDamage(new DamageEvent { RoundNumber = 1, AttackerSteamId = "A", VictimSteamId = "B" }, 90);
        builder.AddSurvival(new SurvivalInfo { RoundNumber = 1, SteamId = "A", Survived = true });

        builder.DiscardOpenRound();

        var data = builder.Build();
        Assert.Empty(data.Rounds);
        Assert.Empty(data.Kills);
        Assert.Empty(data.Damages);
        Assert.Empty(data.Survivals);
    }

    [Fact]
    public void AddPlayer_LatestNameWins()
    {
        var builder = new MatchBuilder();
        builder.AddPlayer("1", "old");
        builder.AddPlayer("1", "new");
        Assert.Equal("new", builder.Build().Players.Single().Name);
    }
}
