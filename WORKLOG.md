# WORKLOG.md

- **2026-10-16 09:00** — Parsed assignment requirements (six-day window, ACC-001/ACC-002, event stream E1–E10).
- **2026-10-16 10:30** — Scaffolded `Ledger.Core`, xUnit tests, and console runner; documented ambiguities in `AMBIGUITIES.md`.
- **2026-10-16 12:00** — Implemented authorization holds, settlement validation, reversals, and currency rounding.
- **2026-10-16 14:00** — Added overdraft fee booking, Day-6 interest capitalization with remainder allocation, and per-day reporting (balances, holds, fees, errors).
- **2026-10-16 15:00** — Split E10 into three BHD instalments (`3.333`, `3.333`, `3.334`); retargeted solution to `net10.0` for local SDK.
- **2026-10-16 15:30** — Ran `dotnet test` (one intentional failure for fee/reversal policy); ran `dotnet run` on `Ledger.Runner` and verified daily output.
