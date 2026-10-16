using Ledger.Core;

var accounts = new[]
{
    new Account("ACC-001", Currency.AED, 0.00m),
    new Account("ACC-002", Currency.BHD, 0.000m)
};

var events = new[]
{
    new LedgerEvent("E1", 1, 1, EventKind.Credit, "ACC-001", 1200.00m, Description: "Day 1 credit"),
    new LedgerEvent("E2", 1, 1, EventKind.Debit, "ACC-001", 950.00m, Description: "Day 1 debit"),
    new LedgerEvent("E3", 2, 2, EventKind.Authorization, "ACC-001", 200.00m, "Auth-A"),
    new LedgerEvent("E4", 3, 3, EventKind.Credit, "ACC-001", 400.00m, Description: "Day 3 credit"),
    new LedgerEvent("E5", 4, 4, EventKind.Settlement, "ACC-001", 185.00m, "Auth-A"),
    new LedgerEvent("E6", 4, 4, EventKind.Settlement, "ACC-001", 160.00m, "Auth-Z"),
    new LedgerEvent("E7", 5, 2, EventKind.Debit, "ACC-001", 820.00m, Description: "Backdated debit"),
    new LedgerEvent("E8", 5, 5, EventKind.Authorization, "ACC-001", 90.00m, "Auth-B"),
    new LedgerEvent("E9", 6, 2, EventKind.Reversal, "ACC-001", 820.00m, ReversesEventId: "E7"),
    new LedgerEvent("E10a", 6, 5, EventKind.Credit, "ACC-002", 3.333m, Description: "BHD instalment 1/3"),
    new LedgerEvent("E10b", 6, 5, EventKind.Credit, "ACC-002", 3.333m, Description: "BHD instalment 2/3"),
    new LedgerEvent("E10c", 6, 5, EventKind.Credit, "ACC-002", 3.334m, Description: "BHD instalment 3/3")
};

var result = new LedgerEngine(accounts).Replay(events);

Console.WriteLine("IN-MEMORY LEDGER REPLAY (Days 1–6)");
Console.WriteLine("==================================");
foreach (var day in result.DailySnapshots)
{
    Console.WriteLine($"--- Day {day.Day} (closing, value_date <= {day.Day}) ---");
    foreach (var account in day.Accounts.Values.OrderBy(x => x.AccountId))
    {
        var scale = Money.Scale(account.Currency);
        Console.WriteLine(
            $"  {account.AccountId} ({account.Currency}): ledger={account.LedgerBalance.ToString($"F{scale}")}, " +
            $"holds={account.ActiveHolds.ToString($"F{scale}")}, available={account.AvailableBalance.ToString($"F{scale}")}");
    }

    foreach (var authGroup in day.AuthorizationStates.OrderBy(x => x.Key, StringComparer.Ordinal))
    {
        if (authGroup.Value.Count == 0) continue;
        Console.WriteLine($"  authorizations ({authGroup.Key}):");
        foreach (var auth in authGroup.Value)
            Console.WriteLine($"    {auth.AuthorizationId} hold {auth.HoldAmount:F2} (value Day {auth.ValueDay})");
    }

    foreach (var fee in day.FeesAssessed.Where(f => f.Value > 0).OrderBy(f => f.Key, StringComparer.Ordinal))
        Console.WriteLine($"  fee assessed {fee.Key}: AED {fee.Value:F2} (value Day {day.Day})");

    if (day.Errors.Count > 0)
    {
        Console.WriteLine("  errors:");
        foreach (var err in day.Errors)
            Console.WriteLine($"    {err.EventId}: {err.Reason}");
    }
}

Console.WriteLine();
Console.WriteLine("STREAM OUTCOME");
Console.WriteLine("ACCEPTED:");
foreach (var e in result.AcceptedEvents)
    Console.WriteLine($"  {e.Id}: {e.Kind} (recorded Day {e.RecordedDay}, value Day {e.ValueDay})");
Console.WriteLine("REJECTED:");
foreach (var e in result.RejectedEvents)
    Console.WriteLine($"  {e.EventId}: {e.Reason}");
Console.WriteLine("BOOKED FEES:");
foreach (var e in result.BookedFees)
    Console.WriteLine($"  {e.Id}: {e.AccountId} AED {e.Amount:F2} (value Day {e.ValueDay})");
Console.WriteLine("BOOKED INTEREST (Day 6 capitalization):");
foreach (var e in result.BookedInterest)
    Console.WriteLine($"  {e.Id}: {e.AccountId} {e.Amount}");
