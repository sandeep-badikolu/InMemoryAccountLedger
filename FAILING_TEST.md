# Annotated failing test

The assignment asks for one failing test against your own design. The repository keeps the main suite honest with a single deliberate failure.

## Test

`LedgerEngineTests.DeliberatelyFailingTest_OverdraftFeeRemainsAfterE7EvenWhenE9ReversesDebit`

```csharp
[Fact]
public void DeliberatelyFailingTest_OverdraftFeeRemainsAfterE7EvenWhenE9ReversesDebit()
{
    var result = CreateEngine().Replay(AssignmentEvents());
    Assert.Contains(result.BookedFees, f => f.ValueDay == 2 && f.AccountId == "ACC-001");
}
```

## What it reveals

Two acceptance statements conflict:

1. **E7 causes an overdraft fee on Day 2** (true while E7 stands and Day 2 closes negative — see passing test `Day2ClosingBalance_IsNegative370_BeforeE9IsApplied`).
2. **After E9, balances and fees return to pre-E7 values** (requires no Day-2 fee once E9 restores the Day-2 ledger balance).

This implementation **recomputes fees from the final value-dated ledger** after the full stream is applied. After E9, Day 2’s closing balance is AED 250.00 again, so **no Day-2 fee is booked**. The assertion above expects a fee to remain anyway, which would violate criterion (2) unless fees are separate immutable events with their own reversal policy.

A production ledger would model fees as explicit posted events and define whether reversing a debit automatically reverses an assessed fee.

## Running

```bash
dotnet test
```

Expect **1 failed**, **N passed** (all tests except the deliberate failure).
