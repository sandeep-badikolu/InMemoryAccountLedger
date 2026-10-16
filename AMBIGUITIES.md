# AMBIGUITIES.md — Questions and resolutions

The exercise deliberately combines value dates, recorded dates, reversals, fees, and interest. These must not be silently conflated.

## 1. Does a reversal erase a previously assessed overdraft fee?

**Ambiguity:** E7 is recorded on Day 5 with value date Day 2 and takes ACC-001 to `-370.00` on Day 2. The acceptance criteria say this causes one AED 25 fee on Day 2. E9 later reverses E7 with value date Day 2. Another criterion says all balances and fees return to their pre-E7 values.

**Resolution for this implementation:** The reversal compensates the debit for ledger-balance purposes, but the fee rule is not automatically reversed. The assignment does not explicitly say that reversal of a debit reverses a separately assessed fee. This means the two acceptance statements cannot both be guaranteed without a fee-reversal policy. The fee should be represented as its own immutable event in a production design.

## 2. Are events processed by stream order or value date?

**Ambiguity:** E7 and E9 are entered later but carry a Day 2 value date.

**Resolution:** Validate events in stream order; compute reporting balances by `ValueDay`. This is a simplified accounting model. A production system would specify whether a backdated event triggers a full recalculation of fee/interest assessments.

## 3. How is a reversal identified and valued?

**Ambiguity:** E9 says it reverses E7 but does not separately define whether amount is mandatory or inferred.

**Resolution:** Require a same-account debit target and prevent multiple reversals of one debit. The reversal amount in the supplied event is the original debit amount. The original event is never mutated.

## 4. What is the correct allocation of BHD 10.000 into three instalments?

**Resolution:** Equal instalments are not all exactly representable at three decimal places. Use `3.333`, `3.333`, and `3.334` (or another explicitly specified residual allocation). Their sum is exactly `10.000`. Three payments of `3.334` are incorrect because they sum to `10.002`.

## 5. What if rounded daily interest does not equal rounded total interest?

**Resolution:** Do not discard the remainder. Assign it deterministically to a designated accrual day or post a documented rounding adjustment so the sum of daily accruals equals the capitalized amount. The exact convention should be approved and tested.

## 6. Is a settlement allowed to exceed the authorization amount?

**Resolution:** No. The implementation rejects settlements greater than the original authorization and rejects missing, mismatched, or already-settled authorizations.

## 7. Does authorization change ledger balance?

**Resolution:** No. It changes active holds and therefore available balance only. Available balance = ledger balance − active holds.

## 9. Which ledger balance is used when approving an authorization?

**Ambiguity:** Backdated debits (E7) share a recorded day with Auth-B (E8) but have an earlier value date.

**Resolution:** For an authorization on recorded day `R` and value day `V`, compare against:

- entries with `RecordedDay < R` and `ValueDay <= V - 1`, plus
- same-day entries with `ValueDay == V` already accepted earlier in the stream.

This approves Auth-B against the Day-4 close (AED 465.00) while E7 is still excluded until its recorded day is processed for same-day checks.

## 8. Is overdraft fee amount currency-specific?

**Ambiguity:** The prompt specifies AED 25.00 but does not define a BHD fee.

**Resolution:** Apply the fee rule only to AED accounts. Do not infer a BHD fee from the AED amount.
