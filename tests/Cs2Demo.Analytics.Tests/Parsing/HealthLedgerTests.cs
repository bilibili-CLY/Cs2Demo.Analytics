using Cs2Demo.Analytics;

namespace Cs2Demo.Analytics.Tests;

public sealed class HealthLedgerTests
{
    [Fact]
    public void Apply_FullHealthHit_RecordsReportedDamage()
    {
        var ledger = new HealthLedger();
        Assert.Equal(25, ledger.Apply(1, "P", 75));
    }

    [Fact]
    public void Apply_Overkill_ClampsToRealDamage()
    {
        // 人只剩 30 血却打出 100 伤害：只记 30。
        var ledger = new HealthLedger();
        Assert.Equal(70, ledger.Apply(1, "P", 30));
        Assert.Equal(30, ledger.Apply(1, "P", -50));
    }

    [Fact]
    public void Apply_HealedBetweenHits_NeverExceedsPrevious()
    {
        var ledger = new HealthLedger();
        Assert.Equal(60, ledger.Apply(1, "P", 40));
        Assert.Equal(0, ledger.Apply(1, "P", 90)); // 回血不算负伤害
        Assert.Equal(40, ledger.Apply(1, "P", 0));
    }

    [Fact]
    public void Apply_DifferentRounds_TrackedSeparately()
    {
        var ledger = new HealthLedger();
        Assert.Equal(50, ledger.Apply(1, "P", 50));
        Assert.Equal(10, ledger.Apply(2, "P", 90));
    }

    [Fact]
    public void DiscardRound_RemovesOnlyThatRound()
    {
        var ledger = new HealthLedger();
        Assert.Equal(50, ledger.Apply(1, "P", 50));
        ledger.DiscardRound(1);
        Assert.Equal(10, ledger.Apply(1, "P", 90)); // 回合被丢弃后重新从 100 起
    }
}
