using Ledger.Core;
using Xunit;

namespace Ledger.Tests;

public sealed class LedgerEngineTests
{
    private static LedgerEngine CreateEngine() => new(new[]
    {
        new Account("ACC-001", Currency.AED, 0.00m),
        new Account("ACC-002", Currency.BHD, 0.000m)
    });

    private static IReadOnlyList<LedgerEvent> AssignmentEvents() => new[]
    {
        new LedgerEvent("E1", 1, 1, EventKind.Credit, "ACC-001", 1200.00m),
        new LedgerEvent("E2", 1, 1, EventKind.Debit, "ACC-001", 950.00m),
        new LedgerEvent("E3", 2, 2, EventKind.Authorization, "ACC-001", 200.00m, "Auth-A"),
        new LedgerEvent("E4", 3, 3, EventKind.Credit, "ACC-001", 400.00m),
        new LedgerEvent("E5", 4, 4, EventKind.Settlement, "ACC-001", 185.00m, "Auth-A"),
        new LedgerEvent("E6", 4, 4, EventKind.Settlement, "ACC-001", 160.00m, "Auth-Z"),
        new LedgerEvent("E7", 5, 2, EventKind.Debit, "ACC-001", 820.00m),
        new LedgerEvent("E8", 5, 5, EventKind.Authorization, "ACC-001", 90.00m, "Auth-B"),
        new LedgerEvent("E9", 6, 2, EventKind.Reversal, "ACC-001", 820.00m, ReversesEventId: "E7"),
        new LedgerEvent("E10a", 6, 5, EventKind.Credit, "ACC-002", 3.333m),
        new LedgerEvent("E10b", 6, 5, EventKind.Credit, "ACC-002", 3.333m),
        new LedgerEvent("E10c", 6, 5, EventKind.Credit, "ACC-002", 3.334m)
    };

    [Fact]
    public void RejectsSettlementWithoutPriorAuthorization()
    {
        var result = CreateEngine().Replay(AssignmentEvents());
        Assert.Contains(result.RejectedEvents, x => x.EventId == "E6");
        Assert.Contains(result.RejectedEvents, x => x.Reason.Contains("missing authorization"));
    }

    [Fact]
    public void AcceptsSettlementAgainstKnownAuthorization()
    {
        var result = CreateEngine().Replay(AssignmentEvents());
        Assert.Contains(result.AcceptedEvents, x => x.Id == "E5");
    }

    [Fact]
    public void AuthorizationHoldChangesAvailableButNotLedgerBalance()
    {
        var events = new[]
        {
            new LedgerEvent("C1", 1, 1, EventKind.Credit, "ACC-001", 100m),
            new LedgerEvent("A1", 1, 1, EventKind.Authorization, "ACC-001", 30m, "AUTH-1")
        };
        var snapshot = CreateEngine().Replay(events, 1).Accounts["ACC-001"];
        Assert.Equal(100.00m, snapshot.LedgerBalance);
        Assert.Equal(30.00m, snapshot.ActiveHolds);
        Assert.Equal(70.00m, snapshot.AvailableBalance);
    }

    [Fact]
    public void AuthB_IsApproved_UsingPriorDayClosingBalance()
    {
        var result = CreateEngine().Replay(AssignmentEvents());
        Assert.Contains(result.AcceptedEvents, x => x.Id == "E8" && x.AuthorizationId == "Auth-B");
        var day5 = result.DailySnapshots[4];
        Assert.Contains(day5.AuthorizationStates["ACC-001"], a => a.AuthorizationId == "Auth-B");
    }

    [Fact]
    public void ReversalCompensatesOriginalDebitWithoutMutatingOriginalEvent()
    {
        var events = new[]
        {
            new LedgerEvent("C1", 1, 1, EventKind.Credit, "ACC-001", 100m),
            new LedgerEvent("D1", 1, 1, EventKind.Debit, "ACC-001", 40m),
            new LedgerEvent("R1", 2, 1, EventKind.Reversal, "ACC-001", 40m, ReversesEventId: "D1")
        };
        var result = CreateEngine().Replay(events, 2);
        Assert.Equal(100.00m, result.Accounts["ACC-001"].LedgerBalance);
        Assert.Equal(40m, events.Single(e => e.Id == "D1").Amount);
    }

