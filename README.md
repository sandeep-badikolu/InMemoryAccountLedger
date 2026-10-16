# In-Memory Account Ledger Core

A .NET 10 in-memory ledger replay engine for the Mal Bank assignment. There is no web layer, database, UI, or persistence. The runner replays the supplied six-day event stream and prints daily closing balances, fee assessments, authorization states, and errors.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or compatible runtime)
- Internet on first build if NuGet packages are not cached

## Run

```bash
dotnet restore
dotnet test
dotnet run --project src/Ledger.Runner/Ledger.Runner.csproj
```

The test suite includes **one intentionally failing test** (`DeliberatelyFailingTest_OverdraftFeeRemainsAfterE7EvenWhenE9ReversesDebit`) that documents a fee/reversal policy conflict. See `FAILING_TEST.md`. All other tests should pass.

## Design summary

- `LedgerEvent` records are immutable. `RecordedDay` is when the event entered the stream; `ValueDay` is the accounting date.
- Events are validated and applied in **stream order**.
- Balances, fees, and interest use entries with `ValueDay` on or before the reporting day.
- Authorizations are approved only if available balance (prior-day closing ledger minus active holds) remains ≥ 0 after the new hold.
- Settlements require a matching, unsettled authorization on the same account.
- Reversals are compensating credits; the original debit is never mutated.
- AED overdraft fees (AED 25.00) are booked as system fee events on days where the closing ledger balance is negative.
- Daily interest (0.04%) accrues on positive closing balances and capitalizes as a single credit on Day 6; rounding remainder is applied to the last accrual day.
- `Money` uses `decimal` with currency scale (AED: 2, BHD: 3).

## Deliverables in this repo

| File | Purpose |
|---|---|
| `NUMBERS.md` | Constants and amounts |
| `AMBIGUITIES.md` | Ambiguities and resolutions |
| `REJECTED.md` | Incorrect acceptance criteria and abandoned approaches |
| `FAILING_TEST.md` | Annotated intentional failure |
| `WORKLOG.md` | Timestamped work log |

## Output

`Ledger.Runner` prints, for each day 1–6:

- Closing ledger, hold, and available balances per account
- Active authorization holds
- Overdraft fees assessed that value date
- Rejected events recorded that day

The final section lists accepted/rejected stream events, booked fees, and capitalized interest.

## Submitting

The assignment requires a **public GitHub repository** with an intact (non-squashed) commit history. This folder is not yet a git repository on your machine — initialize it, commit in logical steps, push, and paste the repo URL into the form:

```bash
git init
git add .
git commit -m "Initial in-memory ledger core implementation"
# Create an empty public repo on GitHub, then:
git remote add origin https://github.com/<you>/InMemoryAccountLedger.git
git push -u origin main
```

Confirm the link opens in a browser without signing in before you submit.
