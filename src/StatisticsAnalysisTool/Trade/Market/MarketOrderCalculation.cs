using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace StatisticsAnalysisTool.Trade.Market;

internal sealed record MarketOrderCalculation(
    bool IsLimitValid,
    ulong? MaximumUnitPrice,
    IReadOnlyList<MarketOrderRow> MatchingOrders,
    long TotalAmount,
    decimal TotalPrice)
{
    public int OrderCount => MatchingOrders.Count;
}

internal static class MarketOrderCalculator
{
    public static MarketOrderCalculation Calculate(IEnumerable<MarketOrderRow> orders, string maximumUnitPriceText)
    {
        var orderList = orders?.ToList() ?? [];
        if (!TryParseMaximumPrice(maximumUnitPriceText, out var maximumUnitPrice))
        {
            return new MarketOrderCalculation(false, null, [], 0, 0);
        }

        var matchingOrders = maximumUnitPrice.HasValue
            ? orderList.Where(x => x.UnitPrice <= maximumUnitPrice.Value).ToList()
            : orderList;

        return new MarketOrderCalculation(
            true,
            maximumUnitPrice,
            new ReadOnlyCollection<MarketOrderRow>(matchingOrders),
            matchingOrders.Sum(x => (long) x.Amount),
            matchingOrders.Sum(x => x.TotalPrice));
    }

    internal static bool TryParseMaximumPrice(string value, out ulong? maximumUnitPrice)
    {
        maximumUnitPrice = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        if (trimmed.Any(character => !char.IsDigit(character)
                                     && !char.IsWhiteSpace(character)
                                     && character is not '.' and not ',' and not '_'))
        {
            return false;
        }

        var normalized = new string(trimmed.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(normalized)
            || !ulong.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return false;
        }

        maximumUnitPrice = parsedValue;
        return true;
    }
}
