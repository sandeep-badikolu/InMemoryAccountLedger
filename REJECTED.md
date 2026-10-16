# REJECTED.md — Acceptance criteria review

## Accepted as correct

| Criterion | Verdict |
|---|---|
| Day 2 closing ledger balance is AED **-570.00** when evaluated after E7 and before E9 | **Accepted** — verified in `Day2ClosingBalance_IsNegative570_BeforeE9IsApplied`. |
| E7 causes exactly one overdraft fee on Day 2 | **Accepted in the E7-only window** — fee books when E7 is present without E9; not simultaneously compatible with full post-E9 fee rollback without a fee-reversal policy. |
| Day 4 settlement of Auth-A must be accepted | **Accepted** — E5 succeeds. |
| Settlement without a prior authorization must be rejected (E6 / Auth-Z) | **Accepted** — E6 rejected; ledger unchanged by E6. |
| If Auth-B is approved, hold reduces available balance but not ledger balance | **Accepted** — authorization does not post a debit. |
| After E9, ledger balances return to pre-E7 values | **Accepted** — ACC-001 ledger AED 465.00 through Day 5–6 snapshots. |

## Rejected — incorrect or unsafe

### “The three BHD instalments in E10 must each be BHD 3.334.”

**Reason:** `3 × 3.334 = 10.002`, which exceeds the fixed total of `10.000`. Use `3.333 + 3.333 + 3.334 = 10.000`.

### “If the rounded daily interest accruals do not sum to the capitalized total, the remainder is discarded.”

**Reason:** Discarding a remainder breaks reconciliation. This implementation assigns any remainder to the last accrual day so daily rounded accruals sum to the capitalized credit.

### “A settlement referencing an authorization ID absent from the ledger may proceed if the account has enough money.”

**Reason:** Available funds do not substitute for authorization. E6 (`Auth-Z`) is rejected.

### “Authorization should debit the ledger immediately.”

**Reason:** Holds reserve available balance only; ledger balance changes on settlement (or explicit debit/credit).

### “Use binary floating-point for monetary amounts.”

**Reason:** Use `decimal` (or integer minor units) with explicit currency rounding.

## Not simultaneously satisfiable without extra policy

### “E7 causes a Day-2 fee” **and** “After E9, all balances **and fees** return to pre-E7 values.”

**Reason:** If a fee is booked when Day 2 closes negative, reversing E9 restores the debit but does not automatically reverse a separately posted fee. This repo recomputes fees from the final value-dated ledger (so post-E9 there is no Day-2 fee). The intentional failing test documents the alternate expectation. See `FAILING_TEST.md`.

## Abandoned approaches

- **Mutating or deleting stream events** — violates append-only ledger rule.
- **Approving authorizations against same-day ledger including backdated debits recorded later** — would reject Auth-B; replaced with prior-day closing balance check documented in `AMBIGUITIES.md`.
