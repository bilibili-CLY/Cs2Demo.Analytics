using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

public sealed class TradeResolverTests
{
    private static KillEvent Kill(int tick, string attacker, string victim, string victimSide)
        => new() { RoundNumber = 1, Tick = tick, AttackerSteamId = attacker, VictimSteamId = victim, VictimSide = victimSide };

    [Fact]
    public void Resolve_TeammateKillsKillerWithin5s_ReturnsTradedPlayer()
    {
        // A 打死 B，3 秒后 C（B 的队友）打死 A：C 补掉了 B 的枪。
        var prior = new List<KillEvent> { Kill(100, "A", "B", Sides.Terrorist) };
        Assert.Equal("B", TradeResolver.Resolve(prior, "A", Sides.Terrorist, 100 + 3 * 64));
    }

    [Fact]
    public void Resolve_OutsideWindow_ReturnsEmpty()
    {
        var prior = new List<KillEvent> { Kill(100, "A", "B", Sides.Terrorist) };
        Assert.Equal("", TradeResolver.Resolve(prior, "A", Sides.Terrorist, 100 + TradeResolver.WindowTicks + 1));
    }

    [Fact]
    public void Resolve_WrongSide_ReturnsEmpty()
    {
        // C 不是 B 的队友：不算补枪。
        var prior = new List<KillEvent> { Kill(100, "A", "B", Sides.Terrorist) };
        Assert.Equal("", TradeResolver.Resolve(prior, "A", Sides.CounterTerrorist, 100 + 64));
    }

    [Fact]
    public void Resolve_UnrelatedKill_ReturnsEmpty()
    {
        var prior = new List<KillEvent> { Kill(100, "X", "Y", Sides.Terrorist) };
        Assert.Equal("", TradeResolver.Resolve(prior, "A", Sides.Terrorist, 100 + 64));
    }
}
