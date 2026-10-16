# Architecture & Trade-offs — In-Memory Account Ledger Core

*Export this document to PDF for Part 2 submission (2–4 pages).*

## 1. Append-only at scale

**What breaks at 100× volume?**  
This exercise stores every input event in memory and recomputes balances by scanning accepted events per account per day. At 100× event volume, CPU and heap grow roughly linearly with stream length; daily snapshots become O(days × events × accounts). Authorization indexes and hold maps stay bounded by active auths, but the event list is unbounded.

**Where state accumulates**  
The full append-only event log, derived fee/interest system events, and per-replay snapshot materialization. Nothing is compacted or archived.

**Cheapest structural change**  
Introduce **event segmentation**: immutable daily (or monthly) **sequence segments** with precomputed **closing balances and hold summaries** per account at segment boundaries. Replay only needs the last segment summary plus events since that boundary. Storage can remain append-only (new segment headers appended); no mutation of historical events.

## 2. Value-dated entries in production (UAE context)

Backdated debits (E7) change historical closing balances and can affect fees, interest, and regulatory reporting retroactively. For a UAE-licensed bank, value dating intersects **CBUAE** expectations on accurate customer balances, **AML/CFT** audit trails, and **IFRS** recognition timing.

**Control before go-live:** **Maker–checker with effective-date policy** — no value-dated posting outside a defined backdating window without second-line approval, immutable audit record (who approved, business reason, customer impact), and automated **recalculation jobs** for fees/interest/tax reports affected by the adjustment.

## 3. Authorization lifecycle beyond settlement

Authorizations can end without a matching settlement amount:

| End state | Real-world scenario | Required behavior |
|-----------|---------------------|-------------------|
| **Expiry** | Card pre-auth TTL (hotels, fuel) | Release hold on expiry; no ledger debit |
| **Partial settlement** | Final amount &lt; hold | Debit settled amount; release remainder |
| **Reversal / void** | Merchant cancel before capture | Remove hold; idempotent void event |
| **Chargeback / dispute** | Post-settlement dispute | Separate dispute workflow; do not reuse auth id |
| **Incremental auth** | Tipping, multi-capture | Extend or replace hold with explicit linking |

Auth-B in the assignment (never settled) remains an **active hold** until expiry or explicit release — production must define TTL and reporting of stale holds.

## 4. Scope trade-offs (exercise vs production)

| Simplified | Why | Deferred risk |
|------------|-----|----------------|
| In-memory only | Assignment scope | Data loss, no DR, no multi-node consistency |
| Single-process replay | Determinism | No concurrent writers; race on balance updates |
| Fee recompute from final value-dated ledger | Resolves reversal ambiguity simply | Incorrect if fees must be immutable once posted |
| Authorization balance rule (recorded vs value date) | Makes Auth-B approvable | Misalignment with real-time available balance |
| No idempotency keys / duplicate detection beyond event id | Smaller code | Double-posting from retries |
| Interest capitalized only on Day 6 | Spec compliance | No mid-period payouts or tiered rates |
| No multi-currency FX | Two static currencies | Cross-currency auth/settlement unsupported |

## 5. Design stance

The core models **immutable events**, **explicit rejection**, and **separation of ledger vs available balance**. Production would externalize the event log (Kafka/S3), use **command handlers with optimistic locking per account**, materialize **read models** for statements, and treat fees/interest as **first-class posted entries** with their own reversal policies.
