# In-Memory Account Ledger Core

A .NET 10 in-memory ledger replay engine for the Mal Bank Staff Engineer exercise. There is no web layer, database, UI, or persistence. The runner replays the supplied six-day event stream and prints daily closing balances, fee assessments, authorization states, and errors.

**Public repository:** https://github.com/sandeep-badikolu/InMemoryAccountLedger

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or compatible runtime)
- Internet on first build if NuGet packages are not cached

## Run

```bash
dotnet restore
dotnet test
dotnet run --project src/Ledger.Runner/Ledger.Runner.csproj
```

The test suite includes **one intentionally failing test** (`DeliberatelyFailingTest_OverdraftFeeRemainsAfterE7EvenWhenE9ReversesDebit`) with **inline comments** explaining the policy conflict. See `FAILING_TEST.md`. All other tests should pass.

## Design summary

- `LedgerEvent` records are immutable. `RecordedDay` is when an event entered the stream; `ValueDay` is the accounting date.
- Events are validated and applied in **stream order**.
- Balances, fees, and interest use entries with `ValueDay` on or before the reporting day.
- Authorizations are approved only if **available balance** (per rules in `AMBIGUITIES.md` §9) remains ≥ 0 after the new hold.
- Settlements require a matching, unsettled authorization on the same account (E6 / Auth-Z is rejected).
- Reversals are compensating credits; the original debit is never mutated.
- AED overdraft fees (AED 25.00) are booked as system fee events when the pre-fee closing balance is negative for that value date.
- Daily interest (0.04%) accrues on positive closing balances and capitalizes as a single credit on Day 6; rounding remainder is applied to the last accrual day.
- `Money` uses `decimal` with currency scale (AED: 2, BHD: 3).

## Assignment deliverables (Part 1)

| File | Purpose |
|---|---|
| `README.md` | How to run and read output |
| `NUMBERS.md` | Constants and amounts |
| `AMBIGUITIES.md` | Ambiguities and resolutions |
| `REJECTED.md` | Refused criteria and abandoned approaches |
| `FAILING_TEST.md` | Annotated intentional failure |
| `WORKLOG.md` | Timestamped work log |
| `ARCHITECTURE.md` | Part 2 source document (PDF in `submission/`) |

## Output

`Ledger.Runner` prints, for each day 1–6:

- Closing ledger, hold, and available balances per account
- Active authorization holds
- Overdraft fees assessed that value date
- Rejected events recorded that day

The final section lists accepted/rejected stream events, booked fees, and capitalized interest.

## Part 2 (architecture PDF)

Upload **`submission/Mal-Bank-Architecture-Trade-offs.pdf`** to the assessment form. Regenerate with:

```bash
pip install fpdf2
python scripts/generate_architecture_pdf.py
```