    [Fact]
    public void BhdTenDinarInstallmentsMustAllocateExactlyTenDinars()
    {
        var installments = new[] { 3.333m, 3.333m, 3.334m };
        Assert.Equal(10.000m, installments.Sum());
        Assert.All(installments, x => Assert.Equal(x, Money.Round(x, Currency.BHD)));
    }

    [Fact]
    public void DecimalRoundingUsesCurrencyPrecision()
    {
        Assert.Equal(833.33m, Money.Round(10000m / 12m, Currency.AED));
        Assert.Equal(3.334m, Money.Round(3.3335m, Currency.BHD));
    }

    [Fact]
    public void DuplicateEventIdsAreRejectedBeforeReplay()
    {
        var events = new[]
        {
            new LedgerEvent("same", 1, 1, EventKind.Credit, "ACC-001", 10m),
            new LedgerEvent("same", 1, 1, EventKind.Credit, "ACC-001", 20m)
        };
        Assert.Throws<ArgumentException>(() => CreateEngine().Replay(events, 1));
    }

    [Fact]
    public void UnknownAuthorizationDoesNotChangeLedgerBalance()
    {
        var events = new[]
        {
            new LedgerEvent("C1", 1, 1, EventKind.Credit, "ACC-001", 500m),
            new LedgerEvent("S1", 1, 1, EventKind.Settlement, "ACC-001", 200m, "MISSING")
        };
        var result = CreateEngine().Replay(events, 1);
        Assert.Equal(500m, result.Accounts["ACC-001"].LedgerBalance);
        Assert.Contains(result.RejectedEvents, x => x.EventId == "S1");
    }

    [Fact]
    public void AssignmentReversalRestoresLedgerBalanceToPreDebitValueDateBalance()
    {
        var result = CreateEngine().Replay(AssignmentEvents());
        Assert.Equal(465.00m, result.DailySnapshots[4].Accounts["ACC-001"].LedgerBalance);
        Assert.Equal(465.00m + result.InterestByAccount["ACC-001"], result.Accounts["ACC-001"].LedgerBalance);
    }

    [Fact]
    public void Day2ClosingBalance_IsNegative570_BeforeE9IsApplied()
    {
        var beforeE9Window = AssignmentEvents()
            .Where(e => e.Id != "E9" && e.RecordedDay <= 5)
            .ToList();
        var result = CreateEngine().Replay(beforeE9Window, 5);
        var day2 = result.DailySnapshots[1].Accounts["ACC-001"];
        var day2Fee = result.DailySnapshots[1].FeesAssessed["ACC-001"];
        Assert.Equal(25.00m, day2Fee);
        Assert.Equal(-570.00m, day2.LedgerBalance + day2Fee);
        Assert.Contains(result.BookedFees, f => f.ValueDay == 2 && f.Amount == 25.00m);
    }

    [Fact]
    public void InterestAccrualsSumToCapitalizedTotal()
    {
        var result = CreateEngine().Replay(AssignmentEvents());
        foreach (var interest in result.BookedInterest)
            Assert.Equal(result.InterestByAccount[interest.AccountId], interest.Amount);
    }

    /// <summary>
    /// Intentionally failing: documents that recomputing fees from the final value-dated ledger
    /// does not preserve a Day-2 fee after E9 reverses E7. See REJECTED.md and FAILING_TEST.md.
    /// </summary>
    [Fact]
    public void DeliberatelyFailingTest_OverdraftFeeRemainsAfterE7EvenWhenE9ReversesDebit()
    {
        var result = CreateEngine().Replay(AssignmentEvents());
        // REVEALS: This engine recomputes fees from the final value-dated ledger after E9,
        // so Day 2 is no longer negative and no fee is booked — conflicting with criteria that
        // require a Day-2 fee to persist after E7 even once E9 reverses the debit.
        Assert.Contains(result.BookedFees, f => f.ValueDay == 2 && f.AccountId == "ACC-001");
    }
}
