namespace Ledger.Core;

public static class Money
{
    public static int Scale(Currency currency) => currency switch
    {
        Currency.AED => 2,
        Currency.BHD => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };

    public static decimal Round(decimal amount, Currency currency) =>
        decimal.Round(amount, Scale(currency), MidpointRounding.AwayFromZero);

    public static decimal ParseAmount(string amount, Currency currency)
    {
        var parsed = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);
        return Round(parsed, currency);
    }
}
