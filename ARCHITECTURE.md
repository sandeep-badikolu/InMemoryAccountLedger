# Architecture & Trade-offs — In-Memory Account Ledger Core

**Author:** Sandeep Badikolu  
**Repository:** https://github.com/sandeep-badikolu/InMemoryAccountLedger  
**Scope:** Staff engineer exercise — in-memory ledger replay (Days 1–6), no persistence layer.

---

## 1. Append-only at scale

### What breaks at 100× volume?

The reference engine keeps the full input stream in memory and, for each reporting day, scans accepted events to compute closing balances, holds, fees, and interest accruals. Complexity scales roughly as **O(days × events × accounts)** per replay. At 100× event volume:

- **Heap:** The event log and derived system events (fees, interest capitalization) grow without bound.
- **CPU:** Repeated scans for balance/hold calculation dominate; snapshot generation becomes expensive.
- **Latency:** A single-threaded replay that is acceptable for a test harness becomes unsuitable for interactive APIs or batch close-of-day at bank scale.

### Where is state unbounded?

| State | Grows with |
|-------|------------|
| Immutable event log | Every posted/rejected stream event + system-generated fee/interest events |
| Authorization index | Distinct auth IDs (bounded in exercise; unbounded in production) |
| Daily snapshot materialization | Days × accounts (recomputed each replay unless cached) |

Nothing is compacted, archived, or tiered. There is no snapshot store — only re-derivation from the log.

### Cheapest structural change

**Segmented append-only log with boundary snapshots:** partition the log by time (e.g. calendar day or month). At each segment boundary, persist an immutable **account summary** (ledger balance, active holds, last sequence number). Replay for a given date range loads one summary plus only events in the tail segment. This preserves append-only semantics (new segment headers and events are appended; history is never rewritten) while bounding work to “tail + one summary” instead of the full history.

---

## 2. Value-dated entries — UAE production surface

Backdated debits (e.g. E7 with `value_date` Day 2, recorded Day 5) change historical closes and can alter fees, interest, liquidity reporting, and customer statements after the fact.

For a **UAE-licensed** institution, value dating touches:

- **Customer fairness & disclosure** — balances and charges shown on channels must reconcile to the authoritative ledger.
- **Regulatory reporting** — CBUAE supervisory returns and liquidity metrics depend on correct as-of balances.
- **Audit & AML** — investigators need an immutable trail of who introduced a backdated entry and why.

### One control before go-live

**Maker–checker with effective-date policy and automated restatement:**  
Block or queue any posting where `value_date` is before the current business date unless a second approver (operations or finance control) authorizes it in a workflow system. Persist approval metadata (user, timestamp, reason code, customer impact flag). On approval, run a **deterministic restatement job** that recalculates affected days’ fees, interest, and regulatory extracts and stores versioned outputs — without mutating original events (compensating entries only).

---

## 3. Authorization lifecycle (beyond settlement)

Settlements are one terminal state. Production systems must handle:

| Terminal / transition | Scenario | Mandated behavior |
|----------------------|----------|-------------------|
| **Expiry** | Card pre-auth TTL (hotels, petrol pumps) | Release hold at expiry; no ledger debit |
| **Partial capture** | Final amount less than hold | Debit actual amount; release residual hold |
| **Void / reversal before capture** | Merchant cancels | Idempotent void; remove hold |
| **Incremental authorization** | Tips, multi-shipment | Link child auths; cap total exposure |
| **Chargeback** | Dispute after capture | New dispute ledger workflow; never reuse auth id as settlement |
| **Stale hold** | Auth-B never settled (assignment) | Report open exposure; enforce TTL and escalation |

The exercise models holds affecting **available** balance only; production must align TTL, reporting, and customer notifications for unreleased holds.

---

## 4. Scope trade-offs (what we cut and deferred risk)

| Cut / simplification | Rationale in exercise | Production risk deferred |
|---------------------|------------------------|---------------------------|
| No database / event store | Assignment scope | No durability, DR, or horizontal scale |
| Single-process replay | Determinism & clarity | Race conditions under concurrent writers |
| Fee recompute from final value-dated ledger | Resolves E7/E9 ambiguity without fee-reversal events | Posted fees may not match operational “point-in-time” truth |
| Authorization balance uses recorded-day + value-day rules | Allows Auth-B approval with backdated E7 | May diverge from real-time available balance |
| No idempotency beyond unique event IDs | Smaller surface | Duplicate delivery from messaging retries |
| Interest only capitalized Day 6 | Spec | No mid-period payouts, tiered rates, or tax withholding |
| Two static currencies, no FX | Spec | No cross-currency holds or settlement |
| No API / UI / authn | Spec | Integration, rate limits, and fraud monitoring omitted |

---

## 5. Implementation stance (link to Part 1)

The codebase enforces **immutable input events**, **explicit rejection** (e.g. E6 / Auth-Z), **append-only** semantics, and separation of **ledger vs available** balance. System fees and interest are generated as separate events for reporting. One **intentionally failing test** documents the conflict between “fee when Day 2 goes negative” and “fees return to pre-E7 state after E9” when fees are recomputed — see `FAILING_TEST.md` and `REJECTED.md`.

Production evolution: externalize the log (e.g. Kafka + object storage), **per-account command serialization** (optimistic locking), materialized read models for statements, and first-class fee/interest entries with explicit reversal policies.

---

*End of document — suitable for 2–4 page PDF submission.*
