namespace Ledger.Core;

/// <summary>
/// Deterministic in-memory event replay. Input event records are never mutated.
/// Events are considered in stream order; their accounting effect is placed on ValueDay.
/// </summary>
public sealed class LedgerEngine
{
    private const decimal DailyInterestRate = 0.0004m;
    private const decimal AedOverdraftFee = 25.00m;

    private readonly IReadOnlyDictionary<string, Account> _accounts;

    public LedgerEngine(IEnumerable<Account> accounts)
    {
        var list = accounts.ToList();
        if (list.Count == 0) throw new ArgumentException("At least one account is required.", nameof(accounts));
        if (list.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != list.Count)
            throw new ArgumentException("Account IDs must be unique.", nameof(accounts));
        foreach (var account in list)
        {
            if (string.IsNullOrWhiteSpace(account.Id)) throw new ArgumentException("Account ID is required.");
            if (account.OpeningBalance != Money.Round(account.OpeningBalance, account.Currency))
                throw new ArgumentException($"Opening balance for {account.Id} exceeds currency precision.");
        }
        _accounts = list.ToDictionary(a => a.Id, StringComparer.Ordinal);
    }

    public ReplayResult Replay(IEnumerable<LedgerEvent> source, int finalDay = 6)
    {
        if (finalDay < 1) throw new ArgumentOutOfRangeException(nameof(finalDay));
        var events = source.ToList();
        ValidateEvents(events, finalDay);

        var accepted = new List<LedgerEvent>();
        var rejected = new List<RejectedEvent>();
        var seenAuthorizations = new Dictionary<string, (string AccountId, decimal Amount, bool Settled)>(StringComparer.Ordinal);
        var reversalTargets = new HashSet<string>(StringComparer.Ordinal);

        void Reject(LedgerEvent e, string reason) => rejected.Add(new RejectedEvent(e.Id, e.RecordedDay, reason));

        foreach (var e in events)
        {
            var account = _accounts[e.AccountId];
            var roundedAmount = Money.Round(e.Amount, account.Currency);
            switch (e.Kind)
            {
                case EventKind.Credit:
                    accepted.Add(e);
                    break;

                case EventKind.Debit:
                    accepted.Add(e);
                    break;

                case EventKind.Authorization:
                    if (string.IsNullOrWhiteSpace(e.AuthorizationId))
                    {
                        Reject(e, "Authorization ID is required.");
                        break;
                    }
                    if (seenAuthorizations.ContainsKey(e.AuthorizationId))
                    {
                        Reject(e, $"Authorization ID '{e.AuthorizationId}' already exists.");
                        break;
                    }
                    var ledgerBeforeHold = LedgerBalanceForAuthorization(account.Id, e, accepted, events);
                    var holdsBefore = ActiveHoldsForAuthorization(account.Id, e, accepted);
                    var available = ledgerBeforeHold - holdsBefore;
                    if (available - roundedAmount < 0m)
                    {
                        Reject(e, "Insufficient available balance for authorization hold.");
                        break;
                    }
                    seenAuthorizations[e.AuthorizationId] = (e.AccountId, roundedAmount, false);
                    accepted.Add(e);
                    break;

                case EventKind.Settlement:
                    if (string.IsNullOrWhiteSpace(e.AuthorizationId) ||
                        !seenAuthorizations.TryGetValue(e.AuthorizationId, out var auth))
                    {
                        Reject(e, $"Settlement references missing authorization '{e.AuthorizationId ?? "(null)"}'.");
                        break;
                    }
                    if (auth.AccountId != e.AccountId)
                    {
                        Reject(e, "Settlement account does not match authorization account.");
                        break;
                    }
                    if (auth.Settled)
                    {
                        Reject(e, $"Authorization '{e.AuthorizationId}' has already been settled.");
                        break;
                    }
                    if (roundedAmount > auth.Amount)
                    {
                        Reject(e, "Settlement amount exceeds authorized amount.");
                        break;
                    }
                    seenAuthorizations[e.AuthorizationId] = (auth.AccountId, auth.Amount, true);
                    accepted.Add(e);
                    break;

                case EventKind.Reversal:
                    if (string.IsNullOrWhiteSpace(e.ReversesEventId) ||
                        !events.Any(x => x.Id == e.ReversesEventId && x.Kind == EventKind.Debit && x.AccountId == e.AccountId))
                    {
                        Reject(e, "Reversal must reference a debit event for the same account.");
                        break;
                    }
                    var target = events.First(x => x.Id == e.ReversesEventId);
                    if (reversalTargets.Contains(target.Id))
                    {
                        Reject(e, "Debit event has already been reversed.");
                        break;
                    }
                    if (roundedAmount != 0m && roundedAmount != Money.Round(target.Amount, account.Currency))
                    {
                        Reject(e, "Reversal amount must match the original debit amount.");
                        break;
                    }
                    reversalTargets.Add(target.Id);
                    accepted.Add(e);
                    break;

                case EventKind.Fee:
                case EventKind.InterestCapitalization:
                    Reject(e, "Fee and interest events are generated by the engine, not supplied in the input stream.");
                    break;

                default:
                    Reject(e, "Unsupported event kind.");
                    break;
            }
        }

        var bookedFees = BookOverdraftFees(accepted, events, finalDay);
        var allPosted = accepted.Concat(bookedFees).ToList();

        var interestDaily = _accounts.Keys.ToDictionary(id => id, _ => new decimal[finalDay], StringComparer.Ordinal);
        for (var day = 1; day <= finalDay; day++)
        {
            foreach (var account in _accounts.Values)
            {
                var dayBalance = LedgerBalanceAsOf(account.Id, allPosted, day, events);
                interestDaily[account.Id][day - 1] = dayBalance > 0m
                    ? Money.Round(dayBalance * DailyInterestRate, account.Currency)
                    : 0m;
            }
        }

        var bookedInterest = finalDay == 6 ? CapitalizeInterest(interestDaily, finalDay) : new List<LedgerEvent>();
        allPosted.AddRange(bookedInterest);

        var interestTotals = _accounts.Keys.ToDictionary(
            id => id,
            id => bookedInterest.Where(e => e.AccountId == id).Sum(e => e.Amount),
            StringComparer.Ordinal);

        var snapshots = new List<DailySnapshot>();
        for (var day = 1; day <= finalDay; day++)
        {
            var feesToday = bookedFees
                .Where(f => f.ValueDay == day)
                .GroupBy(f => f.AccountId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount), StringComparer.Ordinal);

            var accountSnapshots = new Dictionary<string, AccountSnapshot>(StringComparer.Ordinal);
            foreach (var account in _accounts.Values)
            {
                var balance = LedgerBalanceAsOf(account.Id, allPosted, day, events);
                var activeHolds = ActiveHoldsAtDay(account.Id, day, accepted);
                accountSnapshots[account.Id] = new AccountSnapshot(
                    account.Id,
                    account.Currency,
                    Money.Round(balance, account.Currency),
                    Money.Round(activeHolds, account.Currency),
                    Money.Round(balance - activeHolds, account.Currency));
            }

            var authStates = _accounts.Keys.ToDictionary(
                id => id,
                id => (IReadOnlyList<AuthorizationState>)ActiveAuthorizationStates(id, day, accepted),
                StringComparer.Ordinal);

            var dayErrors = rejected.Where(r => r.RecordedDay == day).ToList();

            var feesAssessed = _accounts.Keys.ToDictionary(
                id => id,
                id => feesToday.TryGetValue(id, out var fee) ? fee : 0m,
                StringComparer.Ordinal);

            snapshots.Add(new DailySnapshot(day, accountSnapshots, feesAssessed, authStates, dayErrors));
        }

