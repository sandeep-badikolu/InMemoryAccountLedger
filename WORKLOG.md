# WORKLOG.md

- **2026-10-16 09:00** — Parsed assignment requirements (six-day window, ACC-001/ACC-002, event stream E1–E10).
- **2026-10-16 10:30** — Scaffolded `Ledger.Core`, xUnit tests, and console runner; documented ambiguities in `AMBIGUITIES.md`.
- **2026-10-16 12:00** — Implemented authorization holds, settlement validation, reversals, and currency rounding.
- **2026-10-16 14:00** — Added overdraft fee booking, Day-6 interest capitalization with remainder allocation, and per-day reporting (balances, holds, fees, errors).
- **2026-10-16 15:00** — Split E10 into three BHD instalments (`3.333`, `3.333`, `3.334`); retargeted solution to `net10.0` for local SDK.
- **2026-10-16 15:30** — Ran `dotnet test` (one intentional failure); verified `Ledger.Runner` daily output.
- **2026-10-16 16:00** — Aligned stream with official spec (E6 AED 160, E7/E9 AED 820, E10 recorded Day 6).
- **2026-10-16 16:15** — Initialized git, pushed to public GitHub `sandeep-badikolu/InMemoryAccountLedger` (5+ commits, no squash).
- **2026-10-16 16:30** — Expanded `ARCHITECTURE.md` and generated Part 2 PDF under `submission/`.
