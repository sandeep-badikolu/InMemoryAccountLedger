# NUMBERS.md — Constants and numerical choices

| Value | Choice | Why |
|---|---:|---|
| Supported window | Day 1 through Day 6 | The assignment's fixed replay window |
| AED precision | 2 decimal places | AED minor unit precision |
| BHD precision | 3 decimal places | BHD minor unit precision |
| Daily interest rate | `0.0004` (0.04%) | Supplied by assignment |
| AED overdraft fee | `25.00` | Supplied by assignment |
| Opening balance ACC-001 | AED `0.00` | Supplied by assignment |
| Opening balance ACC-002 | BHD `0.000` | Supplied by assignment |
| E1 credit | AED `1,200.00` | Supplied by assignment |
| E2 debit | AED `950.00` | Supplied by assignment |
| Auth-A hold | AED `200.00` | Supplied by assignment |
| E4 credit | AED `400.00` | Supplied by assignment |
| Auth-A settlement | AED `185.00` | Supplied by assignment |
| Invalid Auth-Z settlement | AED `180.00` | Supplied by assignment; must be rejected |
| E7 debit / E9 reversal | AED `620.00` | Supplied by assignment |
| Auth-B hold | AED `90.00` | Supplied by assignment |
| E10 credit | BHD `10.000` | Supplied by assignment |
| BHD instalment allocation | `3.333`, `3.333`, `3.334` | Exact total is `10.000`; three `3.334` instalments would total `10.002` |
| AED 10,000 / 12 example | 11 × `833.33`, final `833.37` | Residual allocation guarantees exact sum |
| Rounding mode | `MidpointRounding.AwayFromZero` | Explicit deterministic monetary rounding policy; must be agreed for production |
| Event ID uniqueness | Unique, non-empty | Prevents ambiguous references and double application |
| Fee rule | AED 25 once per negative account/day | Supplied by assignment; whether reversal cancels it is unresolved |
| Interest rule | 0.04% on positive closing ledger balance only | Supplied by assignment |