        return new ReplayResult
        {
            Accounts = snapshots[^1].Accounts,
            DailySnapshots = snapshots,
            RejectedEvents = rejected,
            AcceptedEvents = accepted,
            InterestByAccount = interestTotals,
            BookedFees = bookedFees,
            BookedInterest = bookedInterest
        };
    }

    private void ValidateEvents(List<LedgerEvent> events, int finalDay)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in events)
        {
            if (string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id))
                throw new ArgumentException($"Event IDs must be non-empty and unique: '{e.Id}'.");
            if (e.RecordedDay < 1 || e.RecordedDay > finalDay || e.ValueDay < 1 || e.ValueDay > finalDay)
                throw new ArgumentException($"Event {e.Id} has a day outside 1..{finalDay}.");
            if (!_accounts.ContainsKey(e.AccountId))
                throw new ArgumentException($"Event {e.Id} refers to unknown account {e.AccountId}.");
            if (e.Amount < 0) throw new ArgumentException($"Event {e.Id} amount must be non-negative.");
            if (e.Amount != Money.Round(e.Amount, _accounts[e.AccountId].Currency))
                throw new ArgumentException($"Event {e.Id} amount exceeds currency precision.");
        }
    }

    private List<LedgerEvent> BookOverdraftFees(
        IReadOnlyList<LedgerEvent> accepted,
        IReadOnlyList<LedgerEvent> allEvents,
        int finalDay)
    {
        var fees = new List<LedgerEvent>();
        var posted = accepted.ToList();

        for (var day = 1; day <= finalDay; day++)
        {
            foreach (var account in _accounts.Values.Where(a => a.Currency == Currency.AED))
            {
                var balance = LedgerBalanceAsOf(account.Id, posted, day, allEvents, includeFees: false);
                if (balance >= 0m) continue;

                var feeEvent = new LedgerEvent(
                    $"SYS-FEE-{account.Id}-D{day}",
                    day,
                    day,
                    EventKind.Fee,
                    account.Id,
                    AedOverdraftFee,
                    Description: "AED overdraft fee (closing ledger balance negative on this value date).");
                fees.Add(feeEvent);
                posted.Add(feeEvent);
            }
        }

        return fees;
    }

    private List<LedgerEvent> CapitalizeInterest(Dictionary<string, decimal[]> interestDaily, int finalDay)
    {
        var booked = new List<LedgerEvent>();
        foreach (var account in _accounts.Values)
        {
            var daily = interestDaily[account.Id];
            var roundedDaily = daily.Select(x => Money.Round(x, account.Currency)).ToArray();
            var sum = roundedDaily.Sum();
            var capitalized = Money.Round(sum, account.Currency);
            var remainder = capitalized - sum;
            if (remainder != 0m)
                roundedDaily[^1] = Money.Round(roundedDaily[^1] + remainder, account.Currency);

            if (capitalized <= 0m) continue;

            booked.Add(new LedgerEvent(
                $"SYS-INT-{account.Id}",
                finalDay,
                finalDay,
                EventKind.InterestCapitalization,
                account.Id,
                capitalized,
                Description: "Daily interest accruals capitalized at end of Day 6."));
        }

        return booked;
    }

    private decimal LedgerBalanceAsOf(
        string accountId,
        IReadOnlyList<LedgerEvent> posted,
        int asOfValueDay,
        IReadOnlyList<LedgerEvent> allEvents,
        bool includeFees = true,
        bool includeInterest = true)
    {
        if (asOfValueDay < 0) return _accounts[accountId].OpeningBalance;

        var account = _accounts[accountId];
        decimal balance = account.OpeningBalance;
        foreach (var e in posted.Where(e => e.AccountId == accountId && e.ValueDay <= asOfValueDay))
        {
            if (!includeFees && e.Kind == EventKind.Fee) continue;
            if (!includeInterest && e.Kind == EventKind.InterestCapitalization) continue;
            balance += SignedEffect(e, account.Currency, allEvents);
        }
        return Money.Round(balance, account.Currency);
    }

    private decimal LedgerBalanceForAuthorization(
        string accountId,
        LedgerEvent authorization,
        IReadOnlyList<LedgerEvent> acceptedSoFar,
        IReadOnlyList<LedgerEvent> allEvents)
    {
        var account = _accounts[accountId];
        var recordedDay = authorization.RecordedDay;
        var valueDay = authorization.ValueDay;
        decimal balance = account.OpeningBalance;

        foreach (var e in acceptedSoFar.Where(e => e.AccountId == accountId))
        {
            var include = (e.RecordedDay < recordedDay && e.ValueDay <= valueDay - 1)
                || (e.RecordedDay == recordedDay && e.ValueDay == valueDay);
            if (!include) continue;
            balance += SignedEffect(e, account.Currency, allEvents);
        }

        return Money.Round(balance, account.Currency);
    }

    private static decimal ActiveHoldsForAuthorization(
        string accountId,
        LedgerEvent authorization,
        IReadOnlyList<LedgerEvent> acceptedSoFar)
    {
        var recordedDay = authorization.RecordedDay;
        var valueDay = authorization.ValueDay;
        var holds = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var e in acceptedSoFar.Where(e => e.AccountId == accountId))
        {
            var inScope = (e.RecordedDay < recordedDay && e.ValueDay <= valueDay - 1)
                || (e.RecordedDay == recordedDay && e.ValueDay == valueDay);
            if (!inScope) continue;

            if (e.Kind == EventKind.Authorization && e.AuthorizationId is not null)
                holds[e.AuthorizationId] = e.Amount;
            if (e.Kind == EventKind.Settlement && e.AuthorizationId is not null)
                holds.Remove(e.AuthorizationId);
        }

        return holds.Values.Sum();
    }

    private static decimal SignedEffect(LedgerEvent e, Currency currency, IReadOnlyList<LedgerEvent> allEvents)
    {
        var amount = Money.Round(e.Amount, currency);
        return e.Kind switch
        {
            EventKind.Credit => amount,
            EventKind.Debit => -amount,
            EventKind.Settlement => -amount,
            EventKind.Fee => -amount,
            EventKind.InterestCapitalization => amount,
            EventKind.Reversal => ResolveReversalAmount(e, allEvents, currency),
            _ => 0m
        };
    }

    private static decimal ResolveReversalAmount(LedgerEvent reversal, IReadOnlyList<LedgerEvent> allEvents, Currency currency)
    {
        if (reversal.ReversesEventId is null) return 0m;
        var target = allEvents.FirstOrDefault(e => e.Id == reversal.ReversesEventId);
        return target is null ? 0m : Money.Round(target.Amount, currency);
    }

    private static decimal ActiveHoldsAtDay(string accountId, int day, IReadOnlyList<LedgerEvent> accepted)
    {
        var holds = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var e in accepted.Where(e => e.AccountId == accountId && e.ValueDay <= day))
        {
            if (e.Kind == EventKind.Authorization && e.AuthorizationId is not null)
                holds[e.AuthorizationId] = e.Amount;
            if (e.Kind == EventKind.Settlement && e.AuthorizationId is not null)
                holds.Remove(e.AuthorizationId);
        }
        return holds.Values.Sum();
    }

    private static List<AuthorizationState> ActiveAuthorizationStates(string accountId, int day, IReadOnlyList<LedgerEvent> accepted)
    {
        var holds = new Dictionary<string, (decimal Amount, int ValueDay)>(StringComparer.Ordinal);
        foreach (var e in accepted.Where(e => e.AccountId == accountId && e.ValueDay <= day))
        {
            if (e.Kind == EventKind.Authorization && e.AuthorizationId is not null)
                holds[e.AuthorizationId] = (e.Amount, e.ValueDay);
            if (e.Kind == EventKind.Settlement && e.AuthorizationId is not null)
                holds.Remove(e.AuthorizationId);
        }

        return holds
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new AuthorizationState(kv.Key, kv.Value.Amount, kv.Value.ValueDay))
            .ToList();
    }
}
