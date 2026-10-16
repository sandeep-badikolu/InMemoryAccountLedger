namespace Ledger.Core;

public enum Currency
{
    AED,
    BHD
}

public enum EventKind
{
    Credit,
    Debit,
    Authorization,
    Settlement,
    Reversal,
    Fee,
    InterestCapitalization
}

public sealed record Account(string Id, Currency Currency, decimal OpeningBalance);

/// <summary>
/// Immutable input event. Amount is positive; direction is determined by Kind.
/// ValueDay determines the accounting day, not the day the event entered the stream.
/// </summary>
public sealed record LedgerEvent(
    string Id,
    int RecordedDay,
    int ValueDay,
    EventKind Kind,
    string AccountId,
    decimal Amount = 0m,
    string? AuthorizationId = null,
    string? ReversesEventId = null,
    string? Description = null);

public sealed record AccountSnapshot(
    string AccountId,
    Currency Currency,
    decimal LedgerBalance,
    decimal ActiveHolds,
    decimal AvailableBalance);

public sealed record DailySnapshot(
    int Day,
    IReadOnlyDictionary<string, AccountSnapshot> Accounts,
    IReadOnlyDictionary<string, decimal> FeesAssessed,
    IReadOnlyDictionary<string, IReadOnlyList<AuthorizationState>> AuthorizationStates,
    IReadOnlyList<RejectedEvent> Errors);

public sealed record RejectedEvent(string EventId, int RecordedDay, string Reason);

public sealed record AuthorizationState(string AuthorizationId, decimal HoldAmount, int ValueDay);

public sealed class ReplayResult
{
    public required IReadOnlyDictionary<string, AccountSnapshot> Accounts { get; init; }
    public required IReadOnlyList<DailySnapshot> DailySnapshots { get; init; }
    public required IReadOnlyList<RejectedEvent> RejectedEvents { get; init; }
    public required IReadOnlyList<LedgerEvent> AcceptedEvents { get; init; }
    public required IReadOnlyDictionary<string, decimal> InterestByAccount { get; init; }
    public required IReadOnlyList<LedgerEvent> BookedFees { get; init; }
    public required IReadOnlyList<LedgerEvent> BookedInterest { get; init; }
}
